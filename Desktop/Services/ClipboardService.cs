using System;
using System.Threading;
using System.Threading.Tasks;
using Artemis.Desktop.Services.Interfaces;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Artemis.Desktop.Services;

public class ClipboardService : IClipboardService
{
    private CancellationTokenSource? _scheduledWipe;

    public async Task ClearClipboardAsync()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is not null)
        {
            await SecurityHelper.ClearClipboardAsync(desktop.MainWindow);
        }
    }

    public void ScheduleClear(int delaySeconds)
    {
        _scheduledWipe?.Cancel();
        _scheduledWipe?.Dispose();

        if (delaySeconds <= 0)
            return;

        var cts = new CancellationTokenSource();
        _scheduledWipe = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cts.Token);
                await Dispatcher.UIThread.InvokeAsync(ClearClipboardAsync);
            }
            catch (OperationCanceledException)
            {
            }
        }, cts.Token);
    }
}
