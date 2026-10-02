using System;

namespace Artemis.Desktop.Models;

public sealed class RecentFileEntry
{
    public required string Path { get; init; }
    public required string Kind { get; init; }
    public DateTimeOffset LastUsed { get; init; } = DateTimeOffset.UtcNow;
    public string FileName => System.IO.Path.GetFileName(Path);
}
