using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Mediators;

/// <summary>
/// COLLEAGUE de rutas (patrón MEDIATOR): el participante que es dueño del estado de la ruta. Es el ÚNICO que cambia el
/// estado de una ruta por el ciclo de vida (<c>DeliveryRouteService</c> lo hace al actualizar el estado y
/// <c>TripAlertService</c> al registrar un retraso); después de guardar el cambio se lo comunica al mediador, y no
/// conoce al participante de vehículos.
///
/// También responde a lo que el mediador le pregunta sobre las rutas (<see cref="HasOtherActiveTripAsync"/>): el
/// conocimiento de qué es un viaje activo vive aquí, no en el mediador ni en el vehículo.
/// </summary>
public interface IRouteColleague
{
    /// <summary>Guarda el nuevo estado de la ruta y avisa al mediador (<see cref="IFleetLifecycleMediator.RouteStatusChangedAsync"/>).</summary>
    Task ChangeStatusAsync(DeliveryRoute route, DeliveryRouteStatus newStatus);

    /// <summary>
    /// Suma los minutos de retraso, deja la ruta Retrasada, la guarda y avisa al mediador
    /// (<see cref="IFleetLifecycleMediator.RouteDelayReportedAsync"/>). <paramref name="delayMinutes"/> debe ser positivo.
    /// </summary>
    Task ReportDelayAsync(DeliveryRoute route, int delayMinutes);

    /// <summary>
    /// Indica si el vehículo sigue ocupado en OTRO viaje activo (una ruta distinta de <paramref name="exceptRouteId"/> que
    /// lo tiene asignado y está En progreso o Retrasada).
    /// </summary>
    Task<bool> HasOtherActiveTripAsync(Guid vehicleId, Guid exceptRouteId);
}
