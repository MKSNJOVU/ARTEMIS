using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace Artemis.Desktop.Services;

public class DialogService : IDialogService
{
    public async Task<bool> ShowConfirmationAsync(string? message)
    {
        var topLevel = GetTopLevel();

        var messageBox = MessageBoxManager.GetMessageBoxStandard("Confirmation", message ?? string.Empty, ButtonEnum.YesNo);
        var response = await messageBox.ShowWindowDialogAsync(topLevel);
        return response == ButtonResult.Yes;
    }

    private Window GetTopLevel()
    {
        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
        && desktop.MainWindow is not null)
            return desktop.MainWindow;

        throw new InvalidOperationException("No top-level window available");
    }
}