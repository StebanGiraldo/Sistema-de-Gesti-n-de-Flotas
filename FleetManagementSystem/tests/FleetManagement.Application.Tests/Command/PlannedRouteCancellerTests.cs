using FleetManagement.Application.Commands;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Command;

/// <summary>Cancelar las rutas que creó una orden: todas o ninguna, y solo si siguen planificadas.</summary>
public class PlannedRouteCancellerTests
{
    private readonly CommandTestKit _kit = new();

    private async Task<DeliveryRouteStatus> StatusOfAsync(Guid routeId) => (await _kit.RouteAsync(routeId)).Status;

    [Fact]
    public async Task CancelsEveryPlannedRoute()
    {
        var first = await _kit.PlannedZigZagRouteAsync("Ruta 1");
        var second = await _kit.PlannedZigZagRouteAsync("Ruta 2");

        await _kit.Canceller.CancelAsync(new[] { first.Id, second.Id });

        Assert.Equal(DeliveryRouteStatus.Cancelled, await StatusOfAsync(first.Id));
        Assert.Equal(DeliveryRouteStatus.Cancelled, await StatusOfAsync(second.Id));
    }

    [Fact]
    public async Task CancelsNone_WhenOneOfThemAlreadyLeft_AndNamesIt()
    {
        var waiting = await _kit.PlannedZigZagRouteAsync("Ruta que espera");
        var departed = await _kit.PlannedZigZagRouteAsync("Ruta que salió", DeliveryRouteStatus.InProgress);

        var error = await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Canceller.CancelAsync(new[] { waiting.Id, departed.Id }));

        Assert.Contains("la ruta 'Ruta que salió' ya no está planificada (está en progreso)", error.Message);
        Assert.DoesNotContain("Ruta que espera", error.Message);
        Assert.Equal(DeliveryRouteStatus.Planned, await StatusOfAsync(waiting.Id));
    }

    [Fact]
    public async Task NamesEveryRouteThatAlreadyMoved()
    {
        var one = await _kit.PlannedZigZagRouteAsync("Ruta 1", DeliveryRouteStatus.Completed);
        var two = await _kit.PlannedZigZagRouteAsync("Ruta 2", DeliveryRouteStatus.Delayed);

        var error = await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Canceller.CancelAsync(new[] { one.Id, two.Id }));

        Assert.Contains("la ruta 'Ruta 1' ya no está planificada (está completada)", error.Message);
        Assert.Contains("la ruta 'Ruta 2' ya no está planificada (está retrasada)", error.Message);
    }

    [Fact]
    public async Task CancelsNone_WhenOneOfThemNoLongerExists()
    {
        var waiting = await _kit.PlannedZigZagRouteAsync();

        var error = await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Canceller.CancelAsync(new[] { waiting.Id, Guid.NewGuid() }));

        Assert.Contains("ya no existe", error.Message);
        Assert.Equal(DeliveryRouteStatus.Planned, await StatusOfAsync(waiting.Id));
    }

    [Fact]
    public async Task AnEmptyList_DoesNothing()
        => await _kit.Canceller.CancelAsync(Array.Empty<Guid>());

    [Fact]
    public async Task GoesThroughTheRouteService_SoTheVehicleIsReleasedLikeInAManualCancel()
    {
        var van = await _kit.AddVanAsync("EN-RUTA", status: VehicleStatus.EnRoute);
        var route = (await _kit.Routes.CreateRouteAsync(CommandTestKit.NewRouteRequest(vehicleId: null)));
        var stored = await _kit.RouteAsync(route.Id);
        stored.AssignedVehicleId = van.Id;

        await _kit.Canceller.CancelAsync(new[] { route.Id });

        Assert.Equal(VehicleStatus.Available, (await _kit.Fleet.Vehicles.GetByIdAsync(van.Id))!.Status);
        Assert.Contains(_kit.Audit.Entries, entry => entry.Category == "Ruta" && entry.Message.Contains("cambió su estado a Cancelled"));
    }
}
