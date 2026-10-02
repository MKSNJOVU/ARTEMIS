using System;
using Artemis.Desktop.Models;
using Avalonia.Threading;

namespace Artemis.Desktop.Services;

public sealed class NotificationService : INotificationService
{
    public event EventHandler<NotificationItem>? NotificationRequested;

    public void Show(string message, NotificationKind kind = NotificationKind.Info)
    {
        var item = new NotificationItem
        {
            Message = message,
            Kind = kind
        };

        Dispatcher.UIThread.Post(() => NotificationRequested?.Invoke(this, item));
    }
}
