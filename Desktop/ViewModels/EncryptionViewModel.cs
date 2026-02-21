using Artemis.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Artemis.Desktop.Services;
using System.IO;
using System.Threading.Tasks;

namespace Artemis.Desktop.ViewModels;


public partial class EncryptViewModel : ViewModelBase
{
    #region Private Fields
    private readonly IEncryptionService _encryptionService;
    private readonly IFilePickerService _filePickerService;
    private byte[]? _selectedFileBytes;

    [ObservableProperty]
    private string? _selectedFilePath;
    [ObservableProperty]
    private string? _selectedFileName;
    [ObservableProperty]
    private string? _password;
    [ObservableProperty]
    private string? _passwordConfirm;
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
    public EncryptViewModel(IEncryptionService encryptionService, IFilePickerService filePickerService)
    {
        _encryptionService = encryptionService;
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
        PasswordError = null;
        StatusMessage = null;

        // Getting the file path
        var path = await _filePickerService.OpenFileAsync("Select file to encrypt");

        // Safe return for canceled operation or empty path
        if (string.IsNullOrWhiteSpace(path))
            return;

        // Storing the file details
        byte[] fileBytes = File.ReadAllBytes(path);
        SelectedFilePath = path;
        SelectedFileName = Path.GetFileName(path);
        _selectedFileBytes = fileBytes;


    }

    private async Task Encrypt()
    {
        // Clearing previous errors
        ErrorMessage = null;
        PasswordError = null;
        Passwor
    }
    #endregion

    #region Methods

    #endregion
}