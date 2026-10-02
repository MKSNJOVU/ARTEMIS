using System;
using System.Diagnostics;
using System.IO;

namespace Artemis.Desktop.Services;

public sealed class ShellRevealService : IShellRevealService
{
    public void Reveal(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("explorer", $"/select,\"{path}\"")
                {
                    UseShellExecute = true
                });
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo("open", $"-R \"{path}\"")
                {
                    UseShellExecute = false
                });
                return;
            }

            string? directory = File.Exists(path) ? Path.GetDirectoryName(path) : path;
            if (string.IsNullOrWhiteSpace(directory))
                return;

            Process.Start(new ProcessStartInfo("xdg-open", directory)
            {
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            // Reveal is best-effort; the file is already written.
        }
    }
}
