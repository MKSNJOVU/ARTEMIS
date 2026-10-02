using System.IO;

namespace Artemis.Desktop.Models;

public sealed class SelectedFileItem
{
    public required string FullPath { get; init; }
    public string FileName => Path.GetFileName(FullPath);
    public string DirectoryName => Path.GetDirectoryName(FullPath) ?? string.Empty;
}
