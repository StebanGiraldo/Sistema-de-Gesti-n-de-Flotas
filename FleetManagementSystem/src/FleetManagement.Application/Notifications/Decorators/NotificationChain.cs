namespace FleetManagement.Application.Notifications.Decorators;

/// <summary>
/// Utilidades para recorrer una cadena de decoradores desde afuera hacia
/// adentro hasta llegar a la notificación original.
/// </summary>
public static class NotificationChain
{
    /// <summary>Devuelve <paramref name="start"/>, luego lo que envuelve, y así hasta la notificación base (incluida).</summary>
    public static IEnumerable<INotification> Walk(INotification start)
    {
        ArgumentNullException.ThrowIfNull(start);

        var current = start;
        while (true)
        {
            yield return current;

            if (current is INotificationDecorator decorator)
                current = decorator.Inner;
            else
                yield break;
        }
    }

    /// <summary>Notificación original (la más interna, no decorador) de la cadena.</summary>
    public static INotification Unwrap(INotification start) => Walk(start).Last();

    /// <summary>Primer elemento de la cadena (de afuera hacia adentro) que ofrezca la capacidad <typeparamref name="T"/>.</summary>
    public static T? Find<T>(INotification start) where T : class => Walk(start).OfType<T>().FirstOrDefault();
}
