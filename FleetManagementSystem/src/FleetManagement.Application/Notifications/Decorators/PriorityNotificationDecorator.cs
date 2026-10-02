namespace FleetManagement.Application.Notifications.Decorators;

/// <summary>
/// DECORATOR concreto: añade a la notificación un nivel de prioridad. No
/// altera el contenido ni el canal (eso es responsabilidad del Bridge); el
/// nivel lo aprovechan los demás decoradores de la cadena: el de reintentos
/// decide cuánto esfuerzo de entrega hacer y el de auditoría lo registra.
/// Para influir en el de reintentos debe quedar DENTRO de él en la cadena
/// (ver <c>NotificationDecoratorExtensions</c>).
/// </summary>
public sealed class PriorityNotificationDecorator : NotificationDecorator, IPrioritizedNotification
{
    public PriorityNotificationDecorator(INotification inner, NotificationPriority priority) : base(inner)
    {
        if (!Enum.IsDefined(priority))
            throw new ArgumentOutOfRangeException(nameof(priority), priority, "Prioridad de notificación no válida.");

        Priority = priority;
    }

    public NotificationPriority Priority { get; }

    public override Task SendAsync() => Inner.SendAsync();
}
