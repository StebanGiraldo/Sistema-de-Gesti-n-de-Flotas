using FleetManagement.Application.Interfaces;

namespace FleetManagement.Application.Notifications;

/// <summary>
/// ESPECIALIZACIÓN / REFINED ABSTRACTION (patrón BRIDGE).
///
/// Notificación de una incidencia reportada durante un viaje (se dispara al
/// crear una alerta en <c>TripAlertService.CreateAlertAsync</c>). A
/// diferencia de <see cref="MaintenanceNotification"/>, el destinatario
/// natural no es el conductor (él es quien reporta la incidencia), sino el
/// equipo de operaciones/despacho, que es quien debe reaccionar.
/// </summary>
public class TripAlertNotification : FleetNotification
{
    private readonly string _recipient;
    private readonly string _routeName;
    private readonly string _alertType;
    private readonly string _description;
    private readonly int _delayMinutes;

    public TripAlertNotification(
        INotificationChannel channel,
        string recipient,
        string routeName,
        string alertType,
        string description,
        int delayMinutes)
        : base(channel)
    {
        _recipient = recipient;
        _routeName = routeName;
        _alertType = alertType;
        _description = description;
        _delayMinutes = delayMinutes;
    }

    public override Task SendAsync()
    {
        var subject = $"Nueva alerta de viaje - {_routeName}";
        var message = $"Se reportó una incidencia de tipo '{_alertType}' en la ruta '{_routeName}': {_description}."
            + (_delayMinutes > 0 ? $" Retraso estimado: {_delayMinutes} min." : string.Empty);

        return Channel.SendAsync(_recipient, subject, message);
    }
}
