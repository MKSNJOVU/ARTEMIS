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
    private string? _password;
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
    /// <summary>
    /// Command to allow users to select a file
    /// </summary>
    /// <returns>The file name, file path and file bytes of a selected file.</returns>
    [RelayCommand]
    private async Task SelectFile()
    {
        // Clearing previous errors
        ErrorMessage = null;
        StatusMessage = null;

        // Getting the file path
        IReadOnlyList<string>? path = await _filePickerService.OpenFileAsync("Select file to decrypt");

        // Safe return for canceled operation or empty path
        if (path is null || path.Count == 0)
            return;

        // Storing the file details
        SelectedFilePath = path[0];
        SelectedFileName = Path.GetFileName(path[0]);

        if (!Path.GetExtension(SelectedFileName).Equals(".enc", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = $"Cannot Decrypt {SelectedFileName} because it is the wrong file type.";
            return;
        }
        try
        {
            _selectedFileBytes = await File.ReadAllBytesAsync(SelectedFilePath);
        }
        catch
        {
            // Clear selection state on failure and surface a user-friendly error
            _selectedFileBytes = null;
            SelectedFilePath = null;
            SelectedFileName = null;
            StatusMessage = null;
            ErrorMessage = "Unable to read the selected file. It may have been moved, deleted, or is in use.";
        }
    }

    /// <summary>
    /// Decrypt a selected file to its original type.
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task Decrypt()
    {
        // If guarding null or errant inputs
        if (_selectedFileBytes is null)
        {
            ErrorMessage = "No file selected!";
            return;
        }
        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Password cannot be empty!";
            return;
        }
        // Clear previous errors
        ErrorMessage = null;

        IsDecrypting = true;

        try
        {
            StatusMessage = "Deriving decryption key...";
            ProgressValue = 15;

            var decryptedBytes = await _encryptionService.DecryptAsync(_selectedFileBytes, Password);
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

            // Writing a file to its original type
            using (var stream = new MemoryStream(decryptedBytes))
            {
                var fileType = FileTypeValidator.GetFileType(stream);
                string extension = fileType.Extension; // e.g., ".docx", ".jpeg"
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
        finally
        {
            IsDecrypting = false;
        }
    }

    #endregion
}