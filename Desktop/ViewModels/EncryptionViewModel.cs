using Artemis.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Artemis.Desktop.Services;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

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
    private string? _selectedFileName;
    [ObservableProperty]
    private char[]? _passwordBuffer;
    [ObservableProperty]
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
    private bool _isEncrypting;
    [ObservableProperty]
    private bool _showProgress;
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
    [RelayCommand]
    private async Task SelectFile()
    {
        // Clearing previous errors
        ErrorMessage = null;
        PasswordError = null;
        StatusMessage = null;

        // Getting the file path
        IReadOnlyList<string>? path = await _filePickerService.OpenFileAsync("Select file to encrypt");

        // Safe return for canceled operation or empty path
        if (path is null || path.Count == 0)
            return;

        // Storing the file details
        SelectedFilePath = [.. path];
        SelectedFileName = Path.GetFileName(path[0]);
    }


    /// <summary>
    /// Perform the encryption task.
    /// </summary>
    /// <returns>The encrypted file saved to a desired location</returns>
    [RelayCommand]
    private async Task Encrypt()
    {
        // If guarding null or errant inputs
        if (SelectedFilePath is null)
        {
            ErrorMessage = "No file selected!";
            return;
        }
        if (PasswordBuffer is null || PasswordBuffer.Length == 0)
        {
            PasswordError = "Password cannot be empty!";
            return;
        }
        if (!PasswordBuffer.SequenceEqual(PasswordConfirmBuffer ?? []))
        {
            PasswordError = "Passwords do not match!";
            return;
        }
        // Clearing previous errors
        ErrorMessage = null;
        PasswordError = null;

        // Assigning a local Password variable - prevents another thread from nulling out the property
        char[] passwordChars = (char[])PasswordBuffer.Clone();
        // Encryption Process
        IsEncrypting = true;
        try
        {
            StatusMessage = "Reading file...";
            ProgressValue = 10;
            byte[] fileBytes = await File.ReadAllBytesAsync(SelectedFilePath[0]);

            StatusMessage = "Deriving encryption key...";
            ProgressValue = 15;
            var encryptedBytes = await _encryptionService.EncryptAsync(fileBytes, passwordChars);

            ProgressValue = 80;
            StatusMessage = "Saving encrypted file...";
            var encryptedFile = GenerateOutput(SelectedFileName);
            var savePath = await _filePickerService.SaveFileAsync(encryptedFile, "Save your file");

            if (string.IsNullOrWhiteSpace(savePath))
            {
                StatusMessage = "Encryption process cancelled.";
                ProgressValue = 0;
                return;
            }

            // Checking for similar encrypted file
            if (File.Exists(savePath))
            {
                bool decision = await _dialogService.ShowConfirmationAsync("A similar file exists. Do you wish to override existing file?");
                if (!decision)
                {
                    StatusMessage = "Encryption process cancelled.";
                    ProgressValue = 0;
                    return;
                }
            }

            // Save the encrypted output
            await File.WriteAllBytesAsync(savePath, encryptedBytes);
            ProgressValue = 100;
            StatusMessage = "Encryption complete!";
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
            Array.Clear(passwordChars, 0, passwordChars.Length);
            IsEncrypting = false;
        }
    }
    #endregion

    #region Helper Methods
    private static string GenerateOutput(string? file)
    {
        return $"{file}.enc";
    }
    #endregion
}