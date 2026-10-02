namespace Artemis.Desktop.Models;

public sealed class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;
    public string? DefaultOutputFolder { get; set; }
    public bool WipeClipboardOnExit { get; set; } = true;
    public int ClipboardAutoWipeSeconds { get; set; }
    public bool RememberRecentFiles { get; set; } = true;
    public string? LastOpenFolder { get; set; }
}
