namespace FleetManagement.Application.Notifications.Decorators;

/// <summary>
/// Un decorador ES una notificación (<see cref="INotification"/>) y además
/// TIENE otra notificación envuelta (<see cref="Inner"/>). Exponer la
/// notificación envuelta permite recorrer la cadena de decoradores.
/// </summary>
public interface INotificationDecorator : INotification
{
    INotification Inner { get; }
}
