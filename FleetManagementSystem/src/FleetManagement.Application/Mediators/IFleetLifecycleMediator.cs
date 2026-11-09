using FleetManagement.Domain.Entities;

namespace FleetManagement.Application.Mediators;

/// <summary>
/// MEDIATOR (patrón de comportamiento MEDIATOR): coordina el ciclo de vida de las rutas y los vehículos.
///
/// Antes, quien cambiaba el estado de una ruta (<c>DeliveryRouteService.UpdateRouteStatusAsync</c>) también tocaba
/// directamente al vehículo asignado, y quien registraba un retraso (<c>TripAlertService</c>) cambiaba la ruta por su
/// cuenta: la coordinación entre rutas y vehículos estaba repartida entre los servicios. Ahora los participantes
/// (<see cref="IRouteColleague"/> e <see cref="IVehicleColleague"/>) NO se conocen entre sí: el de rutas avisa de lo que
/// pasó a este mediador y es el mediador quien decide qué le toca hacer al de vehículos. Cambiar cómo interactúan
/// (otra regla, otro participante) significa cambiar sólo este mediador.
///
/// No es una fachada: una fachada simplifica la llamada de un cliente a varios subsistemas que no saben de ella; aquí
/// los participantes son quienes inician la comunicación (avisan al mediador) y el mediador les responde y los dirige.
/// Tampoco reemplaza a los patrones que ya existen: la cadena de alertas, el puente de notificaciones y sus
/// decoradores siguen donde estaban; la auditoría del cambio de estado la sigue escribiendo el servicio de rutas.
/// </summary>
public interface IFleetLifecycleMediator
{
    /// <summary>
    /// Una ruta cambió de estado y ya se guardó (<c>route.Status</c> trae el nuevo). Coordina lo que eso implica para su
    /// vehículo asignado:
    /// <list type="number">
    ///   <item>En progreso o Retrasada: el vehículo queda En ruta.</item>
    ///   <item>Completada o Cancelada: el vehículo queda Disponible sólo si iba En ruta y no sigue en otro viaje activo.</item>
    ///   <item>Un vehículo en Mantenimiento o Fuera de servicio no pasa a Disponible por terminar una ruta.</item>
    ///   <item>Sin vehículo asignado (o con un vehículo que ya no existe) no hay nada que coordinar.</item>
    /// </list>
    /// <paramref name="sender"/> es el participante de rutas que avisa; el mediador puede preguntarle lo que necesite saber.
    /// </summary>
    Task RouteStatusChangedAsync(IRouteColleague sender, DeliveryRoute route);

    /// <summary>
    /// Una alerta reportó un retraso y la ruta quedó Retrasada con los minutos sumados (ya guardado). Conserva lo que ya
    /// hacía el servicio de alertas: el retraso NO cambia al vehículo (ver el comentario de la implementación).
    /// </summary>
    Task RouteDelayReportedAsync(IRouteColleague sender, DeliveryRoute route);
}
