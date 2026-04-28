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
using System;
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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };

            desktop.Exit += async (_, _) =>
            {
                var clipboardService = Services.GetRequiredService<IClipboardService>();
                await clipboardService.ClearClipboardAsync();
            };

            desktop.Exit += (_, _) => serviceProvider.Dispose();
        }
    }


    private static void ConfigureServices(IServiceCollection services)
    {
        // Core
        services.AddSingleton<IKeyDerivationService, KeyDerivationService>();
        services.AddSingleton<IEncryptionService, EncryptionService>();

        // Desktop services
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IClipboardService, ClipboardService>();



        // ViewModels
        services.AddTransient<EncryptionViewModel>();
        services.AddTransient<DecryptionViewModel>();
        services.AddSingleton<MainWindowViewModel>();
    }

}
