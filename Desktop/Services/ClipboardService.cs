using System.Threading.Tasks;
using Artemis.Desktop.Services.Interfaces;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Artemis.Desktop.Services;

public class ClipboardService : IClipboardService
{
    public async Task ClearClipboardAsync()
    {
        // EXCELLENCE: Access the TopLevel context via the application lifecycle
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && 
            desktop.MainWindow != null)
        {
            await SecurityHelper.ClearClipboardAsync(desktop.MainWindow);
        }
    }
}
