using Artemis.Core.Cryptography;
using Artemis.Core.Interfaces;
using Artemis.Desktop.Models;
using Artemis.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.IO;
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
    private byte[]? _password;

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
        && Password is not null
        && Password.Length > 0;

    private bool CanSelectFile => !IsDecrypting;

    public DecryptionViewModel(IEncryptionService encryptionService, IFilePickerService filePickerService, IDialogService dialogService)
    {
        _encryptionService = encryptionService;
        _dialogService = dialogService;
        _filePickerService = filePickerService;
    }

    [RelayCommand(CanExecute = nameof(CanSelectFile))]
    private async Task<string> SelectFile()
    {
        ResetState();

        IReadOnlyList<string>? path = await _filePickerService.OpenFileAsync("Select file to decrypt");

        if (path is null || path.Count == 0) return string.Empty;

        SelectedFilePath = path[0];
        SelectedFileName = Path.GetFileName(path[0]);

        if (!Path.GetExtension(SelectedFileName).Equals(".enc", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = $"Cannot Decrypt {SelectedFileName} because it is the wrong file type.";
            SelectedFilePath = null;
            SelectedFileName = null;
            CurrentState = OperationState.Faulted;
            return string.Empty;
        }

        return SelectedFilePath ?? string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanDecrypt))]
    private async Task Decrypt()
    {
        // 1. Setup Initial UI State
        ErrorMessage = null;
        IsDecrypting = true;
        ShowProgress = true;
        CurrentState = OperationState.Processing;

        string? savePath = await SelectFile();
        string decryptDestination = string.Empty;
        try
        {
            // 2. Determine the suggested save name. 
            string? extension = await ExtractFileExtensionAsync(savePath);
            string suggestedFileName = await GenerateOutput(savePath, extension);

            // 3. Ask the user where to save it
            decryptDestination = await _filePickerService.SaveFileAsync(suggestedFileName, "Save Decrypted File");

            if (string.IsNullOrWhiteSpace(decryptDestination))
            {
                StatusMessage = "Decryption process cancelled.";
                CurrentState = OperationState.Idle;
                return;
            }


            if (File.Exists(decryptDestination))
            {
                bool decision = await _dialogService.ShowConfirmationAsync($"The file {Path.GetFileName(savePath)} already exists. Overwrite?");
                if (decision)
                {
                    StatusMessage = "Decrypting file...";
                    ProgressValue = 50;
                }
                else
                {

                    StatusMessage = "Decryption process cancelled.";
                    CurrentState = OperationState.Idle;
                    return;
                }
            }

            // 4. Open the Streams and execute!
            using (var sourceStream = new FileStream(savePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var destinationStream = new FileStream(decryptDestination, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await _encryptionService.DecryptAsync(sourceStream, destinationStream, Password, extension);
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
            if (Path.Exists(decryptDestination))
            {
                File.Delete(decryptDestination);
            }
        }
        catch (Exception e)
        {
            ErrorMessage = $"An error occurred: {e.Message}";
            CurrentState = OperationState.Faulted;

            if (Path.Exists(decryptDestination))
            {
                File.Delete(decryptDestination);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(Password);
            IsDecrypting = false;
            ResetState();
            Password = null;
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

    private async Task<string> GenerateOutput(string filePath, string extension)
    {
        string fileName = Path.GetFileName(filePath);
        string fileOutput = Path.ChangeExtension(fileName, extension);
        return fileOutput;
    }

    private async Task<string> ExtractFileExtensionAsync(string encryptedFile)
    {
        using var source = File.OpenRead(encryptedFile);
        source.Position = CryptoConstants.SaltSize;

        var lengthBuffer = new byte[sizeof(int)];
        int bytesRead = await source.ReadAsync(lengthBuffer);

        if (bytesRead < sizeof(int)) return string.Empty;

        int extensionLength = BitConverter.ToInt32(lengthBuffer);

        if (extensionLength <= 0 || extensionLength > 256) return string.Empty;

        var extensionBytes = new byte[extensionLength];
        await source.ReadExactlyAsync(extensionBytes);

        return System.Text.Encoding.UTF8.GetString(extensionBytes);
    }
    #endregion
}