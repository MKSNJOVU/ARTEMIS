using System;
using Artemis.Core.Cryptography;
using Artemis.Core.Interfaces;
using Artemis.Desktop.Services;
using Artemis.Desktop.Services.Interfaces;
using Artemis.Desktop.ViewModels;
using Artemis.Desktop.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace Artemis.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();
        Services = serviceProvider;

        var settings = Services.GetRequiredService<ISettingsService>();
        ThemeService.Apply(settings.Current.Theme);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };

            desktop.Exit += async (_, _) =>
            {
                var mainWindow = Services.GetRequiredService<MainWindowViewModel>();
                await mainWindow.OnExitAsync();
            };

            desktop.Exit += (_, _) => serviceProvider.Dispose();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IKeyDerivationService, KeyDerivationService>();
        services.AddSingleton<IEncryptionService, EncryptionService>();

        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IRecentFilesService, RecentFilesService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IShellRevealService, ShellRevealService>();

        services.AddSingleton<EncryptionViewModel>();
        services.AddSingleton<DecryptionViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();
    }
}
