using FleetManagement.Application.Commands;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Infrastructure.Commands;
using Xunit;

namespace FleetManagement.Application.Tests.Command;

/// <summary>
/// El invocador del patrón Command con órdenes de mentira (<see cref="ScriptedCommand"/>): ejecutar, deshacer y rehacer,
/// el historial de cada usuario y qué pasa cuando algo falla. Aquí no hay rutas ni vehículos: el invocador no sabe qué
/// hace cada orden, y estas pruebas lo demuestran.
/// </summary>
public class CommandInvokerTests
{
    private const string Ana = "ana";
    private const string Luis = "luis";

    private readonly InMemoryCommandHistoryStore _store = new();
    private readonly UserRecordingAuditLogger _audit = new();
    private readonly ManualTimeProvider _clock = new();

    private CommandInvoker Invoker() => new(_store, _audit, _clock);

    private static IEnumerable<string> Descriptions(IEnumerable<FleetManagement.Application.DTOs.CommandEntryDto> entries) => entries.Select(entry => entry.Description);

    // ------------------------------------------------------------------ ejecutar

    [Fact]
    public async Task Execute_RunsTheCommandOnce_AndReturnsItsResult()
    {
        var command = new ScriptedCommand("A");

        var result = await Invoker().ExecuteAsync(Ana, command);

        Assert.Equal(1, command.ExecuteCalls);
        Assert.Equal(1, result);
        Assert.Equal(0, command.UndoCalls);
    }

