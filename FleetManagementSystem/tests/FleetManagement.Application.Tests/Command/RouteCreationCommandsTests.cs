using FleetManagement.Application.Assignment;
using FleetManagement.Application.Commands;
using FleetManagement.Application.DTOs;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Xunit;
using static FleetManagement.Application.Tests.Command.CommandTestKit;

namespace FleetManagement.Application.Tests.Command;

/// <summary>
/// Las TRES formas de crear una ruta que el panel ofrece —formulario, «⚡ Ruta express» y «⧉ Duplicar»— pasan por Command y
/// quedan en el historial, de modo que «Deshacer» y «Rehacer» funcionan con cualquiera de ellas (antes solo el formulario
/// dejaba historial: crear una ruta express o duplicar una dejaba los dos botones apagados). Aquí se prueban las dos órdenes
/// nuevas una por una y, con <see cref="RouteCreationCommand"/> como base común, que las tres se comportan igual.
/// Deshacer ELIMINA la ruta creada (no la deja cancelada): así deshacer y rehacer repetidos no acumulan rutas.
/// </summary>
public class RouteCreationCommandsTests
{
    private readonly CommandTestKit _kit = new();

    private Task<T> RunAsync<T>(IUndoableCommand<T> command) => _kit.Invoker.ExecuteAsync(Admin, command);

    private async Task<string[]> UndoableAsync() => (await _kit.Invoker.GetHistoryAsync(Admin)).Undoable.Select(entry => entry.Description).ToArray();

    private async Task<string[]> RedoableAsync() => (await _kit.Invoker.GetHistoryAsync(Admin)).Redoable.Select(entry => entry.Description).ToArray();

    // Una ruta express necesita un vehículo en el que quepa su carga (50 kg) y un conductor.
    private static async Task<CreateExpressRouteRequest> ExpressRequestAsync(CommandTestKit kit, string name = "Entrega urgente")
    {
        var vehicle = await kit.Fleet.AddVehicleAsync("EXP-001");
        var driver = await kit.Fleet.AddDriverAsync("Ana Pérez");
        return new CreateExpressRouteRequest(name, 0, 0, 0, 0.5, vehicle.Id, driver.Id);
    }

    // ------------------------------------------------------------------ ruta express

    [Fact]
    public async Task CreateExpressRoute_CreatesAPlannedRoute_AndRecordsIt()
    {
        var request = await ExpressRequestAsync(_kit);

        var created = await RunAsync(_kit.Commands.CreateExpressRoute(request));

        var route = await _kit.RouteAsync(created.Id);
        Assert.Equal("Entrega urgente", route.Name);
        Assert.Equal(DeliveryRouteStatus.Planned, route.Status);
        Assert.Equal(request.AssignedVehicleId, route.AssignedVehicleId);
        Assert.Equal(request.AssignedDriverId, route.AssignedDriverId);
        Assert.Equal(new[] { "Crear la ruta express 'Entrega urgente'" }, await UndoableAsync());
    }

    [Fact]
    public async Task UndoingCreateExpressRoute_DeletesTheRoute_ItIsNotLeftCancelled()
    {
        var created = await RunAsync(_kit.Commands.CreateExpressRoute(await ExpressRequestAsync(_kit)));

        var step = await _kit.Invoker.UndoAsync(Admin);

        Assert.Equal("Crear la ruta express 'Entrega urgente'", step.Description);
        Assert.False(await _kit.RouteExistsAsync(created.Id));
        Assert.Empty(await _kit.AllRoutesAsync());
        Assert.Empty(await UndoableAsync());
        Assert.Equal(new[] { "Crear la ruta express 'Entrega urgente'" }, await RedoableAsync());
    }

    [Fact]
    public async Task RedoingCreateExpressRoute_CreatesANewRoute_WithTheSameData_AndTheOldOneIsGone()
    {
        var request = await ExpressRequestAsync(_kit);
        var first = await RunAsync(_kit.Commands.CreateExpressRoute(request));
        await _kit.Invoker.UndoAsync(Admin);

        await _kit.Invoker.RedoAsync(Admin);

        var again = Assert.Single(await _kit.AllRoutesAsync()); // una sola: la nueva
        Assert.False(await _kit.RouteExistsAsync(first.Id));
        Assert.Equal(DeliveryRouteStatus.Planned, again.Status);
        Assert.NotEqual(first.Id, again.Id);
        Assert.Equal("Entrega urgente", again.Name);
        Assert.Equal(request.AssignedVehicleId, again.AssignedVehicleId);
        Assert.Equal(request.AssignedDriverId, again.AssignedDriverId);
    }

