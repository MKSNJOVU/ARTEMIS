using System;
using Artemis.Desktop.Models;

namespace Artemis.Desktop.Services;

public interface INotificationService
{
    event EventHandler<NotificationItem>? NotificationRequested;
    void Show(string message, NotificationKind kind = NotificationKind.Info);
}