    [Fact]
    public async Task Execute_RecordsTheCommandInTheHistory_WithTheTimeOfTheClock()
    {
        var invoker = Invoker();
        _clock.Advance(TimeSpan.FromMinutes(7));

        await invoker.ExecuteAsync(Ana, new ScriptedCommand("A"));

        var entry = Assert.Single((await invoker.GetHistoryAsync(Ana)).Undoable);
        Assert.Equal("A", entry.Description);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime, entry.ExecutedAt);
    }

    [Fact]
    public async Task Execute_LeavesAnAuditEntry_WithTheUser()
    {
        await Invoker().ExecuteAsync(Ana, new ScriptedCommand("Crear la ruta 'Norte'"));

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("Comando", entry.Category);
        Assert.Equal("Ejecutó: Crear la ruta 'Norte'.", entry.Message);
        Assert.Equal(Ana, entry.User);
    }

    [Fact]
    public async Task Execute_WhenTheCommandFails_RecordsNothing_AndLetsTheErrorThrough()
    {
        var invoker = Invoker();
        var failing = new ScriptedCommand("A") { ExecuteFailure = new InvalidOperationException("regla incumplida") };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => invoker.ExecuteAsync(Ana, failing));

        Assert.Equal("regla incumplida", error.Message);
        Assert.Empty((await invoker.GetHistoryAsync(Ana)).Undoable);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task Execute_WhenTheCommandChangedNothing_DoesNotRecordIt()
    {
        var invoker = Invoker();

        await invoker.ExecuteAsync(Ana, new ScriptedCommand("A") { HasEffect = false });

        Assert.Empty((await invoker.GetHistoryAsync(Ana)).Undoable);
        Assert.Empty(_audit.Entries);
    }

    // ------------------------------------------------------------------ deshacer

    [Fact]
    public async Task Undo_UndoesTheMostRecentOperation_AndMakesItRedoable()
    {
        var invoker = Invoker();
        var first = new ScriptedCommand("A");
        var second = new ScriptedCommand("B");
        await invoker.ExecuteAsync(Ana, first);
        await invoker.ExecuteAsync(Ana, second);

        var step = await invoker.UndoAsync(Ana);

        Assert.Equal("B", step.Description);
        Assert.Equal(1, second.UndoCalls);
        Assert.Equal(0, first.UndoCalls);
        Assert.Equal(new[] { "A" }, Descriptions(step.History.Undoable));
        Assert.Equal(new[] { "B" }, Descriptions(step.History.Redoable));
    }

    [Fact]
    public async Task Undo_UndoesInReverseOrder()
    {
        var journal = new List<string>();
        var invoker = Invoker();
        foreach (var name in new[] { "A", "B", "C" })
            await invoker.ExecuteAsync(Ana, new ScriptedCommand(name, journal));
        journal.Clear();

        await invoker.UndoAsync(Ana);
        await invoker.UndoAsync(Ana);
        await invoker.UndoAsync(Ana);

        Assert.Equal(new[] { "deshacer C", "deshacer B", "deshacer A" }, journal);
    }

    [Fact]
    public async Task Undo_LeavesAnAuditEntry_WithTheUser()
    {
        var invoker = Invoker();
        await invoker.ExecuteAsync(Ana, new ScriptedCommand("A"));

        await invoker.UndoAsync(Ana);

        var entry = _audit.Entries[^1];
        Assert.Equal("Deshizo: A.", entry.Message);
        Assert.Equal(Ana, entry.User);
    }

    [Fact]
    public async Task Undo_WithAnEmptyHistory_SaysThereIsNothingToUndo()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Invoker().UndoAsync(Ana));

        Assert.Equal("No hay nada que deshacer.", error.Message);
    }

    [Fact]
    public async Task Undo_OfAnOperationThatCanNoLongerBeUndone_RemovesItFromTheHistory_AndSaysWhy()
    {
        var invoker = Invoker();
        var older = new ScriptedCommand("A");
        var stale = new ScriptedCommand("B") { UndoFailure = new CommandNotUndoableException("la ruta ya salió") };
        await invoker.ExecuteAsync(Ana, older);
        await invoker.ExecuteAsync(Ana, stale);

        var error = await Assert.ThrowsAsync<CommandNotUndoableException>(() => invoker.UndoAsync(Ana));

        Assert.Equal("la ruta ya salió", error.Message);
        var history = await invoker.GetHistoryAsync(Ana);
        Assert.Equal(new[] { "A" }, Descriptions(history.Undoable));   // B ya no estorba: lo siguiente por deshacer es A
        Assert.Empty(history.Redoable);                                 // y B tampoco quedó como rehacible
        Assert.Contains(_audit.Entries, entry => entry.Message.StartsWith("No se pudo deshacer: B.") && entry.Message.Contains("la ruta ya salió") && entry.User == Ana);
    }

    [Fact]
    public async Task Undo_WhenTheFailureIsNotAStateChange_KeepsTheOperationSoItCanBeRetried()
    {
        var invoker = Invoker();
        var command = new ScriptedCommand("A") { UndoFailure = new TimeoutException("la base no respondió") };
        await invoker.ExecuteAsync(Ana, command);

        await Assert.ThrowsAsync<TimeoutException>(() => invoker.UndoAsync(Ana));
        command.UndoFailure = null;
        await invoker.UndoAsync(Ana);

        Assert.Equal(2, command.UndoCalls);
        Assert.Empty((await invoker.GetHistoryAsync(Ana)).Undoable);
    }

    // ------------------------------------------------------------------ rehacer

    [Fact]
    public async Task Redo_RedoesTheLastUndoneOperation_AndMakesItUndoableAgain()
    {
        var invoker = Invoker();
        var command = new ScriptedCommand("A");
        await invoker.ExecuteAsync(Ana, command);
        await invoker.UndoAsync(Ana);

        var step = await invoker.RedoAsync(Ana);

        Assert.Equal("A", step.Description);
        Assert.Equal(1, command.RedoCalls);
        Assert.Equal(new[] { "A" }, Descriptions(step.History.Undoable));
        Assert.Empty(step.History.Redoable);
    }

    [Fact]
    public async Task Redo_RedoesInTheOrderTheyWereUndone_LastUndoneFirst()
    {
        var journal = new List<string>();
        var invoker = Invoker();
        foreach (var name in new[] { "A", "B" })
            await invoker.ExecuteAsync(Ana, new ScriptedCommand(name, journal));
        await invoker.UndoAsync(Ana); // deshace B
        await invoker.UndoAsync(Ana); // deshace A
        journal.Clear();

        await invoker.RedoAsync(Ana);
        await invoker.RedoAsync(Ana);

        Assert.Equal(new[] { "rehacer A", "rehacer B" }, journal);
    }

    [Fact]
    public async Task Redo_LeavesAnAuditEntry_WithTheUser()
    {
        var invoker = Invoker();
        await invoker.ExecuteAsync(Ana, new ScriptedCommand("A"));
        await invoker.UndoAsync(Ana);

        await invoker.RedoAsync(Ana);

        Assert.Equal("Rehízo: A.", _audit.Entries[^1].Message);
        Assert.Equal(Ana, _audit.Entries[^1].User);
    }

    [Fact]
    public async Task Redo_WithNothingUndone_SaysThereIsNothingToRedo()
    {
        var invoker = Invoker();
        await invoker.ExecuteAsync(Ana, new ScriptedCommand("A"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => invoker.RedoAsync(Ana));

        Assert.Equal("No hay nada que rehacer.", error.Message);
    }

    [Fact]
    public async Task ANewOperation_AfterAnUndo_MakesTheUndoneOneUnrecoverable()
    {
        var invoker = Invoker();
        await invoker.ExecuteAsync(Ana, new ScriptedCommand("A"));
        await invoker.UndoAsync(Ana);

        await invoker.ExecuteAsync(Ana, new ScriptedCommand("B"));

        var history = await invoker.GetHistoryAsync(Ana);
        Assert.Empty(history.Redoable);
        await Assert.ThrowsAsync<InvalidOperationException>(() => invoker.RedoAsync(Ana));
    }

    [Fact]
    public async Task Redo_WhenItFails_KeepsTheOperationSoItCanBeRetried()
    {
        var invoker = Invoker();
        var command = new ScriptedCommand("A");
        await invoker.ExecuteAsync(Ana, command);
        await invoker.UndoAsync(Ana);
        command.RedoFailure = new InvalidOperationException("ya no hay vehículos");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => invoker.RedoAsync(Ana));

        Assert.Equal("ya no hay vehículos", error.Message);
        Assert.Equal(new[] { "A" }, Descriptions((await invoker.GetHistoryAsync(Ana)).Redoable));
        Assert.Empty((await invoker.GetHistoryAsync(Ana)).Undoable);

        command.RedoFailure = null;
        await invoker.RedoAsync(Ana);
        Assert.Equal(new[] { "A" }, Descriptions((await invoker.GetHistoryAsync(Ana)).Undoable));
    }

    [Fact]
    public async Task Redo_OfAnOperationThatCanNoLongerBeRedone_RemovesItFromTheHistory_AndSaysWhy()
    {
        var invoker = Invoker();
        var stale = new ScriptedCommand("B") { RedoFailure = new CommandNotRedoableException("la ruta ya no existe") };
        await invoker.ExecuteAsync(Ana, new ScriptedCommand("A"));
        await invoker.ExecuteAsync(Ana, stale);
        await invoker.UndoAsync(Ana); // B
        await invoker.UndoAsync(Ana); // A: ahora hay que rehacer A y después B
        await invoker.RedoAsync(Ana);

        var error = await Assert.ThrowsAsync<CommandNotRedoableException>(() => invoker.RedoAsync(Ana));

        Assert.Equal("la ruta ya no existe", error.Message);
        var history = await invoker.GetHistoryAsync(Ana);
        Assert.Equal(new[] { "A" }, Descriptions(history.Undoable));
        Assert.Empty(history.Redoable);   // B no queda como un «Rehacer» atascado
        Assert.Equal(1, stale.RedoCalls);
        Assert.Contains(_audit.Entries, entry => entry.Message.StartsWith("No se pudo rehacer: B.") && entry.Message.Contains("la ruta ya no existe") && entry.User == Ana);
    }

    [Fact]
    public async Task Redo_WhenItChangedNothing_DoesNotLeaveAnythingToUndo()
    {
        var invoker = Invoker();
        var command = new ScriptedCommand("A");
        await invoker.ExecuteAsync(Ana, command);
        await invoker.UndoAsync(Ana);
        command.HasEffect = false; // al repetirla, ya no hubo nada que hacer

        await invoker.RedoAsync(Ana);

        var history = await invoker.GetHistoryAsync(Ana);
        Assert.Empty(history.Undoable);
        Assert.Empty(history.Redoable);
    }

    // ------------------------------------------------------------------ cada usuario con su historial

    [Fact]
    public async Task EachUser_HasTheirOwnHistory()
    {
        var invoker = Invoker();
        await invoker.ExecuteAsync(Ana, new ScriptedCommand("de Ana"));
        await invoker.ExecuteAsync(Luis, new ScriptedCommand("de Luis"));

        Assert.Equal(new[] { "de Ana" }, Descriptions((await invoker.GetHistoryAsync(Ana)).Undoable));
        Assert.Equal(new[] { "de Luis" }, Descriptions((await invoker.GetHistoryAsync(Luis)).Undoable));
    }

    [Fact]
    public async Task AUser_CannotUndoWhatAnotherUserDid()
    {
        var invoker = Invoker();
        var anas = new ScriptedCommand("de Ana");
        await invoker.ExecuteAsync(Ana, anas);

        await Assert.ThrowsAsync<InvalidOperationException>(() => invoker.UndoAsync(Luis));

        Assert.Equal(0, anas.UndoCalls);
        Assert.Single((await invoker.GetHistoryAsync(Ana)).Undoable);
    }

    [Fact]
    public async Task TheUserName_IsNotCaseSensitive()
    {
        var invoker = Invoker();
        await invoker.ExecuteAsync("Ana", new ScriptedCommand("A"));

        Assert.Single((await invoker.GetHistoryAsync("ANA")).Undoable);
    }

    // ------------------------------------------------------------------ dos peticiones a la vez

    [Fact]
    public async Task TwoSimultaneousUndos_OfTheSameUser_NeverUndoTheSameOperationTwice()
    {
        var invoker = Invoker();
        var gate = new TaskCompletionSource();
        var older = new ScriptedCommand("A");
        var newer = new ScriptedCommand("B") { UndoGate = gate.Task };
        await invoker.ExecuteAsync(Ana, older);
        await invoker.ExecuteAsync(Ana, newer);

        var firstUndo = invoker.UndoAsync(Ana);   // se queda esperando dentro de B
        var secondUndo = invoker.UndoAsync(Ana);  // debe esperar su turno
        await Task.Delay(50);
        Assert.Equal(1, newer.UndoCalls);
        Assert.Equal(0, older.UndoCalls);

        gate.SetResult();
        await Task.WhenAll(firstUndo, secondUndo);

        Assert.Equal(1, newer.UndoCalls);
        Assert.Equal(1, older.UndoCalls);
        var history = await invoker.GetHistoryAsync(Ana);
        Assert.Empty(history.Undoable);
        Assert.Equal(new[] { "A", "B" }, Descriptions(history.Redoable));
    }
}
