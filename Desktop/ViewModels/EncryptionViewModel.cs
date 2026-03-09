using Artemis.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Artemis.Desktop.Services;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;
using Artemis.Desktop.Models;

namespace Artemis.Desktop.ViewModels;


public partial class EncryptionViewModel : ViewModelBase
{
    #region Private Fields
    private readonly IEncryptionService _encryptionService;
    private readonly IFilePickerService _filePickerService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private List<string>? _selectedFilePath;
    [ObservableProperty]
    private byte[]? _selectedFileBytes;
    [ObservableProperty]
    private string? _selectedFileName;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EncryptCommand))]
    private char[]? _passwordBuffer;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EncryptCommand))]
    private char[]? _passwordConfirmBuffer;
    [ObservableProperty]
    private string? _passwordError;
    [ObservableProperty]
    private string? _errorMessage;
    [ObservableProperty]
    private string? _statusMessage;
    [ObservableProperty]
    private double _progressValue;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EncryptCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectFileCommand))]
    private bool _isEncrypting;
    [ObservableProperty]
    private bool _showProgress;

    [ObservableProperty]
    private OperationState _currentState = OperationState.Idle;
    private bool CanEncrypt => !IsEncrypting
    && SelectedFileBytes is not null
    && PasswordBuffer?.Length > 0
    && PasswordBuffer.SequenceEqual(PasswordConfirmBuffer ?? []);
    private bool CanSelectFile => !IsEncrypting;
    #endregion

    #region Constructor
    public EncryptionViewModel(IEncryptionService encryptionService, IFilePickerService filePickerService, IDialogService dialogService)
    {
        _encryptionService = encryptionService;
        _filePickerService = filePickerService;
        _dialogService = dialogService;
    }
    #endregion

    #region Commands
    /// <summary>
    /// Command to allow users to select a file
    /// </summary>
    /// <returns>The file name, file path and file bytes of a selected file.</returns>
    [RelayCommand(CanExecute = nameof(CanSelectFile))]
    private async Task SelectFile()
    {
        ResetState();

        // Clearing previous errors
        ErrorMessage = null;
        PasswordError = null;
        StatusMessage = null;

        // Getting the file path
        IReadOnlyList<string>? path = await _filePickerService.OpenFileAsync("Select file to encrypt");

        // Safe return for canceled operation or empty path
        if (path is null || path.Count == 0)
            return;

        SelectedFileBytes = null;
        SelectedFilePath = null;
        SelectedFileName = null;

        // Storing the file details
        SelectedFilePath = [.. path];
        SelectedFileName = Path.GetFileName(path[0]);

        try
        {
            SelectedFileBytes = await File.ReadAllBytesAsync(SelectedFilePath[0]);
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


    /// <summary>
    /// Perform the encryption task.
    /// </summary>
    /// <returns>The encrypted file saved to a desired location</returns>
    [RelayCommand(CanExecute = nameof(CanEncrypt))]
    private async Task Encrypt()
    {

        // Clearing previous errors
        ErrorMessage = null;
        PasswordError = null;

        // Secure Snapshot: Capture references and clone sensitive data to avoid race conditions and memory leaks.
        char[]? passwordSnapshot = new char[PasswordBuffer!.Length];
        Array.Copy(PasswordBuffer, passwordSnapshot, PasswordBuffer.Length);
        byte[]? fileSnapshot = SelectedFileBytes;
        byte[]? encryptedBytes = null;
        // Encryption Process
        IsEncrypting = true;
        try
        {
            //Processing data
            CurrentState = OperationState.Processing;

            StatusMessage = "Deriving encryption key...";
            ProgressValue = 15;
            encryptedBytes = await _encryptionService.EncryptAsync(fileSnapshot!, passwordSnapshot);

            //Saving data
            CurrentState = OperationState.Saving;
            ProgressValue = 80;
            StatusMessage = "Saving encrypted file...";
            var encryptedFile = GenerateOutput(SelectedFileName);
            var savePath = await _filePickerService.SaveFileAsync(encryptedFile, "Save your file");

            if (string.IsNullOrWhiteSpace(savePath))
            {
                StatusMessage = "Encryption process cancelled.";
                CurrentState = OperationState.Idle;
                return;
            }

            // Checking for similar encrypted file
            if (File.Exists(savePath))
            {
                bool decision = await _dialogService.ShowConfirmationAsync("A similar file exists. Do you wish to override existing file?");
                if (!decision)
                {
                    StatusMessage = "Encryption process cancelled.";
                    CurrentState = OperationState.Idle;
                    return;
                }
            }

            // Save the encrypted output
            await File.WriteAllBytesAsync(savePath, encryptedBytes);
            ProgressValue = 100;
            StatusMessage = "Encryption complete!";
            CurrentState = OperationState.Completed;
        }
        catch (FileNotFoundException) // Handling the case of a moved file
        {
            ErrorMessage = "File no longer exists. Please select the file again.";
            SelectedFilePath = null;
            SelectedFileName = null;
            return;
        }
        catch (IOException) // Handling the case of an opened file
        {
            ErrorMessage = "File is in use by another program. Please close it and try again.";
            return;
        }

        catch (System.Exception e)
        {
            ErrorMessage = e.Message;
        }
        finally
        {
            if (passwordSnapshot is not null) Array.Clear(passwordSnapshot, 0, passwordSnapshot.Length);
            if (encryptedBytes is not null) Array.Clear(encryptedBytes, 0, encryptedBytes.Length);

            // Clear properties and confirm buffers
            if (PasswordBuffer is not null) Array.Clear(PasswordBuffer, 0, PasswordBuffer.Length);
            if (PasswordConfirmBuffer is not null) Array.Clear(PasswordConfirmBuffer, 0, PasswordConfirmBuffer.Length);
            if (SelectedFileBytes is not null) Array.Clear(SelectedFileBytes, 0, SelectedFileBytes.Length);

            IsEncrypting = false;
        }
    }
    #endregion

    #region Helper Methods
    private static string GenerateOutput(string? file)
    {
        return $"{file}.enc";
    }

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