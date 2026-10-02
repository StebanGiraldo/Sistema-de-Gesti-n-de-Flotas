using FleetManagement.Application.Interfaces;

namespace FleetManagement.Application.Notifications;

/// <summary>
/// ABSTRACCIÓN (patrón estructural BRIDGE).
///
/// Representa una notificación del sistema de gestión de flotas
/// independientemente del canal por el que se envíe. La relación con
/// <see cref="INotificationChannel"/> (el Implementor) es por COMPOSICIÓN
/// ("una FleetNotification TIENE UN canal"), no por herencia: eso es
/// justamente lo que permite que el mismo tipo de notificación use distintos
/// canales sin crear una subclase por cada combinación (ver
/// <c>MaintenanceNotification</c>, <c>TripAlertNotification</c> y
/// <c>VehicleNotification</c>, que nunca heredan de una clase por canal ni
/// referencian un canal concreto).
///
/// Cada especialización (Refined Abstraction) sólo decide QUÉ se envía
/// (destinatario, asunto, cuerpo, construidos a partir de datos de dominio);
/// el CÓMO se envía queda completamente delegado en <see cref="Channel"/>.
///
/// Implementa también <see cref="INotification"/>, el contrato común (Component
/// del patrón DECORATOR) que permite envolver cualquier notificación con
/// decoradores sin modificar sus subclases. Es el único cambio hecho a esta
/// clase al incorporar Decorator: no altera ningún comportamiento del Bridge.
/// </summary>
public abstract class FleetNotification : INotification
{
    /// <summary>
    /// El Implementor (Bridge): a quién se le delega el envío. Las
    /// especializaciones sólo arman contenido; nunca envían por su cuenta ni
    /// conocen si el canal es correo, SMS o push.
    /// </summary>
    protected readonly INotificationChannel Channel;

    protected FleetNotification(INotificationChannel channel)
    {
        Channel = channel ?? throw new ArgumentNullException(nameof(channel));
    }

    /// <summary>Arma el contenido específico de este tipo de notificación y lo entrega al canal.</summary>
    public abstract Task SendAsync();
}
