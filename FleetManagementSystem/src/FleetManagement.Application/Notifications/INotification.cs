namespace FleetManagement.Application.Notifications;

/// <summary>
/// COMPONENT (patrón estructural DECORATOR).
///
/// Contrato común de "algo que se puede enviar". Lo implementan tanto las
/// notificaciones originales (<see cref="FleetNotification"/> y sus
/// especializaciones del patrón BRIDGE) como todos los decoradores, así que
/// quien envía una notificación no sabe ni le importa si tiene delante la
/// original o una envuelta por varios decoradores.
/// </summary>
public interface INotification
{
    Task SendAsync();
}
