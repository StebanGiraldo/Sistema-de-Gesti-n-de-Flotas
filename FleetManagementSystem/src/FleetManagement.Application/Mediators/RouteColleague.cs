using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Mediators;

/// <summary>
/// CONCRETE COLLEAGUE de rutas (patrón MEDIATOR). Guarda los cambios de estado de la ruta con el repositorio de rutas y
/// se los comunica al mediador; no conoce al participante de vehículos ni a ningún servicio. Los servicios siguen siendo
/// quienes validan la petición, arman las respuestas y auditan: esta clase sólo es dueña de la transición de estado.
/// </summary>
public sealed class RouteColleague : IRouteColleague
{
    private readonly IDeliveryRouteRepository _routes;
    private readonly IFleetLifecycleMediator _mediator;

    public RouteColleague(IDeliveryRouteRepository routes, IFleetLifecycleMediator mediator)
    {
        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task ChangeStatusAsync(DeliveryRoute route, DeliveryRouteStatus newStatus)
    {
        ArgumentNullException.ThrowIfNull(route);

        route.Status = newStatus;
        await _routes.UpdateAsync(route);

        await _mediator.RouteStatusChangedAsync(this, route);
    }

    public async Task ReportDelayAsync(DeliveryRoute route, int delayMinutes)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(delayMinutes);

        route.DelayMinutes += delayMinutes;
        route.Status = DeliveryRouteStatus.Delayed;
        await _routes.UpdateAsync(route);

        await _mediator.RouteDelayReportedAsync(this, route);
    }

    public async Task<bool> HasOtherActiveTripAsync(Guid vehicleId, Guid exceptRouteId)
        => (await _routes.GetAllAsync()).Any(other =>
            other.Id != exceptRouteId
            && other.AssignedVehicleId == vehicleId
            && other.Status is DeliveryRouteStatus.InProgress or DeliveryRouteStatus.Delayed);
}
