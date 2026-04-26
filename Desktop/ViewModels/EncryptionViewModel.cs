using Artemis.Core.Interfaces;
using Artemis.Desktop.Models;
using Artemis.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Threading.Tasks;

namespace Artemis.Desktop.ViewModels;


public partial class EncryptionViewModel : ViewModelBase
{
    #region Private Fields
    private readonly IEncryptionService _encryptionService;
    private readonly IFilePickerService _filePickerService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private List<string>? _selectedFilePaths;

    [ObservableProperty]
    private string? _selectedFileName;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EncryptCommand))]
    private SecureString? _password;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EncryptCommand))]
    private SecureString? _passwordConfirm;

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
    && SelectedFilePaths is not null
    && Password.Length > 0
    && Password.Equals(PasswordConfirm);
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
        ResetProgressBarState();

        // Getting the file path
        IReadOnlyList<string>? path = await _filePickerService.OpenFileAsync("Select file to encrypt");

        // Safe return for canceled operation or empty path
        if (path is null || path.Count == 0)
            return;

        try
        {
            // Storing the file details
            SelectedFilePaths = [.. path];
            SelectedFileName = Path.GetFileName(path[0]);
        }
        catch
        {
            SelectedFilePaths = null;
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
        ErrorMessage = null;
        PasswordError = null;
        IsEncrypting = true;
        ShowProgress = true;
        CurrentState = OperationState.Processing;

        string? currentSavePath = null;

        // Encryption Process
        try
        {
            double progresStep = 100.0 / SelectedFilePaths.Count;
            double currentProgress = 0;

            foreach (var filePath in SelectedFilePaths)
            {
                string fileName = Path.GetFileName(filePath);
                currentSavePath = GenerateOutput(fileName);
                string relativeFilePath = Path.GetRelativePath(filePath, currentSavePath);
                // Checking for similar encrypted file to prevent overwriting
                if (File.Exists(currentSavePath))
                {
                    bool decision = await _dialogService.ShowConfirmationAsync($"The file {Path.GetFileName(currentSavePath)} already exists. Overwrite?");
                    if (!decision)
                    {
                        continue;
                    }
                    else
                    {

                        StatusMessage = "Encryption process cancelled.";
                        CurrentState = OperationState.Idle;
                        return;
                    }
                }

                using (var sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var destinationStream = new FileStream(currentSavePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await _encryptionService.EncryptAsync(sourceStream, destinationStream, Password, relativeFilePath);
                }

                currentProgress += progresStep;
                ProgressValue = currentProgress;
            }

            // Save the encrypted output
            ProgressValue = 100;
            StatusMessage = "Encryption complete!";
            CurrentState = OperationState.Completed;
        }
        catch (Exception e)
        {
            ErrorMessage = $"Encryption failed: {e.Message}";
            CurrentState = OperationState.Faulted;

            // Critical Cleanup: If encryption fails halfway, delete the corrupted destination file.
            if (!string.IsNullOrWhiteSpace(currentSavePath) && File.Exists(currentSavePath))
            {
                File.Delete(currentSavePath);
            }
        }
        finally
        {
            ResetEncryptionState();
        }

    }
    #endregion

    #region Helper Methods
    private static string GenerateOutput(string filePath)
    {
        return $"{filePath}.{Path.ChangeExtension(filePath, "enc")}";
    }

    private void ResetProgressBarState()
    {
        CurrentState = OperationState.Idle;
        ShowProgress = false;
        ProgressValue = 0;
        StatusMessage = string.Empty;
        ErrorMessage = null;
    }

    private void ResetEncryptionState()
    {
        IsEncrypting = false;
        Password.Clear();
        PasswordConfirm.Clear();
    }
    #endregion
}