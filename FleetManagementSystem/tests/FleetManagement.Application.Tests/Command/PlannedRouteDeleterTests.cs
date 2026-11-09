using FleetManagement.Application.Commands;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Command;

/// <summary>
/// Eliminar las rutas que creó una orden al deshacerla: todas o ninguna, y solo si siguen planificadas. Se eliminan de
/// verdad —no quedan «canceladas»—, así deshacer y rehacer repetidos no acumulan rutas.
/// </summary>
public class PlannedRouteDeleterTests
{
    private readonly CommandTestKit _kit = new();

    private async Task<DeliveryRouteStatus> StatusOfAsync(Guid routeId) => (await _kit.RouteAsync(routeId)).Status;

    [Fact]
    public async Task DeletesEveryPlannedRoute_NoneIsLeftCancelled()
    {
        var first = await _kit.PlannedZigZagRouteAsync("Ruta 1");
        var second = await _kit.PlannedZigZagRouteAsync("Ruta 2");

        await _kit.Deleter.DeleteAsync(new[] { first.Id, second.Id });

        Assert.False(await _kit.RouteExistsAsync(first.Id));
        Assert.False(await _kit.RouteExistsAsync(second.Id));
        Assert.Empty(await _kit.AllRoutesAsync());
    }

    [Fact]
    public async Task DeletesOnlyTheRoutesItIsGiven()
    {
        var mine = await _kit.PlannedZigZagRouteAsync("Mía");
        var other = await _kit.PlannedZigZagRouteAsync("De otra orden");

        await _kit.Deleter.DeleteAsync(new[] { mine.Id });

        Assert.False(await _kit.RouteExistsAsync(mine.Id));
        Assert.Equal(DeliveryRouteStatus.Planned, await StatusOfAsync(other.Id));
        Assert.Single(await _kit.AllRoutesAsync());
    }

    [Fact]
    public async Task DeletesNone_WhenOneOfThemAlreadyLeft_AndNamesIt()
    {
        var waiting = await _kit.PlannedZigZagRouteAsync("Ruta que espera");
        var departed = await _kit.PlannedZigZagRouteAsync("Ruta que salió", DeliveryRouteStatus.InProgress);

        var error = await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Deleter.DeleteAsync(new[] { waiting.Id, departed.Id }));

