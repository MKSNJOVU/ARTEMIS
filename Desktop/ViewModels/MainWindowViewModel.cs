using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Artemis.Desktop.Models;
using Artemis.Desktop.Services;
using Artemis.Desktop.Services.Interfaces;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Artemis.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly EncryptionViewModel _encryptionViewModel;
    private readonly DecryptionViewModel _decryptionViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly IClipboardService _clipboardService;
    private readonly INotificationService _notificationService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private ViewModelBase _currentView;

    [ObservableProperty]
    private string _currentPageTitle = "Encrypt";

    [ObservableProperty]
    private string _selectedPage = "Encrypt";

    [ObservableProperty]
    private bool _isPaneOpen = true;

    [ObservableProperty]
    private bool _useCustomChrome;

    [ObservableProperty]
    private bool _isCommandPaletteOpen;

    [ObservableProperty]
    private string _paletteQuery = string.Empty;

    [ObservableProperty]
    private CommandItem? _selectedCommand;

    public ObservableCollection<NotificationItem> Notifications { get; } = [];
    public ObservableCollection<CommandItem> FilteredCommands { get; } = [];

    public bool IsEncryptSelected => SelectedPage == "Encrypt";
    public bool IsDecryptSelected => SelectedPage == "Decrypt";
    public bool IsSettingsSelected => SelectedPage == "Settings";

    public MainWindowViewModel(
        EncryptionViewModel encryptionViewModel,
        DecryptionViewModel decryptionViewModel,
        SettingsViewModel settingsViewModel,
        IClipboardService clipboardService,
        INotificationService notificationService,
        ISettingsService settingsService)
    {
        _encryptionViewModel = encryptionViewModel;
        _decryptionViewModel = decryptionViewModel;
        _settingsViewModel = settingsViewModel;
        _clipboardService = clipboardService;
        _notificationService = notificationService;
        _settingsService = settingsService;

        UseCustomChrome = !OperatingSystem.IsLinux();
        CurrentView = _encryptionViewModel;
        _notificationService.NotificationRequested += OnNotificationRequested;
        RefreshPalette();
    }

    [RelayCommand]
    private void ShowEncrypt() => Navigate("Encrypt", "Encrypt", _encryptionViewModel);

    [RelayCommand]
    private void ShowDecrypt() => Navigate("Decrypt", "Decrypt", _decryptionViewModel);

    [RelayCommand]
    private void ShowSettings() => Navigate("Settings", "Settings", _settingsViewModel);

    [RelayCommand]
    private void TogglePane() => IsPaneOpen = !IsPaneOpen;

    [RelayCommand]
    private async Task WipeClipboard()
    {
        await _clipboardService.ClearClipboardAsync();
        _notificationService.Show("Clipboard wiped.", NotificationKind.Success);
    }

    [RelayCommand]
    private void ToggleCommandPalette()
    {
        IsCommandPaletteOpen = !IsCommandPaletteOpen;
        if (IsCommandPaletteOpen)
        {
            PaletteQuery = string.Empty;
            RefreshPalette();
        }
    }

    [RelayCommand]
    private void CloseCommandPalette()
    {
        IsCommandPaletteOpen = false;
        PaletteQuery = string.Empty;
    }

    [RelayCommand]
    private async Task SelectFiles()
    {
        if (CurrentView is EncryptionViewModel encrypt)
            await encrypt.SelectFileCommand.ExecuteAsync(null);
        else if (CurrentView is DecryptionViewModel decrypt)
            await decrypt.SelectFileCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task RunCurrent()
    {
        if (CurrentView is EncryptionViewModel encrypt)
            await encrypt.EncryptCommand.ExecuteAsync(null);
        else if (CurrentView is DecryptionViewModel decrypt)
            await decrypt.DecryptCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void Escape()
    {
        if (IsCommandPaletteOpen)
        {
            CloseCommandPalette();
            return;
        }

        if (CurrentView is EncryptionViewModel { IsEncrypting: true } encrypt)
            encrypt.CancelCommand.Execute(null);
        else if (CurrentView is DecryptionViewModel { IsDecrypting: true } decrypt)
            decrypt.CancelCommand.Execute(null);
    }

    [RelayCommand]
    private void ExecuteSelectedPalette()
    {
        CommandItem? command = SelectedCommand ?? FilteredCommands.FirstOrDefault();
        if (command is null)
            return;

        if (command.CanExecute?.Invoke() == false)
            return;

        CloseCommandPalette();
        command.Execute();
    }

    [RelayCommand]
    private void MovePaletteSelection(int offset)
    {
        if (FilteredCommands.Count == 0)
            return;

        int index = SelectedCommand is null ? 0 : FilteredCommands.IndexOf(SelectedCommand);
        if (index < 0)
            index = 0;

        index = Math.Clamp(index + offset, 0, FilteredCommands.Count - 1);
        SelectedCommand = FilteredCommands[index];
    }

    [RelayCommand]
    private void DismissNotification(NotificationItem? item)
    {
        if (item is not null)
            Notifications.Remove(item);
    }

    partial void OnPaletteQueryChanged(string value) => RefreshPalette();

    partial void OnSelectedPageChanged(string value)
    {
        OnPropertyChanged(nameof(IsEncryptSelected));
        OnPropertyChanged(nameof(IsDecryptSelected));
        OnPropertyChanged(nameof(IsSettingsSelected));
    }

    private void Navigate(string page, string title, ViewModelBase view)
    {
        SelectedPage = page;
        CurrentPageTitle = title;
        CurrentView = view;
        CloseCommandPalette();
    }

    private void RefreshPalette()
    {
        CommandItem[] commands =
        [
            new() { Title = "Go to Encrypt", Shortcut = "Ctrl+1", Keywords = "encrypt lock", Execute = () => ShowEncrypt() },
            new() { Title = "Go to Decrypt", Shortcut = "Ctrl+2", Keywords = "decrypt unlock", Execute = () => ShowDecrypt() },
            new() { Title = "Open Settings", Shortcut = "Ctrl+,", Keywords = "settings preferences theme about", Execute = () => ShowSettings() },
            new() { Title = "Select files", Shortcut = "Ctrl+O", Keywords = "open file browse", Execute = () => _ = SelectFiles() },
            new() { Title = "Run current operation", Shortcut = "Ctrl+Enter", Keywords = "encrypt decrypt start", Execute = () => _ = RunCurrent() },
            new() { Title = "Wipe clipboard", Shortcut = "Ctrl+Shift+X", Keywords = "clipboard clear security", Execute = () => _ = WipeClipboard() }
        ];

        string query = PaletteQuery.Trim();
        FilteredCommands.Clear();
        foreach (CommandItem command in commands)
        {
            if (query.Length == 0 ||
                command.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (command.Keywords?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                FilteredCommands.Add(command);
            }
        }

        SelectedCommand = FilteredCommands.FirstOrDefault();
    }

    private void OnNotificationRequested(object? sender, NotificationItem item)
    {
        Notifications.Add(item);
        DispatcherTimer.RunOnce(() => Notifications.Remove(item), TimeSpan.FromSeconds(4));
    }

    public async Task OnExitAsync()
    {
        if (_settingsService.Current.WipeClipboardOnExit)
            await _clipboardService.ClearClipboardAsync();
    }
}
