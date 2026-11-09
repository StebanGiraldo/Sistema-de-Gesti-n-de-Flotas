using FleetManagement.Application.Assignment;
using FleetManagement.Application.Commands;
using FleetManagement.Application.DTOs;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Xunit;
using static FleetManagement.Application.Tests.Command.CommandTestKit;

namespace FleetManagement.Application.Tests.Command;

/// <summary>
/// Las tres órdenes reales (crear una ruta, aplicar el orden optimizado, aplicar el plan de reparto) ejecutadas por el
/// invocador con los servicios reales: qué cambia al ejecutarlas, al deshacerlas y al rehacerlas, y cuándo se niegan a
/// deshacer o rehacer porque el mundo ya cambió. Deshacer una creación ELIMINA la ruta (no la deja cancelada).
/// </summary>
public class RouteCommandsTests
{
    private readonly CommandTestKit _kit = new();

    private Task<T> RunAsync<T>(IUndoableCommand<T> command) => _kit.Invoker.ExecuteAsync(Admin, command);

    private async Task<string[]> UndoableAsync() => (await _kit.Invoker.GetHistoryAsync(Admin)).Undoable.Select(entry => entry.Description).ToArray();

    private async Task<string[]> RedoableAsync() => (await _kit.Invoker.GetHistoryAsync(Admin)).Redoable.Select(entry => entry.Description).ToArray();

    private async Task<DeliveryRouteStatus[]> StatusesAsync() => (await _kit.AllRoutesAsync()).OrderBy(route => route.CreatedAt).Select(route => route.Status).ToArray();

    // ------------------------------------------------------------------ crear una ruta

    [Fact]
    public async Task CreateRoute_CreatesAPlannedRoute_AndRecordsIt()
    {
        var created = await RunAsync(_kit.Commands.CreateRoute(NewRouteRequest("Ruta Norte")));

        var route = await _kit.RouteAsync(created.Id);
        Assert.Equal("Ruta Norte", route.Name);
        Assert.Equal(DeliveryRouteStatus.Planned, route.Status);
        Assert.Equal(new[] { "Crear la ruta 'Ruta Norte'" }, await UndoableAsync());
    }

    [Fact]
    public async Task UndoingCreateRoute_DeletesTheRoute_ItIsNotLeftCancelled()
    {
        var created = await RunAsync(_kit.Commands.CreateRoute(NewRouteRequest()));

        var step = await _kit.Invoker.UndoAsync(Admin);

        Assert.Equal("Crear la ruta 'Ruta Norte'", step.Description);
        Assert.False(await _kit.RouteExistsAsync(created.Id));
        Assert.Empty(await _kit.AllRoutesAsync());
    }

    [Fact]
    public async Task RedoingCreateRoute_CreatesANewRoute_AndTheOldOneIsGone()
    {
        var first = await RunAsync(_kit.Commands.CreateRoute(NewRouteRequest()));
        await _kit.Invoker.UndoAsync(Admin);

        await _kit.Invoker.RedoAsync(Admin);

        var route = Assert.Single(await _kit.AllRoutesAsync()); // una sola: la nueva
        Assert.NotEqual(first.Id, route.Id);
        Assert.False(await _kit.RouteExistsAsync(first.Id));
        Assert.Equal(DeliveryRouteStatus.Planned, route.Status);
        Assert.Equal("Ruta Norte", route.Name);
    }

    [Fact]
    public async Task UndoingAgain_AfterARedo_DeletesTheNewRoute_AndNothingIsLeft()
    {
        await RunAsync(_kit.Commands.CreateRoute(NewRouteRequest()));
        await _kit.Invoker.UndoAsync(Admin);
        await _kit.Invoker.RedoAsync(Admin);

        await _kit.Invoker.UndoAsync(Admin);

        Assert.Empty(await _kit.AllRoutesAsync());
    }

    [Fact]
    public async Task UndoingAndRedoingManyTimes_NeverLeavesMoreThanOneRoute_NorACancelledOne()
    {
        await RunAsync(_kit.Commands.CreateRoute(NewRouteRequest()));

        for (var cycle = 1; cycle <= 5; cycle++)
        {
            await _kit.Invoker.UndoAsync(Admin);
            Assert.Empty(await _kit.AllRoutesAsync());

            await _kit.Invoker.RedoAsync(Admin);
            var route = Assert.Single(await _kit.AllRoutesAsync());
            Assert.Equal(DeliveryRouteStatus.Planned, route.Status);
        }
    }

