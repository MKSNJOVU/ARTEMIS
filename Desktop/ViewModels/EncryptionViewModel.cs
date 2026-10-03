using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Artemis.Core.Interfaces;
using Artemis.Desktop.Models;
using Artemis.Desktop.Services;
using Artemis.Desktop.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Artemis.Desktop.ViewModels;

public partial class EncryptionViewModel : ViewModelBase
{
    private const string RecentKind = "encrypt";

    private readonly IEncryptionService _encryptionService;
    private readonly IFilePickerService _filePickerService;
    private readonly IDialogService _dialogService;
    private readonly IRecentFilesService _recentFilesService;
    private readonly ISettingsService _settingsService;
    private readonly INotificationService _notificationService;
    private readonly IShellRevealService _shellRevealService;
    private readonly IClipboardService _clipboardService;

    private CancellationTokenSource? _cancellation;
    private string? _lastOutputPath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EncryptCommand))]
    [NotifyPropertyChangedFor(nameof(HasFiles))]
    private ObservableCollection<SelectedFileItem> _selectedFiles = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EncryptCommand))]
    private byte[]? _password = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EncryptCommand))]
    private byte[]? _passwordConfirm = [];

    [ObservableProperty]
    private string? _passwordError;

    [ObservableProperty]
    private string? _bannerMessage;

    [ObservableProperty]
    private NotificationKind _bannerKind = NotificationKind.Info;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EncryptCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(RevealOutputCommand))]
    [NotifyPropertyChangedFor(nameof(CanRevealOutput))]
    private bool _isEncrypting;

    [ObservableProperty]
    private bool _showProgress;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RevealOutputCommand))]
    [NotifyPropertyChangedFor(nameof(CanRevealOutput))]
    private OperationState _currentState = OperationState.Idle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecentFiles))]
    private ObservableCollection<RecentFileEntry> _recentFiles = [];

    public bool HasFiles => SelectedFiles.Count > 0;
    public bool HasRecentFiles => RecentFiles.Count > 0;
    public bool CanRevealOutput => !IsEncrypting && CurrentState == OperationState.Completed && !string.IsNullOrWhiteSpace(_lastOutputPath);

    private bool CanEncrypt => !IsEncrypting
        && SelectedFiles.Count > 0
        && Password?.Length > 0
        && PasswordConfirm?.Length > 0
        && IsPasswordValid(Password, PasswordConfirm);

    private bool CanSelectFile => !IsEncrypting;

    public EncryptionViewModel(
        IEncryptionService encryptionService,
        IFilePickerService filePickerService,
        IDialogService dialogService,
        IRecentFilesService recentFilesService,
        ISettingsService settingsService,
        INotificationService notificationService,
        IShellRevealService shellRevealService,
        IClipboardService clipboardService)
    {
        _encryptionService = encryptionService;
        _filePickerService = filePickerService;
        _dialogService = dialogService;
        _recentFilesService = recentFilesService;
        _settingsService = settingsService;
        _notificationService = notificationService;
        _shellRevealService = shellRevealService;
        _clipboardService = clipboardService;
        RefreshRecentFiles();
    }

    [RelayCommand(CanExecute = nameof(CanSelectFile))]
    public async Task SelectFile()
    {
        ResetTransientState();
        IReadOnlyList<string>? paths = await _filePickerService.OpenFileAsync(
            "Select file(s) to encrypt",
            allowMultiple: true,
            startPath: _settingsService.Current.LastOpenFolder);

        if (paths is null || paths.Count == 0)
            return;

        AddFiles(paths);
    }

    [RelayCommand]
    private void AddDroppedFiles(IEnumerable<string>? paths)
    {
        if (IsEncrypting || paths is null)
            return;

        ResetTransientState();
        AddFiles(paths);
    }

    [RelayCommand]
    private void RemoveFile(SelectedFileItem? item)
    {
        if (item is null || IsEncrypting)
            return;

        SelectedFiles.Remove(item);
        OnPropertyChanged(nameof(HasFiles));
        EncryptCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void AddRecentFile(RecentFileEntry? entry)
    {
        if (entry is null || IsEncrypting)
            return;

        AddFiles([entry.Path]);
    }

    [RelayCommand(CanExecute = nameof(CanEncrypt))]
    public async Task Encrypt()
    {
        BannerMessage = null;
        PasswordError = null;
        IsEncrypting = true;
        ShowProgress = true;
        CurrentState = OperationState.Processing;
        _cancellation = new CancellationTokenSource();
        CancellationToken token = _cancellation.Token;

        string? currentSavePath = null;
        try
        {
            string? location = _settingsService.Current.DefaultOutputFolder;
            if (string.IsNullOrWhiteSpace(location) || !Directory.Exists(location))
                location = await _filePickerService.OpenFolderAsync("Choose a folder for encrypted files", _settingsService.Current.DefaultOutputFolder);

            if (string.IsNullOrWhiteSpace(location))
            {
                FinishProgress();
                BannerKind = NotificationKind.Warning;
                BannerMessage = "Encryption cancelled.";
                CurrentState = OperationState.Idle;
                return;
            }

            _settingsService.Update(settings => settings.LastOpenFolder = location);

            double progressStep = 100.0 / SelectedFiles.Count;
            double currentProgress = 0;
            int completed = 0;

            foreach (SelectedFileItem file in SelectedFiles.ToList())
            {
                token.ThrowIfCancellationRequested();

                if (!File.Exists(file.FullPath))
                {
                    FinishProgress();
                    BannerKind = NotificationKind.Error;
                    BannerMessage = $"Unable to read {file.FileName}. It may have been moved or deleted.";
                    CurrentState = OperationState.Faulted;
                    return;
                }

                string originalExtension = Path.GetExtension(file.FullPath);
                string fileName = Path.ChangeExtension(file.FileName, "enc");
                currentSavePath = Path.Combine(location, fileName);

                if (File.Exists(currentSavePath))
                {
                    bool overwrite = await _dialogService.ShowConfirmationAsync($"The file {fileName} already exists. Overwrite?");
                    if (!overwrite)
                        continue;
                }

                StatusMessage = $"Encrypting {file.FileName}...";
                using (var sourceStream = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var destinationStream = new FileStream(currentSavePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await _encryptionService.EncryptAsync(sourceStream, destinationStream, Password!, originalExtension);
                }

                _recentFilesService.Add(file.FullPath, RecentKind);
                _lastOutputPath = currentSavePath;
                completed++;
                currentProgress += progressStep;
                ProgressValue = currentProgress;
            }

            FinishProgress();
            if (completed == 0)
            {
                BannerKind = NotificationKind.Warning;
                BannerMessage = "No files were encrypted.";
                CurrentState = OperationState.Idle;
                return;
            }

            CurrentState = OperationState.Completed;
            BannerKind = NotificationKind.Success;
            BannerMessage = completed == 1 ? "Encryption complete." : $"Encrypted {completed} files.";
            _notificationService.Show(BannerMessage, NotificationKind.Success);
            _clipboardService.ScheduleClear(_settingsService.Current.ClipboardAutoWipeSeconds);
            RefreshRecentFiles();
        }
        catch (OperationCanceledException)
        {
            FinishProgress();
            BannerKind = NotificationKind.Warning;
            BannerMessage = "Encryption cancelled.";
            CurrentState = OperationState.Idle;
            DeletePartial(currentSavePath);
        }
        catch (Exception ex)
        {
            FinishProgress();
            BannerKind = NotificationKind.Error;
            BannerMessage = $"Encryption failed: {ex.Message}";
            CurrentState = OperationState.Faulted;
            DeletePartial(currentSavePath);
        }
        finally
        {
            ResetSecrets();
            IsEncrypting = false;
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    [RelayCommand(CanExecute = nameof(IsEncrypting))]
    private void Cancel()
    {
        _cancellation?.Cancel();
    }

    [RelayCommand(CanExecute = nameof(CanRevealOutput))]
    private void RevealOutput()
    {
        if (!string.IsNullOrWhiteSpace(_lastOutputPath))
            _shellRevealService.Reveal(_lastOutputPath);
    }

    partial void OnPasswordChanged(byte[]? value) => UpdatePasswordError();
    partial void OnPasswordConfirmChanged(byte[]? value) => UpdatePasswordError();

    private void AddFiles(IEnumerable<string> paths)
    {
        foreach (string path in paths.Where(File.Exists))
        {
            if (SelectedFiles.Any(file => string.Equals(file.FullPath, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            SelectedFiles.Add(new SelectedFileItem { FullPath = path });
        }

        string? lastFolder = SelectedFiles.LastOrDefault()?.DirectoryName;
        if (!string.IsNullOrWhiteSpace(lastFolder))
            _settingsService.Update(settings => settings.LastOpenFolder = lastFolder);

        OnPropertyChanged(nameof(HasFiles));
        EncryptCommand.NotifyCanExecuteChanged();
    }

    private void RefreshRecentFiles()
    {
        RecentFiles = new ObservableCollection<RecentFileEntry>(_recentFilesService.Get(RecentKind));
    }

    private void UpdatePasswordError()
    {
        if (PasswordConfirm is { Length: > 0 } && !IsPasswordValid(Password ?? [], PasswordConfirm))
            PasswordError = "Passwords do not match.";
        else
            PasswordError = null;

        EncryptCommand.NotifyCanExecuteChanged();
    }

    private void FinishProgress()
    {
        ShowProgress = false;
        ProgressValue = 0;
        StatusMessage = string.Empty;
    }

    private void ResetTransientState()
    {
        CurrentState = OperationState.Idle;
        FinishProgress();
        BannerMessage = null;
    }

    private void ResetSecrets()
    {
        if (Password is not null)
            CryptographicOperations.ZeroMemory(Password);
        if (PasswordConfirm is not null)
            CryptographicOperations.ZeroMemory(PasswordConfirm);
        Password = null;
        PasswordConfirm = null;
    }

    private static void DeletePartial(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            File.Delete(path);
    }

    private static bool IsPasswordValid(byte[]? password, byte[]? passwordConfirm)
    {
        if (password is null || passwordConfirm is null || password.Length != passwordConfirm.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(password, passwordConfirm);
    }
}