        Assert.Contains("la ruta 'Ruta que salió' ya no está planificada (está en progreso)", error.Message);
        Assert.Contains("No se eliminó ninguna", error.Message);
        Assert.DoesNotContain("Ruta que espera", error.Message);
        Assert.Equal(DeliveryRouteStatus.Planned, await StatusOfAsync(waiting.Id)); // sigue ahí, sin tocar
        Assert.Equal(DeliveryRouteStatus.InProgress, await StatusOfAsync(departed.Id));
    }

    [Fact]
    public async Task NamesEveryRouteThatAlreadyMoved()
    {
        var one = await _kit.PlannedZigZagRouteAsync("Ruta 1", DeliveryRouteStatus.Completed);
        var two = await _kit.PlannedZigZagRouteAsync("Ruta 2", DeliveryRouteStatus.Delayed);

        var error = await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Deleter.DeleteAsync(new[] { one.Id, two.Id }));

        Assert.Contains("la ruta 'Ruta 1' ya no está planificada (está completada)", error.Message);
        Assert.Contains("la ruta 'Ruta 2' ya no está planificada (está retrasada)", error.Message);
        Assert.True(await _kit.RouteExistsAsync(one.Id));
        Assert.True(await _kit.RouteExistsAsync(two.Id));
    }

    [Fact]
    public async Task ARouteThatWasCancelledByHand_IsNotDeleted_ItIsNoLongerPlanned()
    {
        var cancelled = await _kit.PlannedZigZagRouteAsync("Ruta cancelada a mano", DeliveryRouteStatus.Cancelled);

        var error = await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Deleter.DeleteAsync(new[] { cancelled.Id }));

        Assert.Contains("la ruta 'Ruta cancelada a mano' ya no está planificada (está cancelada)", error.Message);
        Assert.True(await _kit.RouteExistsAsync(cancelled.Id));
    }

    [Fact]
    public async Task DeletesNone_WhenOneOfThemNoLongerExists()
    {
        var waiting = await _kit.PlannedZigZagRouteAsync();

        var error = await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Deleter.DeleteAsync(new[] { waiting.Id, Guid.NewGuid() }));

        Assert.Contains("ya no existe", error.Message);
        Assert.Equal(DeliveryRouteStatus.Planned, await StatusOfAsync(waiting.Id));
    }

    [Fact]
    public async Task AnEmptyList_DoesNothing()
    {
        var route = await _kit.PlannedZigZagRouteAsync();

        await _kit.Deleter.DeleteAsync(Array.Empty<Guid>());

        Assert.True(await _kit.RouteExistsAsync(route.Id));
        Assert.DoesNotContain(_kit.Audit.Entries, entry => entry.Message.Contains("eliminada"));
    }

    [Fact]
    public async Task DoesNotTouchTheVehicle_BecauseCreatingThePlannedRouteDidNotEither()
    {
        var van = await _kit.AddVanAsync("DISPONIBLE");
        var created = await _kit.Routes.CreateRouteAsync(CommandTestKit.NewRouteRequest(vehicleId: van.Id));

        await _kit.Deleter.DeleteAsync(new[] { created.Id });

        Assert.False(await _kit.RouteExistsAsync(created.Id));
        Assert.Equal(VehicleStatus.Available, (await _kit.Fleet.Vehicles.GetByIdAsync(van.Id))!.Status);
    }

    [Fact]
    public async Task DoesNotFreeAVehicleThatIsOnAnotherTrip()
    {
        var van = await _kit.AddVanAsync("EN-VIAJE", status: VehicleStatus.EnRoute);
        var trip = await _kit.Fleet.AddRouteAsync("Viaje en curso", DeliveryRouteStatus.InProgress, van);
        var planned = await _kit.PlannedZigZagRouteAsync("Planificada con el mismo vehículo");
        (await _kit.RouteAsync(planned.Id)).AssignedVehicleId = van.Id;

        await _kit.Deleter.DeleteAsync(new[] { planned.Id });

        Assert.Equal(VehicleStatus.EnRoute, (await _kit.Fleet.Vehicles.GetByIdAsync(van.Id))!.Status);
        Assert.Equal(DeliveryRouteStatus.InProgress, await StatusOfAsync(trip.Id));
    }

    [Fact]
    public async Task LeavesAnEntryInTheAuditLog_ForEveryDeletedRoute()
    {
        var first = await _kit.PlannedZigZagRouteAsync("Ruta 1");
        var second = await _kit.PlannedZigZagRouteAsync("Ruta 2");

        await _kit.Deleter.DeleteAsync(new[] { first.Id, second.Id });

        Assert.Contains(_kit.Audit.Entries, entry => entry.Category == "Ruta" && entry.Message == "Ruta 'Ruta 1' eliminada al deshacer su creación.");
        Assert.Contains(_kit.Audit.Entries, entry => entry.Category == "Ruta" && entry.Message == "Ruta 'Ruta 2' eliminada al deshacer su creación.");
    }

    [Fact]
    public async Task WhenItRefuses_ItLeavesNothingInTheAuditLog()
    {
        var waiting = await _kit.PlannedZigZagRouteAsync("Ruta que espera");
        var departed = await _kit.PlannedZigZagRouteAsync("Ruta que salió", DeliveryRouteStatus.InProgress);

        await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Deleter.DeleteAsync(new[] { waiting.Id, departed.Id }));

        Assert.DoesNotContain(_kit.Audit.Entries, entry => entry.Message.Contains("eliminada"));
    }
}
