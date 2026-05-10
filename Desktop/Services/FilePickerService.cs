using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Artemis.Desktop.Services;

public class FilePickerService : IFilePickerService
{
    public async Task<IReadOnlyList<string>?> OpenFileAsync(string title = "Open File", bool allowMultiple = false)
    {
        var topLevel = GetTopLevel();
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = allowMultiple,
            Title = title
        });
        return files?.Count > 0 ? files?
        .Select(c => c.TryGetLocalPath())
        .OfType<string>()
        .ToList() : null;
    }

    public async Task<string?> SaveFileAsync(string suggestedFileName, string title = "Save File")
    {
        var topLevel = GetTopLevel();
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName
        });
        return file?.TryGetLocalPath();
    }

    public async Task<string>? OpenFolderAsync(string title = "Select Folder", bool allowMultiple = false)
    {
        var topLevel = GetTopLevel();
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = allowMultiple,
            Title = title
        });
        string actualPath;
        return folders?.Count > 0 ? actualPath = folders[0].Path.LocalPath : null;
    }

    private static Window GetTopLevel()
    {
        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
        && desktop.MainWindow is not null)
            return desktop.MainWindow;

        throw new InvalidOperationException("No top-level window available");
    }
}