using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Services;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Commands;

/// <summary>
/// ORIGINATOR (patrón MEMENTO): crea el recuerdo con el orden de las paradas y las estimaciones de la ruta y es el único que
/// sabe volver a ese estado. La restauración es "todo o nada": primero verifica que la ruta siga planificada y con las mismas
/// paradas, y solo entonces cambia algo.
/// </summary>
public sealed class RouteSnapshotService : IRouteSnapshotService
{
    private readonly IDeliveryRouteRepository _routes;

    public RouteSnapshotService(IDeliveryRouteRepository routes)
    {
        _routes = routes;
    }

    public async Task<RouteSnapshot> CaptureAsync(Guid routeId)
    {
        var route = await FindAsync(routeId);
        return new RouteSnapshot(
            route.Id,
            route.Name,
            route.Waypoints.Select(waypoint => new WaypointPosition(waypoint.Id, waypoint.Order)),
            route.EstimatedDistanceKm,
            route.EstimatedDurationMinutes);
    }

    public async Task RestoreStopOrderAsync(IRouteMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);

        // Solo se restaura con un recuerdo propio: uno ajeno no trae las paradas ni las estimaciones que hacen falta.
        if (memento is not RouteSnapshot snapshot)
            throw new ArgumentException("El recuerdo no lo creó este servicio (CaptureAsync); no se puede restaurar a partir de él.", nameof(memento));

        var route = await _routes.GetByIdAsync(snapshot.RouteId)
            ?? throw new CommandNotUndoableException($"La ruta '{snapshot.RouteName}' ya no existe; no se puede restaurar el orden de sus paradas.");
        if (route.Status != DeliveryRouteStatus.Planned)
            throw new CommandNotUndoableException($"La ruta '{route.Name}' ya no está planificada (está {RouteStatusText.Describe(route.Status)}); no se puede restaurar el orden de sus paradas.");

        var byId = route.Waypoints.ToDictionary(waypoint => waypoint.Id);
        if (byId.Count != snapshot.Stops.Count || snapshot.Stops.Any(stop => !byId.ContainsKey(stop.WaypointId)))
            throw new CommandNotUndoableException($"Las paradas de la ruta '{route.Name}' cambiaron después de optimizarla; no se puede restaurar su orden.");

        foreach (var stop in snapshot.Stops)
            byId[stop.WaypointId].Order = stop.Order;

        route.Waypoints = route.Waypoints.OrderBy(waypoint => waypoint.Order).ToList();
        route.EstimatedDistanceKm = snapshot.EstimatedDistanceKm;
        route.EstimatedDurationMinutes = snapshot.EstimatedDurationMinutes;
        await _routes.UpdateAsync(route);
    }

    private async Task<DeliveryRoute> FindAsync(Guid routeId)
        => await _routes.GetByIdAsync(routeId) ?? throw new KeyNotFoundException("Ruta no encontrada.");
}
