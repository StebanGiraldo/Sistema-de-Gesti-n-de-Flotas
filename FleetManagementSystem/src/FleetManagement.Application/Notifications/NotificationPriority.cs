namespace FleetManagement.Application.Notifications;

/// <summary>Nivel de urgencia de una notificación. Lo aporta <c>PriorityNotificationDecorator</c>.</summary>
public enum NotificationPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}
