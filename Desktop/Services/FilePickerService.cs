using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Artemis.Desktop.Services;

public class FilePickerService : IFilePickerService
{
    public async Task<IReadOnlyList<string>?> OpenFileAsync(
        string title = "Open File",
        bool allowMultiple = false,
        IReadOnlyList<string>? patterns = null,
        string? startPath = null)
    {
        Window topLevel = GetTopLevel();
        IStorageFolder? startLocation = await TryGetFolderAsync(topLevel.StorageProvider, startPath);

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = allowMultiple,
            Title = title,
            SuggestedStartLocation = startLocation,
            FileTypeFilter = CreateFilters(patterns)
        });

        return files.Count > 0
            ? files.Select(file => file.TryGetLocalPath()).OfType<string>().ToList()
            : null;
    }

    public async Task<string?> SaveFileAsync(
        string suggestedFileName,
        string title = "Save File",
        IReadOnlyList<string>? patterns = null,
        string? startPath = null)
    {
        Window topLevel = GetTopLevel();
        IStorageFolder? startLocation = await TryGetFolderAsync(topLevel.StorageProvider, startPath);

        IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            SuggestedStartLocation = startLocation,
            FileTypeChoices = CreateFilters(patterns)
        });

        return file?.TryGetLocalPath();
    }

    public async Task<string?> OpenFolderAsync(string title = "Select Folder", string? startPath = null)
    {
        Window topLevel = GetTopLevel();
        IStorageFolder? startLocation = await TryGetFolderAsync(topLevel.StorageProvider, startPath);

        IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            SuggestedStartLocation = startLocation
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() ?? folders[0].Path.LocalPath : null;
    }

    private static IReadOnlyList<FilePickerFileType>? CreateFilters(IReadOnlyList<string>? patterns)
    {
        if (patterns is null || patterns.Count == 0)
            return null;

        return
        [
            new FilePickerFileType("Supported files") { Patterns = patterns.ToList() },
            FilePickerFileTypes.All
        ];
    }

    private static async Task<IStorageFolder?> TryGetFolderAsync(IStorageProvider provider, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        string directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? path;
        if (!Directory.Exists(directory))
            return null;

        return await provider.TryGetFolderFromPathAsync(directory);
    }

    private static Window GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is not null)
            return desktop.MainWindow;

        throw new InvalidOperationException("No top-level window available");
    }
}
