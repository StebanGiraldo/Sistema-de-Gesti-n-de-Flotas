using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Services;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Commands;

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
            route.Waypoints.Select(waypoint => new WaypointPosition(waypoint.Id, waypoint.Order)).ToList(),
            route.EstimatedDistanceKm,
            route.EstimatedDurationMinutes);
    }

    public async Task RestoreStopOrderAsync(RouteSnapshot snapshot)
    {
        var route = await FindAsync(snapshot.RouteId);
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
