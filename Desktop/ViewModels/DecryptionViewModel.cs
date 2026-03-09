using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Artemis.Core.Interfaces;
using Artemis.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileTypeChecker;

namespace Artemis.Desktop.ViewModels;

public enum OperationState { Idle, Processing, Saving, Completed, Faulted }

public partial class DecryptionViewModel : ViewModelBase
{
    #region Private Fields
    private readonly IEncryptionService _encryptionService;
    private readonly IFilePickerService _filePickerService;
    private readonly IDialogService _dialogService;
    #endregion

    #region Properties
    [ObservableProperty]
    private string? _selectedFilePath;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DecryptCommand))]
    private string? _selectedFileName;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DecryptCommand))]
    private char[]? _passwordBuffer;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DecryptCommand))]
    private byte[]? _selectedFileBytes;

    [ObservableProperty]
    private string? _errorMessage;
    [ObservableProperty]
    private string? _statusMessage;
    [ObservableProperty]
    private double _progressValue;
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DecryptCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectFileCommand))]
    private bool _isDecrypting;
    
    [ObservableProperty]
    private bool _showProgress;

    [ObservableProperty]
    private OperationState _currentState = OperationState.Idle;

    private bool CanDecrypt => !IsDecrypting && SelectedFileBytes is not null && PasswordBuffer?.Length > 0;
    private bool CanSelectFile => !IsDecrypting;
    #endregion

    #region Constructor
    public DecryptionViewModel(IEncryptionService encryptionService, IFilePickerService filePickerService, IDialogService dialogService)
    {
        _encryptionService = encryptionService;
        _dialogService = dialogService;
        _filePickerService = filePickerService;
    }
    #endregion

    #region Commands
    [RelayCommand(CanExecute = nameof(CanSelectFile))]
    private async Task SelectFile()
    {
        ResetState();

        IReadOnlyList<string>? path = await _filePickerService.OpenFileAsync("Select file to decrypt");

        if (path is null || path.Count == 0)
            return;

        SelectedFileBytes = null;
        SelectedFilePath = null;
        SelectedFileName = null;

        SelectedFilePath = path[0];
        SelectedFileName = Path.GetFileName(path[0]);

        if (!Path.GetExtension(SelectedFileName).Equals(".enc", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = $"Cannot Decrypt {SelectedFileName} because it is the wrong file type.";
            SelectedFilePath = null;
            SelectedFileName = null;
            CurrentState = OperationState.Faulted;
            return;
        }
        try
        {
            SelectedFileBytes = await File.ReadAllBytesAsync(SelectedFilePath);
        }
        catch
        {
            SelectedFileBytes = null;
            SelectedFilePath = null;
            SelectedFileName = null;
            ErrorMessage = "Unable to read the selected file. It may have been moved, deleted, or is in use.";
            CurrentState = OperationState.Faulted;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDecrypt))]
    private async Task Decrypt()
    {
        ErrorMessage = null;

        if (SelectedFileBytes is null || PasswordBuffer is null || PasswordBuffer.Length == 0)
        {
            ErrorMessage = SelectedFileBytes is null ? "No file selected!" : "Password cannot be empty!";
            CurrentState = OperationState.Faulted;
            return;
        }

        ErrorMessage = null;
        IsDecrypting = true;
        ShowProgress = true;
        CurrentState = OperationState.Processing;

        byte[]? decryptedBytes = null;
        try
        {
            StatusMessage = "Deriving decryption key...";
            ProgressValue = 15;

            decryptedBytes = await _encryptionService.DecryptAsync(SelectedFileBytes, PasswordBuffer);

            ProgressValue = 50;
            StatusMessage = "Preparing to save...";
            CurrentState = OperationState.Saving;

            string? decryptedFileName = Path.GetFileNameWithoutExtension(SelectedFileName);
            bool isSaveFile = await _dialogService.ShowConfirmationAsync("Save file to location?");
            if (!isSaveFile)
            {
                StatusMessage = "Decryption process cancelled.";
                CurrentState = OperationState.Idle;
                return;
            }

            string extension;
            using (var stream = new MemoryStream(decryptedBytes))
            {
                var fileType = FileTypeValidator.GetFileType(stream);
                extension = fileType.Extension;
            }

            var suggestedFileName = $"{decryptedFileName}{extension}";
            var savePath = await _filePickerService.SaveFileAsync(suggestedFileName, "Save your file");

            if (string.IsNullOrWhiteSpace(savePath))
            {
                StatusMessage = "Decryption process cancelled.";
                CurrentState = OperationState.Idle;
                return;
            }

            if (File.Exists(savePath))
            {
                var overwrite = await _dialogService.ShowConfirmationAsync(
                    $"The file '{Path.GetFileName(savePath)}' already exists. Overwrite it?");
                if (!overwrite)
                {
                    StatusMessage = "Decryption process cancelled.";
                    CurrentState = OperationState.Idle;
                    return;
                }
            }

            StatusMessage = "Writing file to disk...";
            ProgressValue = 85;
            await File.WriteAllBytesAsync(savePath, decryptedBytes);

            StatusMessage = "Decryption complete!";
            ProgressValue = 100;
            CurrentState = OperationState.Completed;
        }
        catch (CryptographicException)
        {
            ErrorMessage = "Decryption failed. Wrong password or corrupted file.";
            CurrentState = OperationState.Faulted;
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage = "You do not have permissions to open this file.";
            CurrentState = OperationState.Faulted;
        }
        catch (IOException)
        {
            ErrorMessage = "File is in use by another program. Please close it and try again.";
            CurrentState = OperationState.Faulted;
        }
        catch (Exception e)
        {
            ErrorMessage = e.Message;
            CurrentState = OperationState.Faulted;
        }
        finally
        {
            if (decryptedBytes is not null) Array.Clear(decryptedBytes, 0, decryptedBytes.Length);
            if (SelectedFileBytes is not null) Array.Clear(SelectedFileBytes, 0, SelectedFileBytes.Length);
            if (PasswordBuffer is not null) Array.Clear(PasswordBuffer, 0, PasswordBuffer.Length);
            
            IsDecrypting = false;
            SelectedFileBytes = null;
        }
    }
    #endregion

    #region Helper Methods
    private void ResetState()
    {
        CurrentState = OperationState.Idle;
        ShowProgress = false;
        ProgressValue = 0;
        StatusMessage = string.Empty;
        ErrorMessage = null;
    }
    #endregion
}
