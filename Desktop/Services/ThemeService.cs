using Artemis.Desktop.Models;
using Avalonia;
using Avalonia.Styling;

namespace Artemis.Desktop.Services;

public static class ThemeService
{
    public static void Apply(ThemeMode theme)
    {
        if (Application.Current is null)
            return;

        Application.Current.RequestedThemeVariant = theme switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.System => ThemeVariant.Default,
            _ => ThemeVariant.Dark
        };
    }
}
