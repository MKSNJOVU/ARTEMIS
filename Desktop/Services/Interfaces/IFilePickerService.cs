using System.Collections.Generic;
using System.Threading.Tasks;

namespace Artemis.Desktop.Services;

public interface IFilePickerService
{
    Task<IReadOnlyList<string>?> OpenFileAsync(
        string title = "Open File",
        bool allowMultiple = false,
        IReadOnlyList<string>? patterns = null,
        string? startPath = null);

    Task<string?> SaveFileAsync(
        string suggestedFileName,
        string title = "Save File",
        IReadOnlyList<string>? patterns = null,
        string? startPath = null);

    Task<string?> OpenFolderAsync(string title = "Select Folder", string? startPath = null);
}