    [Fact]
    public async Task CreateExpressRoute_ThatBreaksAnAssignmentRule_FailsAndIsNotRecorded()
    {
        var driver = await _kit.Fleet.AddDriverAsync();
        var inShop = await _kit.Fleet.AddVehicleAsync("TALLER", VehicleStatus.Maintenance);

        await Assert.ThrowsAsync<AssignmentRuleException>(() =>
            RunAsync(_kit.Commands.CreateExpressRoute(new CreateExpressRouteRequest("Entrega urgente", 0, 0, 0, 0.5, inShop.Id, driver.Id))));

        Assert.Empty(await _kit.AllRoutesAsync());
        Assert.Empty(await UndoableAsync());
    }

    // ------------------------------------------------------------------ duplicar una ruta

    [Fact]
    public async Task DuplicateRoute_CreatesAPlannedCopy_AndRecordsIt()
    {
        var original = await _kit.PlannedZigZagRouteAsync();

        var copy = await RunAsync(_kit.Commands.DuplicateRoute(original.Id, new DuplicateRouteRequest(null)));

        var stored = await _kit.RouteAsync(copy.Id);
        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal("Ruta zig-zag (copia)", stored.Name);
        Assert.Equal(DeliveryRouteStatus.Planned, stored.Status);
        Assert.Equal(new[] { "A", "B", "C" }, await _kit.StopLabelsAsync(copy.Id));
        Assert.Equal(new[] { "Duplicar la ruta como 'Ruta zig-zag (copia)'" }, await UndoableAsync());
    }

    [Fact]
    public async Task DuplicateRoute_UsesTheRequestedDate()
    {
        var original = await _kit.PlannedZigZagRouteAsync();
        var date = new DateTime(2030, 5, 17, 8, 0, 0, DateTimeKind.Utc);

        var copy = await RunAsync(_kit.Commands.DuplicateRoute(original.Id, new DuplicateRouteRequest(date)));

        Assert.Equal(date, (await _kit.RouteAsync(copy.Id)).ScheduledDate);
    }

    [Fact]
    public async Task TheDescriptionOfADuplicate_NamesTheCopyOnlyOnceItExists()
    {
        var original = await _kit.PlannedZigZagRouteAsync();
        var command = _kit.Commands.DuplicateRoute(original.Id, new DuplicateRouteRequest(null));
        Assert.Equal("Duplicar una ruta", command.Description);

        await RunAsync(command);

        Assert.Equal("Duplicar la ruta como 'Ruta zig-zag (copia)'", command.Description);
    }

    [Fact]
    public async Task UndoingDuplicateRoute_DeletesTheCopy_AndTheOriginalIsNotTouched()
    {
        var original = await _kit.PlannedZigZagRouteAsync();
        var copy = await RunAsync(_kit.Commands.DuplicateRoute(original.Id, new DuplicateRouteRequest(null)));

        await _kit.Invoker.UndoAsync(Admin);

        Assert.False(await _kit.RouteExistsAsync(copy.Id));
        var untouched = await _kit.RouteAsync(original.Id);
        Assert.Equal(DeliveryRouteStatus.Planned, untouched.Status);
        Assert.Equal(new[] { "A", "B", "C" }, await _kit.StopLabelsAsync(original.Id));
        Assert.Equal(original.Id, Assert.Single(await _kit.AllRoutesAsync()).Id); // solo queda la original
    }

    [Fact]
    public async Task RedoingDuplicateRoute_DuplicatesTheOriginalAgain_AndTheOldCopyIsGone()
    {
        var original = await _kit.PlannedZigZagRouteAsync();
        var firstCopy = await RunAsync(_kit.Commands.DuplicateRoute(original.Id, new DuplicateRouteRequest(null)));
        await _kit.Invoker.UndoAsync(Admin);

        await _kit.Invoker.RedoAsync(Admin);

        var routes = await _kit.AllRoutesAsync();
        Assert.Equal(2, routes.Count); // la original y la copia NUEVA
        Assert.False(await _kit.RouteExistsAsync(firstCopy.Id));
        Assert.Equal(DeliveryRouteStatus.Planned, (await _kit.RouteAsync(original.Id)).Status);
        Assert.Single(routes, route => route.Status == DeliveryRouteStatus.Planned && route.Id != original.Id);
        Assert.Single(await UndoableAsync());
        Assert.Empty(await RedoableAsync());
    }

