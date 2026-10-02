namespace FleetManagement.Application.Notifications.Decorators;

/// <summary>
/// Capacidad opcional: la notificación (o alguno de sus decoradores) declara
/// un nivel de prioridad. Otros decoradores la descubren recorriendo la
/// cadena (ver <see cref="NotificationChain"/>) sin depender de una clase
/// concreta.
/// </summary>
public interface IPrioritizedNotification
{
    NotificationPriority Priority { get; }
}
