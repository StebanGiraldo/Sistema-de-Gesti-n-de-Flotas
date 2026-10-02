using FleetManagement.Application.Interfaces;

namespace FleetManagement.Application.Notifications.Decorators;

/// <summary>
/// Sintaxis fluida para apilar decoradores. El primero que se llama queda
/// más adentro (más cerca de la notificación original). Ejemplo, con el orden
/// recomendado (prioridad dentro de reintentos, reintentos dentro de auditoría):
///
///   await new MaintenanceNotification(channel, ...)
///       .WithPriority(NotificationPriority.High)
///       .WithRetry()
///       .WithAudit(auditLogger)
///       .SendAsync();
///
/// Cadena resultante: Audit → Retry → Priority → MaintenanceNotification → (Bridge) canal.
/// </summary>
public static class NotificationDecoratorExtensions
{
    public static INotification WithPriority(this INotification notification, NotificationPriority priority)
        => new PriorityNotificationDecorator(notification, priority);

    public static INotification WithRetry(this INotification notification, int? maxAttempts = null, TimeSpan? delayBetweenAttempts = null)
        => new RetryNotificationDecorator(notification, maxAttempts, delayBetweenAttempts);

    public static INotification WithAudit(this INotification notification, IFleetAuditLogger auditLogger)
        => new AuditNotificationDecorator(notification, auditLogger);
}
