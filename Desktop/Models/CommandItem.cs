using System;

namespace Artemis.Desktop.Models;

public sealed class CommandItem
{
    public required string Title { get; init; }
    public string? Shortcut { get; init; }
    public string? Keywords { get; init; }
    public required Action Execute { get; init; }
    public Func<bool>? CanExecute { get; init; }
}
