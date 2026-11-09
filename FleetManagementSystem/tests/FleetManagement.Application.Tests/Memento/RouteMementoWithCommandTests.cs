using System.Reflection;
using FleetManagement.Application.Commands;
using FleetManagement.Application.Tests.Command;
using FleetManagement.Domain.Entities;
using Xunit;
using static FleetManagement.Application.Tests.Command.CommandTestKit;

namespace FleetManagement.Application.Tests.Memento;

/// <summary>
/// PRUEBAS DEL MEMENTO TRABAJANDO CON COMMAND. Los tres papeles: el Originator (<see cref="RouteSnapshotService"/>) crea y
/// restaura el recuerdo, el Memento (<see cref="RouteSnapshot"/>) lo guarda y el Caretaker
/// (<see cref="ApplyRouteOptimizationCommand"/>, y detrás de él el historial del invocador) lo conserva sin mirar dentro.
/// Se usan los servicios reales y el invocador real, con repositorios en memoria.
/// </summary>
public class RouteMementoWithCommandTests
{
    private const BindingFlags EverythingDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private readonly CommandTestKit _kit = new();

    private Task<T> RunAsync<T>(IUndoableCommand<T> command) => _kit.Invoker.ExecuteAsync(Admin, command);

    /// <summary>Paradas (por orden de visita), distancia y duración de la ruta tal como están guardadas.</summary>
    private async Task<string> StateAsync(Guid routeId)
    {
        var route = await _kit.RouteAsync(routeId);
        return $"{string.Join(",", route.Waypoints.OrderBy(waypoint => waypoint.Order).Select(waypoint => waypoint.Label))}|{route.EstimatedDistanceKm}|{route.EstimatedDurationMinutes}";
    }

    // ---------- 1. El Caretaker solo ve la interfaz estrecha ----------

    [Fact]
    public void TheCaretaker_HoldsTheMementoOnlyThroughTheNarrowInterface()
    {
        var command = typeof(ApplyRouteOptimizationCommand);
        var fieldTypes = command.GetFields(EverythingDeclared).Select(field => field.FieldType).ToList();

        Assert.Contains(typeof(IRouteMemento), fieldTypes);
        Assert.DoesNotContain(typeof(RouteSnapshot), fieldTypes);
    }

    [Fact]
    public void TheCaretaker_DoesNotLeakTheMementoOutOfItself()
    {
        // Ningún miembro público de la orden devuelve o recibe el recuerdo: solo el Originator lo recibe de vuelta.
        var memento = typeof(IRouteMemento);
        var publicMethods = typeof(ApplyRouteOptimizationCommand).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var publicProperties = typeof(ApplyRouteOptimizationCommand).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.All(publicMethods, method =>
        {
            Assert.False(memento.IsAssignableFrom(method.ReturnType), $"{method.Name} devuelve el recuerdo");
            Assert.All(method.GetParameters(), parameter => Assert.False(memento.IsAssignableFrom(parameter.ParameterType), $"{method.Name} recibe el recuerdo"));
        });
        Assert.All(publicProperties, property => Assert.False(memento.IsAssignableFrom(property.PropertyType), $"{property.Name} expone el recuerdo"));
    }

