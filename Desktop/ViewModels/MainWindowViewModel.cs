using Artemis.Desktop.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace Artemis.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    #region Private fields
    private readonly IServiceProvider _serviceProvider;
    private readonly IClipboardService _clipboardService;

    [ObservableProperty]
    private ViewModelBase? _currentView;
    #endregion

    #region Constructor
    public MainWindowViewModel(IServiceProvider serviceProvider, IClipboardService clipboardService)
    {
        _serviceProvider = serviceProvider;
        _clipboardService = clipboardService;
        // Set the default view on startup
        ShowEncrypt();
    }
    #endregion

    #region Methods
    [RelayCommand]
    private void ShowEncrypt()
    {
        CurrentView = _serviceProvider.GetRequiredService<EncryptionViewModel>();
    }
    [RelayCommand]
    private void ShowDecrypt()
    {
        CurrentView = _serviceProvider.GetRequiredService<DecryptionViewModel>();
    }

    [RelayCommand]
    private async Task WipeClipboard()
    {
        await _clipboardService.ClearClipboardAsync();
    }
    #endregion
}
