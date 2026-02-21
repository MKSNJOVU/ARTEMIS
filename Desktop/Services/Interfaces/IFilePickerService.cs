using System.Threading.Tasks;

namespace Artemis.Desktop.Services;

public interface IFilePickerService
{
    Task<string?> OpenFileAsync(string title = "Open File", bool allowMultiple = false);
    Task<string?> SaveFileAsync(string title = "Save File");
    Task<string?> OpenFolderAsync(bool allowMultiple = false, string title = "Select Folder");
}