    [Fact]
    public void OnlyTheOriginator_UsesTheWideMementoInItsSignatures()
    {
        // Quien solo guarda el recuerdo (órdenes, fábrica, invocador, historial...) no declara el tipo ancho en ningún campo,
        // propiedad, constructor ni método; solo lo hacen el propio recuerdo y su Originator.
        var allowed = new[] { typeof(RouteSnapshot), typeof(RouteSnapshotService), typeof(IRouteSnapshotService) };
        var offenders = new List<string>();

        foreach (var type in typeof(RouteSnapshot).Assembly.GetTypes().Where(type => type.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() is null))
        {
            if (allowed.Contains(type))
                continue;

            var used = type.GetFields(EverythingDeclared).Select(field => field.FieldType)
                .Concat(type.GetProperties(EverythingDeclared).Select(property => property.PropertyType))
                .Concat(type.GetConstructors(EverythingDeclared).SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType)))
                .Concat(type.GetMethods(EverythingDeclared).SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType)));

            if (used.Any(candidate => candidate == typeof(RouteSnapshot) || candidate == typeof(Task<RouteSnapshot>)))
                offenders.Add(type.Name);
        }

        Assert.Empty(offenders);
    }

    // ---------- 2. Deshacer y Rehacer con el Memento ----------

    [Fact]
    public async Task Undo_GivesTheMementoBackToTheOriginator_WhichRestoresStopsAndEstimates()
    {
        var route = await _kit.PlannedZigZagRouteAsync();
        var original = await StateAsync(route.Id);
        await RunAsync(_kit.Commands.ApplyOptimization(route.Id));
        Assert.NotEqual(original, await StateAsync(route.Id));

        await _kit.Invoker.UndoAsync(Admin);

        Assert.Equal(original, await StateAsync(route.Id));
        Assert.Equal("A,B,C|155.7|117", await StateAsync(route.Id));
    }

    [Fact]
    public async Task UndoRedoUndoRedoUndo_EveryUndoReturnsToTheOriginalState_AndEveryRedoToTheOptimizedOne()
    {
        var route = await _kit.PlannedZigZagRouteAsync();
        var original = await StateAsync(route.Id);
        await RunAsync(_kit.Commands.ApplyOptimization(route.Id));
        var optimized = await StateAsync(route.Id);
        Assert.NotEqual(original, optimized);

        for (var cycle = 1; cycle <= 3; cycle++)
        {
            await _kit.Invoker.UndoAsync(Admin);
            Assert.Equal(original, await StateAsync(route.Id));

            await _kit.Invoker.RedoAsync(Admin);
            Assert.Equal(optimized, await StateAsync(route.Id));
        }

        await _kit.Invoker.UndoAsync(Admin);
        Assert.Equal(original, await StateAsync(route.Id));
    }

    [Fact]
    public async Task EveryExecution_TakesItsOwnMemento_SoUndoingARedoReturnsToTheStateRightBeforeThatRedo()
    {
        var route = await _kit.PlannedZigZagRouteAsync();
        await RunAsync(_kit.Commands.ApplyOptimization(route.Id));
        await _kit.Invoker.UndoAsync(Admin);

        // Entre deshacer y rehacer alguien ajusta a mano la estimación (la ruta sigue planificada y con las mismas paradas).
        (await _kit.RouteAsync(route.Id)).EstimatedDistanceKm = 999;

        await _kit.Invoker.RedoAsync(Admin);
        Assert.NotEqual(999, (await _kit.RouteAsync(route.Id)).EstimatedDistanceKm); // volvió a optimizar y recalculó

        await _kit.Invoker.UndoAsync(Admin);

        // El recuerdo del "rehacer" es el de ese momento (999), no el de la primera ejecución (155,7).
        Assert.Equal(999, (await _kit.RouteAsync(route.Id)).EstimatedDistanceKm);
        Assert.Equal(new[] { "A", "B", "C" }, await _kit.StopLabelsAsync(route.Id));
    }

    [Fact]
    public async Task TwoOptimizations_KeepTheirOwnMementos_AndAreUndoneInReverseOrder()
    {
        var first = await _kit.PlannedZigZagRouteAsync("Ruta 1");
        var second = await _kit.PlannedZigZagRouteAsync("Ruta 2");
        var original = await StateAsync(first.Id);
        await RunAsync(_kit.Commands.ApplyOptimization(first.Id));
        await RunAsync(_kit.Commands.ApplyOptimization(second.Id));
        var optimized = await StateAsync(first.Id);

        await _kit.Invoker.UndoAsync(Admin); // la última: Ruta 2

        Assert.Equal(original, await StateAsync(second.Id));
        Assert.Equal(optimized, await StateAsync(first.Id)); // la Ruta 1 no se tocó: su recuerdo es otro

        await _kit.Invoker.UndoAsync(Admin); // luego la primera: Ruta 1

        Assert.Equal(original, await StateAsync(first.Id));
    }

    // ---------- 3. El Originator se niega: el Caretaker no reconstruye nada por su cuenta ----------

    [Fact]
    public async Task WhenTheOriginatorRefuses_TheRouteKeepsItsOptimizedState_AndTheEntryLeavesTheHistory()
    {
        var route = await _kit.PlannedZigZagRouteAsync();
        await RunAsync(_kit.Commands.ApplyOptimization(route.Id));
        var optimized = await StateAsync(route.Id);
        await _kit.Routes.UpdateRouteStatusAsync(route.Id, "InProgress"); // la ruta ya salió: el recuerdo ya no aplica

        await Assert.ThrowsAsync<CommandNotUndoableException>(() => _kit.Invoker.UndoAsync(Admin));

        Assert.Equal(optimized, await StateAsync(route.Id));
        Assert.Empty((await _kit.Invoker.GetHistoryAsync(Admin)).Undoable);
    }

    [Fact]
    public async Task AnOptimizationThatChangedNothing_LeavesNoMementoInTheHistory()
    {
        var route = await _kit.PlannedZigZagRouteAsync();
        await RunAsync(_kit.Commands.ApplyOptimization(route.Id));
        await _kit.Invoker.UndoAsync(Admin);
        await _kit.Invoker.RedoAsync(Admin); // ya está optimizada

        var again = await RunAsync(_kit.Commands.ApplyOptimization(route.Id));

        Assert.False(again.Applied);
        Assert.Single((await _kit.Invoker.GetHistoryAsync(Admin)).Undoable); // solo la primera orden: la que cambió algo
    }

    [Fact]
    public async Task UndoingACommandThatNeverRan_FailsClearly_BecauseThereIsNoMementoYet_AndTouchesNothing()
    {
        var route = await _kit.PlannedZigZagRouteAsync();
        var before = await StateAsync(route.Id);
        var command = _kit.Commands.ApplyOptimization(route.Id); // creada pero sin ejecutar: el Caretaker todavía no tiene ningún recuerdo

        await Assert.ThrowsAsync<InvalidOperationException>(() => command.UndoAsync());

        Assert.Equal(before, await StateAsync(route.Id));
    }
}
