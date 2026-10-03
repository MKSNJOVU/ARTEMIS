using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Artemis.Core.Cryptography;
using Artemis.Core.Interfaces;
using Artemis.Desktop.Models;
using Artemis.Desktop.Services;
using Artemis.Desktop.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Artemis.Desktop.ViewModels;

public partial class DecryptionViewModel : ViewModelBase
{
    private const string RecentKind = "decrypt";

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
    [NotifyCanExecuteChangedFor(nameof(DecryptCommand))]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    private ObservableCollection<SelectedFileItem> _selectedFiles = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DecryptCommand))]
    private byte[]? _password = [];

    [ObservableProperty]
    private string? _bannerMessage;

    [ObservableProperty]
    private NotificationKind _bannerKind = NotificationKind.Info;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DecryptCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(RevealOutputCommand))]
    [NotifyPropertyChangedFor(nameof(CanRevealOutput))]
    private bool _isDecrypting;

    [ObservableProperty]
    private bool _showProgress;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RevealOutputCommand))]
    [NotifyPropertyChangedFor(nameof(CanRevealOutput))]
    private OperationState _currentState = OperationState.Idle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecentFiles))]
    private ObservableCollection<RecentFileEntry> _recentFiles = [];

    public bool HasFile => SelectedFiles.Count > 0;
    public bool HasRecentFiles => RecentFiles.Count > 0;
    public bool CanRevealOutput => !IsDecrypting && CurrentState == OperationState.Completed && !string.IsNullOrWhiteSpace(_lastOutputPath);

    private bool CanDecrypt => !IsDecrypting
        && SelectedFiles.Count > 0
        && Password?.Length > 0;

    private bool CanSelectFile => !IsDecrypting;

    public DecryptionViewModel(
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
        ResetTransientState(keepSelection: true);
        IReadOnlyList<string>? paths = await _filePickerService.OpenFileAsync(
            "Select file to decrypt",
            allowMultiple: false,
            patterns: ["*.enc"],
            startPath: _settingsService.Current.LastOpenFolder);

        if (paths is null || paths.Count == 0)
            return;

        SetFile(paths[0]);
    }

    [RelayCommand]
    private void AddDroppedFiles(IEnumerable<string>? paths)
    {
        if (IsDecrypting || paths is null)
            return;

        string? path = paths.FirstOrDefault();
        if (path is not null)
            SetFile(path);
    }

    [RelayCommand]
    private void RemoveFile(SelectedFileItem? item)
    {
        if (item is null || IsDecrypting)
            return;

        SelectedFiles.Remove(item);
        OnPropertyChanged(nameof(HasFile));
        DecryptCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void AddRecentFile(RecentFileEntry? entry)
    {
        if (entry is null || IsDecrypting)
            return;

        SetFile(entry.Path);
    }

    [RelayCommand(CanExecute = nameof(CanDecrypt))]
    public async Task Decrypt()
    {
        BannerMessage = null;
        IsDecrypting = true;
        ShowProgress = true;
        CurrentState = OperationState.Processing;
        _cancellation = new CancellationTokenSource();
        CancellationToken token = _cancellation.Token;

        string? decryptDestination = null;
        try
        {
            token.ThrowIfCancellationRequested();
            string savePath = SelectedFiles[0].FullPath;

            if (!File.Exists(savePath))
            {
                FinishProgress();
                BannerKind = NotificationKind.Error;
                BannerMessage = "The selected file is no longer available.";
                CurrentState = OperationState.Faulted;
                return;
            }

            string extension = await ExtractFileExtensionAsync(savePath);
            string suggestedFileName = Path.ChangeExtension(Path.GetFileName(savePath), extension);
            string? startPath = _settingsService.Current.DefaultOutputFolder ?? _settingsService.Current.LastOpenFolder;

            decryptDestination = await _filePickerService.SaveFileAsync(suggestedFileName, "Save decrypted file", startPath: startPath);
            if (string.IsNullOrWhiteSpace(decryptDestination))
            {
                FinishProgress();
                BannerKind = NotificationKind.Warning;
                BannerMessage = "Decryption cancelled.";
                CurrentState = OperationState.Idle;
                return;
            }

            if (File.Exists(decryptDestination))
            {
                bool overwrite = await _dialogService.ShowConfirmationAsync($"The file {Path.GetFileName(decryptDestination)} already exists. Overwrite?");
                if (!overwrite)
                {
                    FinishProgress();
                    BannerKind = NotificationKind.Warning;
                    BannerMessage = "Decryption cancelled.";
                    CurrentState = OperationState.Idle;
                    return;
                }
            }

            StatusMessage = "Decrypting file...";
            ProgressValue = 50;
            token.ThrowIfCancellationRequested();

            using (var sourceStream = new FileStream(savePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var destinationStream = new FileStream(decryptDestination, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await _encryptionService.DecryptAsync(sourceStream, destinationStream, Password!);
            }

            FinishProgress();
            CurrentState = OperationState.Completed;
            BannerKind = NotificationKind.Success;
            BannerMessage = "Decryption complete.";
            _lastOutputPath = decryptDestination;
            _recentFilesService.Add(savePath, RecentKind);
            _settingsService.Update(settings => settings.LastOpenFolder = Path.GetDirectoryName(decryptDestination));
            _notificationService.Show(BannerMessage, NotificationKind.Success);
            _clipboardService.ScheduleClear(_settingsService.Current.ClipboardAutoWipeSeconds);
            RefreshRecentFiles();
        }
        catch (OperationCanceledException)
        {
            FinishProgress();
            BannerKind = NotificationKind.Warning;
            BannerMessage = "Decryption cancelled.";
            CurrentState = OperationState.Idle;
            DeletePartial(decryptDestination);
        }
        catch (CryptographicException)
        {
            FinishProgress();
            BannerKind = NotificationKind.Error;
            BannerMessage = "Decryption failed. Wrong password or corrupted file.";
            CurrentState = OperationState.Faulted;
            DeletePartial(decryptDestination);
        }
        catch (Exception ex)
        {
            FinishProgress();
            BannerKind = NotificationKind.Error;
            BannerMessage = $"An error occurred: {ex.Message}";
            CurrentState = OperationState.Faulted;
            DeletePartial(decryptDestination);
        }
        finally
        {
            if (Password is not null)
                CryptographicOperations.ZeroMemory(Password);
            Password = null;
            IsDecrypting = false;
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    [RelayCommand(CanExecute = nameof(IsDecrypting))]
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

    private void SetFile(string path)
    {
        ResetTransientState(keepSelection: false);

        if (!File.Exists(path))
        {
            BannerKind = NotificationKind.Error;
            BannerMessage = "Unable to read the selected file. It may have been moved or deleted.";
            CurrentState = OperationState.Faulted;
            return;
        }

        if (!Path.GetExtension(path).Equals(".enc", StringComparison.OrdinalIgnoreCase))
        {
            BannerKind = NotificationKind.Error;
            BannerMessage = $"Cannot decrypt {Path.GetFileName(path)} because it is not an .enc file.";
            CurrentState = OperationState.Faulted;
            return;
        }

        SelectedFiles.Clear();
        SelectedFiles.Add(new SelectedFileItem { FullPath = path });
        _settingsService.Update(settings => settings.LastOpenFolder = Path.GetDirectoryName(path));
        OnPropertyChanged(nameof(HasFile));
        DecryptCommand.NotifyCanExecuteChanged();
    }

    private void RefreshRecentFiles()
    {
        RecentFiles = new ObservableCollection<RecentFileEntry>(_recentFilesService.Get(RecentKind));
    }

    private void FinishProgress()
    {
        ShowProgress = false;
        ProgressValue = 0;
        StatusMessage = string.Empty;
    }

    private void ResetTransientState(bool keepSelection)
    {
        CurrentState = OperationState.Idle;
        FinishProgress();
        BannerMessage = null;
        if (!keepSelection)
            SelectedFiles.Clear();
    }

    private static void DeletePartial(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            File.Delete(path);
    }

    private static async Task<string> ExtractFileExtensionAsync(string encryptedFile)
    {
        await using var source = File.OpenRead(encryptedFile);
        source.Position = CryptoConstants.SaltSize;

        byte[] lengthBuffer = new byte[sizeof(int)];
        int bytesRead = await source.ReadAsync(lengthBuffer);
        if (bytesRead < sizeof(int))
            return string.Empty;

        int extensionLength = BitConverter.ToInt32(lengthBuffer);
        if (extensionLength <= 0 || extensionLength > 256)
            return string.Empty;

        byte[] extensionBytes = new byte[extensionLength];
        await source.ReadExactlyAsync(extensionBytes);
        return System.Text.Encoding.UTF8.GetString(extensionBytes);
    }
}
