using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
namespace Artemis.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    #region Private fields
    private readonly IServiceProvider _serviceProvider;
    [ObservableProperty]
    private ViewModelBase? _currentView;
    #endregion

    #region Constructor
    public MainWindowViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
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

    #endregion
}