    [Fact]
    public async Task UndoingCreateRoute_WhenTheRouteAlreadyLeft_IsRefused_AndChangesNothing()
    {
        var created = await RunAsync(_kit.Commands.CreateRoute(NewRouteRequest()));
        await _kit.Routes.UpdateRouteStatusAsync(created.Id, "InProgress");

        var error = await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Invoker.UndoAsync(Admin));

        Assert.Contains("'Ruta Norte' ya no está planificada (está en progreso)", error.Message);
        Assert.Contains("No se eliminó ninguna", error.Message);
        Assert.Equal(DeliveryRouteStatus.InProgress, (await _kit.RouteAsync(created.Id)).Status);
        Assert.Empty(await UndoableAsync());   // ya no se puede deshacer nunca: se quitó
        Assert.Empty(await RedoableAsync());
    }

    [Fact]
    public async Task CreateRoute_ThatBreaksAnAssignmentRule_FailsAndIsNotRecorded()
    {
        var inShop = await _kit.AddVanAsync("TALLER", status: VehicleStatus.Maintenance);

        await Assert.ThrowsAsync<AssignmentRuleException>(() => RunAsync(_kit.Commands.CreateRoute(NewRouteRequest(vehicleId: inShop.Id))));

        Assert.Empty(await _kit.AllRoutesAsync());
        Assert.Empty(await UndoableAsync());
    }

    // ------------------------------------------------------------------ aplicar el orden optimizado

    [Fact]
    public async Task ApplyOptimization_ReordersTheStops_AndRecordsIt()
    {
        var route = await _kit.PlannedZigZagRouteAsync();

        var result = await RunAsync(_kit.Commands.ApplyOptimization(route.Id));

        Assert.True(result.Applied);
        Assert.Equal(new[] { "B", "C", "A" }, await _kit.StopLabelsAsync(route.Id));
        Assert.Equal(new[] { "Aplicar el orden optimizado de paradas a la ruta 'Ruta zig-zag'" }, await UndoableAsync());
    }

    [Fact]
    public async Task UndoingTheOptimization_RestoresTheOriginalOrderAndTheEstimates()
    {
        var route = await _kit.PlannedZigZagRouteAsync();
        await RunAsync(_kit.Commands.ApplyOptimization(route.Id));
        Assert.NotEqual(155.7, (await _kit.RouteAsync(route.Id)).EstimatedDistanceKm); // la optimización sí cambió el cálculo

        await _kit.Invoker.UndoAsync(Admin);

        var restored = await _kit.RouteAsync(route.Id);
        Assert.Equal(new[] { "A", "B", "C" }, await _kit.StopLabelsAsync(route.Id));
        Assert.Equal(new[] { "A", "B", "C" }, restored.Waypoints.Select(waypoint => waypoint.Label).ToArray()); // también la lista, no solo el número de orden
        Assert.Equal(155.7, restored.EstimatedDistanceKm);
        Assert.Equal(117, restored.EstimatedDurationMinutes);
        Assert.Equal(DeliveryRouteStatus.Planned, restored.Status);
    }

    [Fact]
    public async Task RedoingTheOptimization_AppliesItAgain()
    {
        var route = await _kit.PlannedZigZagRouteAsync();
        await RunAsync(_kit.Commands.ApplyOptimization(route.Id));
        await _kit.Invoker.UndoAsync(Admin);

        await _kit.Invoker.RedoAsync(Admin);

        Assert.Equal(new[] { "B", "C", "A" }, await _kit.StopLabelsAsync(route.Id));
        Assert.Single(await UndoableAsync());
        Assert.Empty(await RedoableAsync());
    }

    [Fact]
    public async Task UndoingTheOptimization_OfARouteThatAlreadyLeft_IsRefused_AndKeepsTheOptimizedOrder()
    {
        var route = await _kit.PlannedZigZagRouteAsync();
        await RunAsync(_kit.Commands.ApplyOptimization(route.Id));
        await _kit.Routes.UpdateRouteStatusAsync(route.Id, "InProgress");

        var error = await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Invoker.UndoAsync(Admin));

        Assert.Contains("'Ruta zig-zag' ya no está planificada (está en progreso)", error.Message);
        Assert.Equal(new[] { "B", "C", "A" }, await _kit.StopLabelsAsync(route.Id));
        Assert.Empty(await UndoableAsync());
    }

    [Fact]
    public async Task UndoingTheOptimization_WhenTheStopsChangedAfterwards_IsRefused()
    {
        var route = await _kit.PlannedZigZagRouteAsync();
        await RunAsync(_kit.Commands.ApplyOptimization(route.Id));
        var stored = await _kit.RouteAsync(route.Id);
        stored.Waypoints.Add(new Waypoint { Order = 3, Label = "D" });

        var error = await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Invoker.UndoAsync(Admin));

        Assert.Contains("Las paradas de la ruta 'Ruta zig-zag' cambiaron", error.Message);
        Assert.Equal(new[] { "B", "C", "A", "D" }, await _kit.StopLabelsAsync(route.Id));
    }

    [Fact]
    public async Task ApplyOptimization_OfARouteThatIsAlreadyOptimal_ChangesNothing_AndIsNotRecorded()
    {
        var route = await _kit.PlannedZigZagRouteAsync();
        await RunAsync(_kit.Commands.ApplyOptimization(route.Id));

        var again = await RunAsync(_kit.Commands.ApplyOptimization(route.Id));

        Assert.False(again.Applied);
        Assert.Single(await UndoableAsync()); // solo la primera vez
    }

    [Fact]
    public async Task ApplyOptimization_OfAMissingRoute_IsNotFound_AndNotRecorded()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => RunAsync(_kit.Commands.ApplyOptimization(Guid.NewGuid())));

        Assert.Empty(await UndoableAsync());
    }

    [Fact]
    public async Task ApplyOptimization_OfARouteThatAlreadyLeft_IsRefused_AndNotRecorded()
    {
        var route = await _kit.PlannedZigZagRouteAsync(status: DeliveryRouteStatus.InProgress);

        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(_kit.Commands.ApplyOptimization(route.Id)));

        Assert.Equal(new[] { "A", "B", "C" }, await _kit.StopLabelsAsync(route.Id));
        Assert.Empty(await UndoableAsync());
    }

    // ------------------------------------------------------------------ aplicar el plan de reparto

    private async Task TwoSmallVansAsync()
    {
        await _kit.AddVanAsync("A");
        await _kit.AddVanAsync("B");
    }

    [Fact]
    public async Task ApplyDispatchPlan_CreatesOnePlannedRoutePerTrip_AndRecordsIt()
    {
        await TwoSmallVansAsync();

        var result = await RunAsync(_kit.Commands.ApplyDispatchPlan(TwoHeavyDeliveries()));

        Assert.Equal(2, result.CreatedRoutes.Count);
        Assert.Equal(new[] { DeliveryRouteStatus.Planned, DeliveryRouteStatus.Planned }, await StatusesAsync());
        Assert.Equal(new[] { "Aplicar el plan de reparto (2 ruta(s) creada(s))" }, await UndoableAsync());
    }

    [Fact]
    public async Task UndoingTheDispatchPlan_DeletesEveryRouteItCreated_AndNothingElse()
    {
        await TwoSmallVansAsync();
        var untouched = await _kit.PlannedZigZagRouteAsync("Otra ruta");
        var result = await RunAsync(_kit.Commands.ApplyDispatchPlan(TwoHeavyDeliveries()));
        Assert.Equal(3, (await _kit.AllRoutesAsync()).Count);

        await _kit.Invoker.UndoAsync(Admin);

        var remaining = Assert.Single(await _kit.AllRoutesAsync()); // solo queda la ruta que no era del plan
        Assert.Equal(untouched.Id, remaining.Id);
        Assert.Equal(DeliveryRouteStatus.Planned, remaining.Status);
        foreach (var created in result.CreatedRoutes)
            Assert.False(await _kit.RouteExistsAsync(created.Id));
    }

    [Fact]
    public async Task RedoingTheDispatchPlan_AppliesItAgain_AndTheOldRoutesAreGone()
    {
        await TwoSmallVansAsync();
        var first = await RunAsync(_kit.Commands.ApplyDispatchPlan(TwoHeavyDeliveries()));
        await _kit.Invoker.UndoAsync(Admin);

        await _kit.Invoker.RedoAsync(Admin);

        var routes = await _kit.AllRoutesAsync();
        Assert.Equal(2, routes.Count); // las del plan rehecho; las anteriores ya no están
        Assert.All(routes, route => Assert.Equal(DeliveryRouteStatus.Planned, route.Status));
        Assert.DoesNotContain(routes, route => first.CreatedRoutes.Any(old => old.Id == route.Id));
        Assert.Single(await UndoableAsync());
    }

    [Fact]
    public async Task UndoingAndRedoingTheDispatchPlanManyTimes_NeverAccumulatesRoutes()
    {
        await TwoSmallVansAsync();
        await RunAsync(_kit.Commands.ApplyDispatchPlan(TwoHeavyDeliveries()));

        for (var cycle = 1; cycle <= 4; cycle++)
        {
            await _kit.Invoker.UndoAsync(Admin);
            Assert.Empty(await _kit.AllRoutesAsync());

            await _kit.Invoker.RedoAsync(Admin);
            var routes = await _kit.AllRoutesAsync();
            Assert.Equal(2, routes.Count);
            Assert.All(routes, route => Assert.Equal(DeliveryRouteStatus.Planned, route.Status));
        }
    }

    [Fact]
    public async Task UndoingTheDispatchPlan_IsAllOrNothing_WhenOneOfItsRoutesAlreadyLeft()
    {
        await TwoSmallVansAsync();
        var result = await RunAsync(_kit.Commands.ApplyDispatchPlan(TwoHeavyDeliveries()));
        var departed = result.CreatedRoutes[0];
        var waiting = result.CreatedRoutes[1];
        await _kit.Routes.UpdateRouteStatusAsync(departed.Id, "InProgress");

        var error = await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Invoker.UndoAsync(Admin));

        Assert.Contains($"'{departed.Name}' ya no está planificada", error.Message);
        Assert.DoesNotContain($"'{waiting.Name}'", error.Message);
        Assert.Equal(DeliveryRouteStatus.InProgress, (await _kit.RouteAsync(departed.Id)).Status);
        Assert.Equal(DeliveryRouteStatus.Planned, (await _kit.RouteAsync(waiting.Id)).Status); // la que sí se podía eliminar, no se tocó
    }

    [Fact]
    public async Task ApplyDispatchPlan_WhenNothingCanBeAssigned_FailsAndIsNotRecorded()
    {
        // No hay vehículos en la flota.
        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(_kit.Commands.ApplyDispatchPlan(TwoHeavyDeliveries())));

        Assert.Empty(await _kit.AllRoutesAsync());
        Assert.Empty(await UndoableAsync());
    }

    [Fact]
    public async Task RedoingTheDispatchPlan_WhenTheFleetIsBusyNow_FailsAndKeepsItRedoable()
    {
        var van = await _kit.AddVanAsync("A", capacityKg: 100);
        var request = new FleetManagement.Application.DTOs.DispatchPlanRequest(0, 0, new List<FleetManagement.Application.DTOs.DispatchDeliveryRequest> { new("Entrega 1", 6, 0, null, 0, 0.1) });
        await RunAsync(_kit.Commands.ApplyDispatchPlan(request));
        await _kit.Invoker.UndoAsync(Admin);
        var vehicle = (await _kit.Fleet.Vehicles.GetByIdAsync(van.Id))!;
        vehicle.Status = VehicleStatus.Maintenance; // mientras tanto, la furgoneta entró al taller

        await Assert.ThrowsAsync<InvalidOperationException>(() => _kit.Invoker.RedoAsync(Admin));

        Assert.Single(await RedoableAsync());
        Assert.Empty(await UndoableAsync());
    }

    // ------------------------------------------------------------------ varias órdenes mezcladas

    [Fact]
    public async Task DifferentKindsOfOperations_AreUndoneInReverseOrder()
    {
        var zigZag = await _kit.PlannedZigZagRouteAsync();
        await RunAsync(_kit.Commands.ApplyOptimization(zigZag.Id));
        var created = await RunAsync(_kit.Commands.CreateRoute(NewRouteRequest("Ruta nueva")));

        var first = await _kit.Invoker.UndoAsync(Admin);
        Assert.Equal("Crear la ruta 'Ruta nueva'", first.Description);
        Assert.False(await _kit.RouteExistsAsync(created.Id));
        Assert.Equal(new[] { "B", "C", "A" }, await _kit.StopLabelsAsync(zigZag.Id)); // la optimización sigue puesta

        var second = await _kit.Invoker.UndoAsync(Admin);
        Assert.Equal("Aplicar el orden optimizado de paradas a la ruta 'Ruta zig-zag'", second.Description);
        Assert.Equal(new[] { "A", "B", "C" }, await _kit.StopLabelsAsync(zigZag.Id));
    }

    // ------------------------------------------------------------------ órdenes que dependen de una ruta que ya no existe

    private static CreateDeliveryRouteRequest ZigZagRequest()
        => new("Ruta con paradas", 0, 0, 0, 0.6, new List<WaypointDto> { new(0, 0.5, "A", 0), new(0, 0.1, "B", 1), new(0, 0.3, "C", 2) }, null, null, null, null);

    [Fact]
    public async Task UndoingTheOptimization_OfARouteThatNoLongerExists_IsRefused_AndRemovedFromTheHistory()
    {
        var route = await _kit.PlannedZigZagRouteAsync();
        await RunAsync(_kit.Commands.ApplyOptimization(route.Id));
        await _kit.Fleet.Routes.DeleteAsync(route.Id); // la ruta desapareció (por ejemplo, otra orden deshizo su creación)

        var error = await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Invoker.UndoAsync(Admin));

        Assert.Contains("La ruta 'Ruta zig-zag' ya no existe", error.Message);
        Assert.Empty(await UndoableAsync());   // no queda un «Deshacer» atascado
        Assert.Empty(await RedoableAsync());
    }

    [Fact]
    public async Task RedoingAnOptimization_OfARouteThatWasDeletedByUndoingItsCreation_IsRefused_AndRemovedFromTheHistory()
    {
        var created = await RunAsync(_kit.Commands.CreateRoute(ZigZagRequest()));
        var optimized = await RunAsync(_kit.Commands.ApplyOptimization(created.Id));
        Assert.True(optimized.Applied);
        await _kit.Invoker.UndoAsync(Admin); // restaura el orden de las paradas
        await _kit.Invoker.UndoAsync(Admin); // elimina la ruta
        await _kit.Invoker.RedoAsync(Admin); // la vuelve a crear: es una ruta NUEVA, con otro Id

        var error = await Assert.ThrowsAsync<CommandNotRedoableException>(() => _kit.Invoker.RedoAsync(Admin));

        Assert.Contains("La ruta 'Ruta con paradas' ya no existe", error.Message);
        Assert.Empty(await RedoableAsync());   // no queda un «Rehacer» atascado
        Assert.Equal(new[] { "Crear la ruta 'Ruta con paradas'" }, await UndoableAsync());
        var onlyRoute = Assert.Single(await _kit.AllRoutesAsync()); // solo la ruta nueva
        Assert.NotEqual(created.Id, onlyRoute.Id);
    }

    [Fact]
    public async Task RedoingAnOptimization_WhenTheRouteStillExists_StillWorks()
    {
        var created = await RunAsync(_kit.Commands.CreateRoute(ZigZagRequest()));
        await RunAsync(_kit.Commands.ApplyOptimization(created.Id));
        await _kit.Invoker.UndoAsync(Admin); // solo se deshace la optimización: la ruta sigue

        await _kit.Invoker.RedoAsync(Admin);

        Assert.Equal(new[] { "B", "C", "A" }, await _kit.StopLabelsAsync(created.Id));
        Assert.Equal(2, (await UndoableAsync()).Length);
    }

    [Fact]
    public void TheCommandFactory_BuildsADifferentCommandEachTime()
    {
        var first = _kit.Commands.CreateRoute(NewRouteRequest());
        var second = _kit.Commands.CreateRoute(NewRouteRequest());

        Assert.NotSame(first, second);
    }
}
