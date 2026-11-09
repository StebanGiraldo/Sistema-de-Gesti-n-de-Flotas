using FleetManagement.Application.Commands;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Command;

/// <summary>La "foto" del orden de las paradas (Memento) que permite deshacer una optimización.</summary>
public class RouteSnapshotServiceTests
{
    private readonly CommandTestKit _kit = new();

    private static void Reorder(DeliveryRoute route, params string[] labels)
    {
        for (var index = 0; index < labels.Length; index++)
            route.Waypoints.Single(waypoint => waypoint.Label == labels[index]).Order = index;
        route.Waypoints = route.Waypoints.OrderBy(waypoint => waypoint.Order).ToList();
    }

    [Fact]
    public async Task Capture_RemembersTheRoute_ItsStopsInOrder_AndItsEstimates()
    {
        var route = await _kit.PlannedZigZagRouteAsync();

        var snapshot = await _kit.Snapshots.CaptureAsync(route.Id);

        Assert.Equal(route.Id, snapshot.RouteId);
        Assert.Equal("Ruta zig-zag", snapshot.RouteName);
        Assert.Equal(new[] { 0, 1, 2 }, snapshot.Stops.Select(stop => stop.Order).ToArray());
        Assert.Equal(route.Waypoints.Select(waypoint => waypoint.Id).ToArray(), snapshot.Stops.Select(stop => stop.WaypointId).ToArray());
        Assert.Equal(155.7, snapshot.EstimatedDistanceKm);
        Assert.Equal(117, snapshot.EstimatedDurationMinutes);
    }

    [Fact]
    public async Task Capture_OfAMissingRoute_IsNotFound()
        => await Assert.ThrowsAsync<KeyNotFoundException>(() => _kit.Snapshots.CaptureAsync(Guid.NewGuid()));

    [Fact]
    public async Task TheSnapshot_IsNotAffectedByLaterChangesToTheRoute()
    {
        var route = await _kit.PlannedZigZagRouteAsync();
        var snapshot = await _kit.Snapshots.CaptureAsync(route.Id);

        Reorder(route, "C", "A", "B");
        route.EstimatedDistanceKm = 1;

        Assert.Equal(new[] { 0, 1, 2 }, snapshot.Stops.Select(stop => stop.Order).ToArray());
        Assert.Equal(155.7, snapshot.EstimatedDistanceKm);
    }

    [Fact]
    public async Task Restore_PutsTheStopsBackInTheirOriginalOrder_AndTheEstimatesBack()
    {
        var route = await _kit.PlannedZigZagRouteAsync();
        var snapshot = await _kit.Snapshots.CaptureAsync(route.Id);
        Reorder(route, "C", "A", "B");
        route.EstimatedDistanceKm = 1;
        route.EstimatedDurationMinutes = 2;

        await _kit.Snapshots.RestoreStopOrderAsync(snapshot);

        var restored = await _kit.RouteAsync(route.Id);
        Assert.Equal(new[] { "A", "B", "C" }, restored.Waypoints.Select(waypoint => waypoint.Label).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, restored.Waypoints.Select(waypoint => waypoint.Order).ToArray());
        Assert.Equal(155.7, restored.EstimatedDistanceKm);
        Assert.Equal(117, restored.EstimatedDurationMinutes);
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.InProgress, "en progreso")]
    [InlineData(DeliveryRouteStatus.Delayed, "retrasada")]
    [InlineData(DeliveryRouteStatus.Completed, "completada")]
    [InlineData(DeliveryRouteStatus.Cancelled, "cancelada")]
    public async Task Restore_IsRefused_WhenTheRouteIsNoLongerPlanned(DeliveryRouteStatus status, string described)
    {
        var route = await _kit.PlannedZigZagRouteAsync();
        var snapshot = await _kit.Snapshots.CaptureAsync(route.Id);
        Reorder(route, "C", "A", "B");
        route.Status = status;

        var error = await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Snapshots.RestoreStopOrderAsync(snapshot));

        Assert.Contains($"ya no está planificada (está {described})", error.Message);
        Assert.Equal(new[] { "C", "A", "B" }, await _kit.StopLabelsAsync(route.Id)); // no se tocó
    }

    [Fact]
    public async Task Restore_IsRefused_WhenAStopWasAdded()
    {
        var route = await _kit.PlannedZigZagRouteAsync();
        var snapshot = await _kit.Snapshots.CaptureAsync(route.Id);
        route.Waypoints.Add(new Waypoint { Order = 3, Label = "D" });

        await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Snapshots.RestoreStopOrderAsync(snapshot));
    }

    [Fact]
    public async Task Restore_IsRefused_WhenAStopWasRemoved()
    {
        var route = await _kit.PlannedZigZagRouteAsync();
        var snapshot = await _kit.Snapshots.CaptureAsync(route.Id);
        route.Waypoints.RemoveAt(0);

        await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Snapshots.RestoreStopOrderAsync(snapshot));
    }

    [Fact]
    public async Task Restore_IsRefused_WhenAStopWasReplacedByAnotherOne()
    {
        var route = await _kit.PlannedZigZagRouteAsync();
        var snapshot = await _kit.Snapshots.CaptureAsync(route.Id);
        route.Waypoints.RemoveAt(0);
        route.Waypoints.Add(new Waypoint { Order = 2, Label = "Otra" }); // misma cantidad, otra parada

        await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Snapshots.RestoreStopOrderAsync(snapshot));
    }

    [Fact]
    public async Task Restore_OfARouteThatNoLongerExists_IsRefused_AsNotUndoable()
    {
        // Antes una ruta nunca desaparecía (se cancelaba); ahora deshacer su creación la elimina, y deshacer algo que se
        // hizo sobre ella ya no es posible: se explica (409) y se quita del historial en vez de dar un 404 que deja el botón atascado.
        var route = await _kit.PlannedZigZagRouteAsync();
        var snapshot = await _kit.Snapshots.CaptureAsync(route.Id);
        await _kit.Fleet.Routes.DeleteAsync(route.Id);

        var error = await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Snapshots.RestoreStopOrderAsync(snapshot));

        Assert.Contains("La ruta 'Ruta zig-zag' ya no existe", error.Message);
    }
}
