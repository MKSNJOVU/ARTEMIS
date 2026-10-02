using System.Reflection;
using System.Threading.Tasks;
using Artemis.Desktop.Models;
using Artemis.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Artemis.Desktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IRecentFilesService _recentFilesService;
    private readonly IFilePickerService _filePickerService;
    private readonly INotificationService _notificationService;

    [ObservableProperty]
    private ThemeMode _theme;

    [ObservableProperty]
    private string? _defaultOutputFolder;

    [ObservableProperty]
    private bool _wipeClipboardOnExit;

    [ObservableProperty]
    private decimal _clipboardAutoWipeSeconds;

    [ObservableProperty]
    private bool _rememberRecentFiles;

    public ThemeMode[] ThemeOptions { get; } = [ThemeMode.Dark, ThemeMode.Light, ThemeMode.System];

    public string AppVersion { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

    public string Disclaimer { get; } =
        "This project was built for educational purposes. Encrypting any document is entirely at your own risk. The author is not responsible for data loss, file corruption, or security breaches.";

    public SettingsViewModel(
        ISettingsService settingsService,
        IRecentFilesService recentFilesService,
        IFilePickerService filePickerService,
        INotificationService notificationService)
    {
        _settingsService = settingsService;
        _recentFilesService = recentFilesService;
        _filePickerService = filePickerService;
        _notificationService = notificationService;

        Theme = _settingsService.Current.Theme;
        DefaultOutputFolder = _settingsService.Current.DefaultOutputFolder;
        WipeClipboardOnExit = _settingsService.Current.WipeClipboardOnExit;
        ClipboardAutoWipeSeconds = _settingsService.Current.ClipboardAutoWipeSeconds;
        RememberRecentFiles = _settingsService.Current.RememberRecentFiles;
    }

    [RelayCommand]
    private async Task BrowseOutputFolder()
    {
        string? folder = await _filePickerService.OpenFolderAsync("Choose default output folder", DefaultOutputFolder);
        if (!string.IsNullOrWhiteSpace(folder))
            DefaultOutputFolder = folder;
    }

    [RelayCommand]
    private void ClearOutputFolder()
    {
        DefaultOutputFolder = null;
    }

    [RelayCommand]
    private void ClearRecentFiles()
    {
        _recentFilesService.Clear();
        _notificationService.Show("Recent files cleared.", NotificationKind.Info);
    }

    partial void OnThemeChanged(ThemeMode value)
    {
        _settingsService.Update(settings => settings.Theme = value);
        ThemeService.Apply(value);
    }

    partial void OnDefaultOutputFolderChanged(string? value)
    {
        _settingsService.Update(settings => settings.DefaultOutputFolder = value);
    }

    partial void OnWipeClipboardOnExitChanged(bool value)
    {
        _settingsService.Update(settings => settings.WipeClipboardOnExit = value);
    }

    partial void OnClipboardAutoWipeSecondsChanged(decimal value)
    {
        int normalized = value < 0 ? 0 : (int)value;
        if (normalized != value)
        {
            ClipboardAutoWipeSeconds = normalized;
            return;
        }

        _settingsService.Update(settings => settings.ClipboardAutoWipeSeconds = normalized);
    }

    partial void OnRememberRecentFilesChanged(bool value)
    {
        _settingsService.Update(settings => settings.RememberRecentFiles = value);
    }
}
