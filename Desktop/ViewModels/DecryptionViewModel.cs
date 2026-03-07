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

public partial class DecryptionViewModel : ViewModelBase
{
    #region Private Fields
    private readonly IEncryptionService _encryptionService;
    private readonly IFilePickerService _filePickerService;
    private readonly IDialogService _dialogService;
    private byte[]? _selectedFileBytes;
    #endregion

    #region Properties
    [ObservableProperty]
    private string? _selectedFilePath;
    [ObservableProperty]
    private string? _selectedFileName;
    
    [ObservableProperty]
    private char[]? _passwordBuffer;
    
    [ObservableProperty]
    private string? _errorMessage;
    [ObservableProperty]
    private string? _statusMessage;
    [ObservableProperty]
    private double _progressValue;
    [ObservableProperty]
    private bool _isDecrypting;
    [ObservableProperty]
    private bool _showProgress;
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
    [RelayCommand]
    private async Task SelectFile()
    {
        ErrorMessage = null;
        StatusMessage = null;

        IReadOnlyList<string>? path = await _filePickerService.OpenFileAsync("Select file to decrypt");

        if (path is null || path.Count == 0)
            return;

        _selectedFileBytes = null;
        SelectedFilePath = null;
        SelectedFileName = null;

        SelectedFilePath = path[0];
        SelectedFileName = Path.GetFileName(path[0]);

        if (!Path.GetExtension(SelectedFileName).Equals(".enc", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = $"Cannot Decrypt {SelectedFileName} because it is the wrong file type.";
            SelectedFilePath = null;
            SelectedFileName = null;
            return;
        }
        try
        {
            _selectedFileBytes = await File.ReadAllBytesAsync(SelectedFilePath);
        }
        catch
        {
            _selectedFileBytes = null;
            SelectedFilePath = null;
            SelectedFileName = null;
            ErrorMessage = "Unable to read the selected file. It may have been moved, deleted, or is in use.";
        }
    }

    [RelayCommand]
    private async Task Decrypt()
    {
        if (_selectedFileBytes is null)
        {
            ErrorMessage = "No file selected!";
            return;
        }
        if (PasswordBuffer is null || PasswordBuffer.Length == 0)
        {
            ErrorMessage = "Password cannot be empty!";
            return;
        }
        
        ErrorMessage = null;
        IsDecrypting = true;

        try
        {
            StatusMessage = "Deriving decryption key...";
            ProgressValue = 15;

            var decryptedBytes = await _encryptionService.DecryptAsync(_selectedFileBytes, PasswordBuffer);
            ProgressValue = 80;
            StatusMessage = "Saving decrypted file...";

            string? decryptedFileName = Path.GetFileNameWithoutExtension(SelectedFileName);
            bool isSaveFile = await _dialogService.ShowConfirmationAsync("Save file to location?");
            if (!isSaveFile)
            {
                StatusMessage = "Decryption process cancelled.";
                ProgressValue = 0;
                return;
            }

            using (var stream = new MemoryStream(decryptedBytes))
            {
                var fileType = FileTypeValidator.GetFileType(stream);
                string extension = fileType.Extension;
                var suggestedFileName = $"{decryptedFileName}{extension}";
                var savePath = await _filePickerService.SaveFileAsync(suggestedFileName, "Save your file");

                if (string.IsNullOrWhiteSpace(savePath))
                {
                    StatusMessage = "Decryption process cancelled.";
                    ProgressValue = 0;
                    return;
                }

                if (File.Exists(savePath))
                {
                    var overwrite = await _dialogService.ShowConfirmationAsync(
                        $"The file '{Path.GetFileName(savePath)}' already exists. Overwrite it?");
                    if (!overwrite)
                    {
                        StatusMessage = "Decryption process cancelled.";
                        ProgressValue = 0;
                        return;
                    }
                }
                await File.WriteAllBytesAsync(savePath, decryptedBytes);
                StatusMessage = "Decryption complete!";
                ProgressValue = 100;
            }

        }
        catch (CryptographicException)
        {
            ErrorMessage = "Decryption failed. Wrong password or corrupted file.";
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage = "You do not have permissions to open this file.";
        }
        catch (IOException)
        {
            ErrorMessage = "File is in use by another program. Please close it and try again.";
        }
        catch (Exception e)
        {
            ErrorMessage = e.Message;
        }
        finally
        {
            if (PasswordBuffer is not null) Array.Clear(PasswordBuffer, 0, PasswordBuffer.Length);
            IsDecrypting = false;
        }
    }
    #endregion
}
