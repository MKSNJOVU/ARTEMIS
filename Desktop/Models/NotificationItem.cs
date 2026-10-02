using System;

namespace Artemis.Desktop.Models;

public sealed class NotificationItem
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string Message { get; init; }
    public NotificationKind Kind { get; init; } = NotificationKind.Info;
}
