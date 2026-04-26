using Artemis.Core.Interfaces;
using Artemis.Desktop.Models;
using Artemis.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Artemis.Desktop.ViewModels;

public partial class DecryptionViewModel : ViewModelBase
{
    private readonly IEncryptionService _encryptionService;
    private readonly IFilePickerService _filePickerService;
    private readonly IDialogService _dialogService;


    [ObservableProperty]
    private string? _selectedFilePath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DecryptCommand))]
    private string? _selectedFileName;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DecryptCommand))]
    private SecureString? _password;

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

    private bool CanDecrypt => !IsDecrypting
        && !string.IsNullOrWhiteSpace(SelectedFilePath)
        && Password.Length > 0;

    private bool CanSelectFile => !IsDecrypting;

    public DecryptionViewModel(IEncryptionService encryptionService, IFilePickerService filePickerService, IDialogService dialogService)
    {
        _encryptionService = encryptionService;
        _dialogService = dialogService;
        _filePickerService = filePickerService;
    }

    [RelayCommand(CanExecute = nameof(CanSelectFile))]
    private async Task SelectFile()
    {
        ResetState();

        IReadOnlyList<string>? path = await _filePickerService.OpenFileAsync("Select file to decrypt");

        if (path is null || path.Count == 0) return;

        SelectedFilePath = path[0];
        SelectedFileName = Path.GetFileName(path[0]);

        if (!Path.GetExtension(SelectedFileName).Equals(".enc", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = $"Cannot Decrypt {SelectedFileName} because it is the wrong file type.";
            SelectedFilePath = null;
            SelectedFileName = null;
            CurrentState = OperationState.Faulted;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDecrypt))]
    private async Task Decrypt()
    {
        // 1. Setup Initial UI State
        ErrorMessage = null;
        IsDecrypting = true;
        ShowProgress = true;
        CurrentState = OperationState.Processing;

        string? savePath = null;

        try
        {
            // 2. Determine the suggested save name. 
            // [YOUR TURN: Since we can't look at the file bytes in memory anymore to guess the extension, 
            // how would you manipulate the SelectedFileName string to remove the ".enc" at the end?]

            string suggestedFileName = GenerateOutput(savePath);


            // 3. Ask the user where to save it
            savePath = await _filePickerService.SaveFileAsync(suggestedFileName, "Save Decrypted File");

            // [YOUR TURN: What should the UI do if savePath is null or whitespace (meaning the user hit cancel)?]
            if (string.IsNullOrWhiteSpace(savePath))
            {
                CurrentState = OperationState.Faulted;
                return;
            }

            // [YOUR TURN: What should happen if File.Exists(savePath) is true?]
            if (File.Exists(savePath))
            {
                bool decision = await _dialogService.ShowConfirmationAsync($"The file {Path.GetFileName(savePath)} already exists. Overwrite?");
                if (decision)
                {
                    StatusMessage = "Decrypting file...";
                    ProgressValue = 50; // Indeterminate progress for streaming 
                }
                else
                {

                    StatusMessage = "Encryption process cancelled.";
                    CurrentState = OperationState.Idle;
                    return;
                }
            }

            // 4. Open the Streams and execute!
            // [YOUR TURN: Open a FileStream for reading the SelectedFilePath]
            // [YOUR TURN: Open a FileStream for writing to the savePath]
            // [YOUR TURN: Pass both streams and the Password to _encryptionService.DecryptAsync]

            using (var sourceStream = new FileStream(suggestedFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var destinationStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await _encryptionService.DecryptAsync(sourceStream, destinationStream, Password, relativeFilePath);
            }

            StatusMessage = "Decryption complete!";
            ProgressValue = 100;
            CurrentState = OperationState.Completed;
        }
        catch (CryptographicException)
        {
            ErrorMessage = "Decryption failed. Wrong password or corrupted file.";
            CurrentState = OperationState.Faulted;

            // 5. Cleanup the corrupted file
            // [YOUR TURN: The user typed the wrong password, but the destination stream might have written 
            // a few chunks of garbage to the hard drive before crashing. Write the code to delete the savePath file if it exists.]

            if (Path.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
        catch (Exception e)
        {
            ErrorMessage = $"An error occurred: {e.Message}";
            CurrentState = OperationState.Faulted;

            // [YOUR TURN: Ensure you also clean up the file in this general catch block]
            ResetState();
        }
        finally
        {
            IsDecrypting = false;
            Password.Clear();
        }
    }
    #region
    private void ResetState()
    {
        CurrentState = OperationState.Idle;
        ShowProgress = false;
        ProgressValue = 0;
        StatusMessage = string.Empty;
        ErrorMessage = null;
    }

    private static string GenerateOutput(string filePath)
    {
        return $"{filePath}.{Path.ChangeExtension(filePath, Path.GetExtension(filePath))}";
    }

    #endregion
}