using FleetManagement.Application.Commands;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Memento;

/// <summary>
/// El Originator tiene que GUARDAR la ruta restaurada, no solo cambiarla en el objeto que leyó. Se prueba con un repositorio que,
/// como una base de datos real, guarda y entrega copias: si el Originator olvidara llamar a UpdateAsync, el orden y las
/// estimaciones restaurados se perderían y estas pruebas fallarían (con el repositorio en memoria del proyecto, que entrega siempre
/// la misma instancia, ese olvido no se notaría).
/// </summary>
public class RouteMementoPersistenceTests
{
    private readonly CopyingRouteRepository _routes = new();

    private async Task<DeliveryRoute> PlannedRouteAsync()
    {
        var route = new DeliveryRoute { Name = "Ruta de prueba", Status = DeliveryRouteStatus.Planned, EstimatedDistanceKm = 155.7, EstimatedDurationMinutes = 117 };
        foreach (var (label, order) in new[] { ("A", 0), ("B", 1), ("C", 2) })
            route.Waypoints.Add(new Waypoint { Order = order, Label = label });
        return await _routes.AddAsync(route);
    }

    /// <summary>Lo que hace una optimización: reordena las paradas y cambia las estimaciones, y lo guarda.</summary>
    private async Task OptimizeAsync(Guid routeId)
    {
        var route = (await _routes.GetByIdAsync(routeId))!;
        var newOrder = new[] { "C", "A", "B" };
        foreach (var waypoint in route.Waypoints)
            waypoint.Order = Array.IndexOf(newOrder, waypoint.Label);
        route.Waypoints = route.Waypoints.OrderBy(waypoint => waypoint.Order).ToList();
        route.EstimatedDistanceKm = 100;
        route.EstimatedDurationMinutes = 80;
        await _routes.UpdateAsync(route);
    }

    private async Task<string> StoredStateAsync(Guid routeId)
    {
        var route = (await _routes.GetByIdAsync(routeId))!;
        return $"{string.Join(",", route.Waypoints.OrderBy(waypoint => waypoint.Order).Select(waypoint => waypoint.Label))}|{route.EstimatedDistanceKm}|{route.EstimatedDurationMinutes}";
    }

    [Fact]
    public async Task Restore_LeavesTheOriginalStopsAndEstimatesSaved_InTheStore()
    {
        var route = await PlannedRouteAsync();
        var originator = new RouteSnapshotService(_routes);
        var memento = await originator.CaptureAsync(route.Id);
        await OptimizeAsync(route.Id);
        Assert.Equal("C,A,B|100|80", await StoredStateAsync(route.Id));

        await originator.RestoreStopOrderAsync(memento);

        Assert.Equal("A,B,C|155.7|117", await StoredStateAsync(route.Id));
    }

    [Fact]
    public async Task Capture_ChangesNothingInTheStore()
    {
        var route = await PlannedRouteAsync();
        var before = await StoredStateAsync(route.Id);

        await new RouteSnapshotService(_routes).CaptureAsync(route.Id);

        Assert.Equal(before, await StoredStateAsync(route.Id));
    }

    [Fact]
    public async Task ARefusedRestore_ChangesNothingInTheStore()
    {
        var route = await PlannedRouteAsync();
        var originator = new RouteSnapshotService(_routes);
        var memento = await originator.CaptureAsync(route.Id);
        await OptimizeAsync(route.Id);
        var stored = (await _routes.GetByIdAsync(route.Id))!;
        stored.Status = DeliveryRouteStatus.InProgress; // la ruta ya salió
        await _routes.UpdateAsync(stored);
        var optimized = await StoredStateAsync(route.Id);

        await Assert.ThrowsAsync<CommandNotUndoableException>(() => originator.RestoreStopOrderAsync(memento));

        Assert.Equal(optimized, await StoredStateAsync(route.Id));
    }
}
