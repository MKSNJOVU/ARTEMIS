using System.Threading.Tasks;
using System.Collections.Generic;

namespace Artemis.Desktop.Services;

public interface IFilePickerService
{
    Task<IReadOnlyList<string>?> OpenFileAsync(string title = "Open File", bool allowMultiple = false);
    Task<string?> SaveFileAsync(string suggestedFileName, string title = "Save File");
    Task<IReadOnlyList<string>?> OpenFolderAsync(string title = "Select Folder", bool allowMultiple = false);
}