    [Fact]
    public async Task RedoingDuplicateRoute_WhenTheOriginalNoLongerExists_IsRefused_AndRemovedFromTheHistory()
    {
        var original = await _kit.PlannedZigZagRouteAsync();
        await RunAsync(_kit.Commands.DuplicateRoute(original.Id, new DuplicateRouteRequest(null)));
        await _kit.Invoker.UndoAsync(Admin);
        await _kit.Fleet.Routes.DeleteAsync(original.Id); // la original desapareció mientras tanto

        var error = await Assert.ThrowsAsync<CommandNotRedoableException>(() => _kit.Invoker.RedoAsync(Admin));

        Assert.Contains("ya no existe", error.Message);
        Assert.Empty(await RedoableAsync());   // no queda un «Rehacer» atascado
        Assert.Empty(await UndoableAsync());
        Assert.Empty(await _kit.AllRoutesAsync());
    }

    [Fact]
    public async Task DuplicateRoute_OfAMissingRoute_IsNotFound_AndNotRecorded()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => RunAsync(_kit.Commands.DuplicateRoute(Guid.NewGuid(), new DuplicateRouteRequest(null))));

        Assert.Empty(await _kit.AllRoutesAsync());
        Assert.Empty(await UndoableAsync());
    }

    // ------------------------------------------------------------------ las tres formas se comportan igual

    /// <summary>Arma la orden de cada forma de crear una ruta con lo que necesite (vehículo y conductor, o una ruta que duplicar).</summary>
    private static async Task<IUndoableCommand<DeliveryRouteDto>> CommandForAsync(CommandTestKit kit, string way)
    {
        switch (way)
        {
            case "formulario":
                return kit.Commands.CreateRoute(NewRouteRequest("Por formulario"));
            case "express":
                return kit.Commands.CreateExpressRoute(await ExpressRequestAsync(kit));
            default:
                var original = await kit.PlannedZigZagRouteAsync("Ruta base");
                return kit.Commands.DuplicateRoute(original.Id, new DuplicateRouteRequest(null));
        }
    }

    [Theory]
    [InlineData("formulario")]
    [InlineData("express")]
    [InlineData("duplicar")]
    public async Task EveryWayOfCreatingARoute_AppearsInTheHistory_CanBeUndoneAndRedone(string way)
    {
        var command = await CommandForAsync(_kit, way);

        var created = await RunAsync(command);

        Assert.Single(await UndoableAsync());
        Assert.Empty(await RedoableAsync());
        Assert.Equal(DeliveryRouteStatus.Planned, (await _kit.RouteAsync(created.Id)).Status);

        await _kit.Invoker.UndoAsync(Admin);
        Assert.False(await _kit.RouteExistsAsync(created.Id)); // se eliminó: no quedó «cancelada»
        Assert.DoesNotContain(await _kit.AllRoutesAsync(), route => route.Name == created.Name);
        Assert.Empty(await UndoableAsync());
        Assert.Single(await RedoableAsync());

        await _kit.Invoker.RedoAsync(Admin);
        Assert.False(await _kit.RouteExistsAsync(created.Id)); // la primera sigue sin existir
        var again = Assert.Single(await _kit.AllRoutesAsync(), route => route.Name == created.Name); // y hay una sola, NUEVA
        Assert.Equal(DeliveryRouteStatus.Planned, again.Status);
        Assert.NotEqual(created.Id, again.Id);
        Assert.Single(await UndoableAsync());
        Assert.Empty(await RedoableAsync());

        await _kit.Invoker.UndoAsync(Admin); // ahora elimina la nueva, no la primera
        Assert.DoesNotContain(await _kit.AllRoutesAsync(), route => route.Name == created.Name);
    }

    [Theory]
    [InlineData("formulario")]
    [InlineData("express")]
    [InlineData("duplicar")]
    public async Task UndoingAndRedoingManyTimes_NeverAccumulatesRoutes_NorLeavesCancelledOnes(string way)
    {
        var command = await CommandForAsync(_kit, way);
        var created = await RunAsync(command);
        var before = (await _kit.AllRoutesAsync()).Count; // con la ruta creada (y la original, si se duplicó)

        for (var cycle = 1; cycle <= 5; cycle++)
        {
            await _kit.Invoker.UndoAsync(Admin);
            Assert.Equal(before - 1, (await _kit.AllRoutesAsync()).Count); // deshacer quita la ruta
            Assert.DoesNotContain(await _kit.AllRoutesAsync(), route => route.Name == created.Name);

            await _kit.Invoker.RedoAsync(Admin);
            Assert.Equal(before, (await _kit.AllRoutesAsync()).Count);       // rehacer deja exactamente una, nunca más
            Assert.DoesNotContain(await _kit.AllRoutesAsync(), route => route.Status == DeliveryRouteStatus.Cancelled);
        }
    }

    [Theory]
    [InlineData("formulario")]
    [InlineData("express")]
    [InlineData("duplicar")]
    public async Task EveryWayOfCreatingARoute_RefusesToUndo_OnceTheRouteLeft_AndChangesNothing(string way)
    {
        var created = await RunAsync(await CommandForAsync(_kit, way));
        await _kit.Routes.UpdateRouteStatusAsync(created.Id, "InProgress");

        var error = await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Invoker.UndoAsync(Admin));

        Assert.Contains($"'{created.Name}' ya no está planificada (está en progreso)", error.Message);
        Assert.Equal(DeliveryRouteStatus.InProgress, (await _kit.RouteAsync(created.Id)).Status);
        Assert.Empty(await UndoableAsync());   // ya no se puede deshacer nunca: se quitó del historial
        Assert.Empty(await RedoableAsync());
    }

    [Theory]
    [InlineData("formulario")]
    [InlineData("express")]
    [InlineData("duplicar")]
    public async Task ACreationCommandThatNeverRan_HasNoEffect_AndCannotBeUndone(string way)
    {
        var command = await CommandForAsync(_kit, way);

        Assert.False(command.HasEffect);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => command.UndoAsync());
        Assert.Contains("todavía no se ejecutó", error.Message);
    }

    [Fact]
    public async Task RoutesCreatedByDifferentWays_AreUndoneOneByOne_InReverseOrder_EachDeletingOnlyItsOwnRoute()
    {
        var original = await _kit.PlannedZigZagRouteAsync("Ruta base");
        var request = await ExpressRequestAsync(_kit);
        var byForm = await RunAsync(_kit.Commands.CreateRoute(NewRouteRequest("Por formulario")));
        var byExpress = await RunAsync(_kit.Commands.CreateExpressRoute(request));
        var byCopy = await RunAsync(_kit.Commands.DuplicateRoute(original.Id, new DuplicateRouteRequest(null)));

        Assert.Equal(
            new[] { "Duplicar la ruta como 'Ruta base (copia)'", "Crear la ruta express 'Entrega urgente'", "Crear la ruta 'Por formulario'" },
            await UndoableAsync());

        await _kit.Invoker.UndoAsync(Admin);
        Assert.False(await _kit.RouteExistsAsync(byCopy.Id));
        Assert.Equal(DeliveryRouteStatus.Planned, (await _kit.RouteAsync(byExpress.Id)).Status);
        Assert.Equal(DeliveryRouteStatus.Planned, (await _kit.RouteAsync(byForm.Id)).Status);

        await _kit.Invoker.UndoAsync(Admin);
        Assert.False(await _kit.RouteExistsAsync(byExpress.Id));
        Assert.Equal(DeliveryRouteStatus.Planned, (await _kit.RouteAsync(byForm.Id)).Status);

        await _kit.Invoker.UndoAsync(Admin);
        Assert.False(await _kit.RouteExistsAsync(byForm.Id));
        Assert.Equal(DeliveryRouteStatus.Planned, (await _kit.RouteAsync(original.Id)).Status); // la ruta base nunca se tocó
        Assert.Equal(original.Id, Assert.Single(await _kit.AllRoutesAsync()).Id);
        Assert.Empty(await UndoableAsync());
        Assert.Equal(3, (await _kit.Invoker.GetHistoryAsync(Admin)).Redoable.Count);
    }

    [Fact]
    public async Task TheCommandFactory_BuildsADifferentCommandEachTime_ForEveryWayOfCreatingARoute()
    {
        var request = await ExpressRequestAsync(_kit);
        var original = await _kit.PlannedZigZagRouteAsync();

        Assert.NotSame(_kit.Commands.CreateExpressRoute(request), _kit.Commands.CreateExpressRoute(request));
        Assert.NotSame(_kit.Commands.DuplicateRoute(original.Id, new DuplicateRouteRequest(null)), _kit.Commands.DuplicateRoute(original.Id, new DuplicateRouteRequest(null)));

        // Las tres comparten la misma base: así comparten también cómo se deshace y se rehace.
        Assert.IsType<CreateRouteCommand>(_kit.Commands.CreateRoute(NewRouteRequest()));
        Assert.IsType<CreateExpressRouteCommand>(_kit.Commands.CreateExpressRoute(request));
        Assert.IsType<DuplicateRouteCommand>(_kit.Commands.DuplicateRoute(original.Id, new DuplicateRouteRequest(null)));
        Assert.IsAssignableFrom<RouteCreationCommand>(_kit.Commands.CreateRoute(NewRouteRequest()));
        Assert.IsAssignableFrom<RouteCreationCommand>(_kit.Commands.CreateExpressRoute(request));
        Assert.IsAssignableFrom<RouteCreationCommand>(_kit.Commands.DuplicateRoute(original.Id, new DuplicateRouteRequest(null)));
    }
}
