using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Artemis.Desktop.Services;

public class FilePickerService : IFilePickerService
{
    public async Task<string?> OpenFileAsync(string title = "Open File", bool allowMultiple = false)
    {
        var topLevel = GetTopLevel();
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = allowMultiple,
            Title = title
        });
        return files?.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> SaveFileAsync(string title = "Save File")
    {
        var topLevel = GetTopLevel();
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title
        });
        return file?.TryGetLocalPath();
    }

    public async Task<string?> OpenFolderAsync(bool allowMultiple = false, string title = "Select Folder")
    {
        var topLevel = GetTopLevel();
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = allowMultiple,
            Title = title
        });
        return folders?.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    private TopLevel GetTopLevel()
    {
        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
        && desktop.MainWindow is not null)
            return desktop.MainWindow;

        throw new InvalidOperationException("No top-level window available");
    }
}