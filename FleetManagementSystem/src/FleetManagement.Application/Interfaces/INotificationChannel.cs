namespace FleetManagement.Application.Interfaces;

/// <summary>
/// IMPLEMENTOR (patrón estructural BRIDGE).
///
/// Abstrae el canal de envío de una notificación (correo, SMS, push, ...)
/// detrás de una única operación de envío. La Abstracción del patrón
/// (<c>FleetManagement.Application.Notifications.FleetNotification</c>) y
/// sus especializaciones (<c>MaintenanceNotification</c>,
/// <c>TripAlertNotification</c>, <c>VehicleNotification</c>) dependen
/// ÚNICAMENTE de esta interfaz, nunca de un canal concreto: eso es lo que
/// permite combinar cualquier tipo de notificación con cualquier canal sin
/// crear una clase por cada combinación (evita la explosión combinatoria
/// típica que resuelve Bridge).
/// </summary>
public interface INotificationChannel
{
    /// <summary>
    /// Entrega el mensaje ya construido por la Abstracción. En este
    /// prototipo el envío está simulado (consola + registro de auditoría);
    /// no se integra ningún proveedor real de correo/SMS/push.
    /// </summary>
    Task SendAsync(string recipient, string subject, string message);
}
