namespace FleetManagement.Application.Notifications.Decorators;

/// <summary>
/// DECORATOR base (patrón estructural DECORATOR): guarda la notificación
/// envuelta y obliga a cada decorador concreto a definir cómo envía. Cada
/// decorador concreto añade UNA responsabilidad (auditar, reintentar,
/// priorizar) y delega el envío real en <see cref="Inner"/>, que puede ser la
/// notificación original (Bridge) u otro decorador.
/// </summary>
public abstract class NotificationDecorator : INotificationDecorator
{
    protected NotificationDecorator(INotification inner)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public INotification Inner { get; }

    public abstract Task SendAsync();
}
