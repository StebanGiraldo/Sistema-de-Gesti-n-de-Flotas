using FleetManagement.Application.Commands;
using Xunit;

namespace FleetManagement.Application.Tests.Command;

/// <summary>El historial de un usuario, solo: qué entra, qué se olvida y por qué dos operaciones suyas no pueden pisarse.</summary>
public class CommandHistoryTests
{
    private static readonly DateTime Start = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    private static ScriptedCommand Action(string name) => new(name);

    private static IEnumerable<string> Names(IEnumerable<HistoryEntry> entries) => entries.Select(entry => entry.Action.Description);

    [Fact]
    public void ANewHistory_IsEmpty()
    {
        var history = new CommandHistory();

        Assert.Empty(history.Undoable);
        Assert.Empty(history.Redoable);
        Assert.Null(history.NextToUndo);
        Assert.Null(history.NextToRedo);
    }

    [Fact]
    public void Record_PutsTheMostRecentOperationFirst_AndKeepsItsTime()
    {
        var history = new CommandHistory();

        history.Record(Action("A"), Start);
        history.Record(Action("B"), Start.AddMinutes(5));

        Assert.Equal(new[] { "B", "A" }, Names(history.Undoable));
        Assert.Equal("B", history.NextToUndo!.Action.Description);
        Assert.Equal(Start.AddMinutes(5), history.NextToUndo.At);
    }

    [Fact]
    public void MoveNextUndoToRedo_MovesOnlyTheMostRecentOperation()
    {
        var history = new CommandHistory();
        history.Record(Action("A"), Start);
        history.Record(Action("B"), Start);

        history.MoveNextUndoToRedo();

        Assert.Equal(new[] { "A" }, Names(history.Undoable));
        Assert.Equal(new[] { "B" }, Names(history.Redoable));
    }

    [Fact]
    public void Redoing_PutsTheOperationBackOnTheUndoSide()
    {
        var history = new CommandHistory();
        history.Record(Action("A"), Start);
        history.MoveNextUndoToRedo();

        history.MoveNextRedoToUndo();

        Assert.Equal(new[] { "A" }, Names(history.Undoable));
        Assert.Empty(history.Redoable);
    }

    [Fact]
    public void Record_AfterAnUndo_DropsWhatCouldHaveBeenRedone()
    {
        var history = new CommandHistory();
        history.Record(Action("A"), Start);
        history.MoveNextUndoToRedo();

        history.Record(Action("B"), Start);

        Assert.Empty(history.Redoable);
        Assert.Equal(new[] { "B" }, Names(history.Undoable));
    }

    [Fact]
    public void Record_ForgetsTheOldestOperation_WhenTheCapacityIsExceeded()
    {
        var history = new CommandHistory(capacity: 3);

        foreach (var name in new[] { "A", "B", "C", "D" })
            history.Record(Action(name), Start);

        Assert.Equal(new[] { "D", "C", "B" }, Names(history.Undoable));
    }

    [Fact]
    public void TheDefaultCapacity_IsTwenty()
    {
        var history = new CommandHistory();

        for (var number = 1; number <= 25; number++)
            history.Record(Action($"op{number}"), Start);

        Assert.Equal(20, history.Undoable.Count);
        Assert.Equal("op25", history.Undoable[0].Action.Description);
        Assert.Equal("op6", history.Undoable[^1].Action.Description);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ACapacityBelowOne_IsRejected(int capacity)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new CommandHistory(capacity));

    [Fact]
    public void DiscardNextToUndo_RemovesTheMostRecentOperationWithoutMakingItRedoable()
    {
        var history = new CommandHistory();
        history.Record(Action("A"), Start);
        history.Record(Action("B"), Start);

        history.DiscardNextToUndo();

        Assert.Equal(new[] { "A" }, Names(history.Undoable));
        Assert.Empty(history.Redoable);
    }

    [Fact]
    public void DiscardNextToRedo_RemovesTheNextOperationToRedo()
    {
        var history = new CommandHistory();
        history.Record(Action("A"), Start);
        history.Record(Action("B"), Start);
        history.MoveNextUndoToRedo();
        history.MoveNextUndoToRedo();

        history.DiscardNextToRedo();

        Assert.Equal(new[] { "B" }, Names(history.Redoable));
    }

    [Fact]
    public async Task RunExclusiveAsync_DoesNotLetTwoOperationsOverlap()
    {
        var history = new CommandHistory();
        var gate = new TaskCompletionSource();
        var order = new List<string>();

        var first = history.RunExclusiveAsync(async () =>
        {
            order.Add("primera empieza");
            await gate.Task;
            order.Add("primera termina");
            return 1;
        });
        var second = history.RunExclusiveAsync(() =>
        {
            order.Add("segunda");
            return Task.FromResult(2);
        });

        await Task.Delay(50); // si no hubiera exclusión, la segunda ya habría corrido
        Assert.Equal(new[] { "primera empieza" }, order);

        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(new[] { "primera empieza", "primera termina", "segunda" }, order);
    }

    [Fact]
    public async Task RunExclusiveAsync_ReleasesTheGate_WhenTheOperationFails()
    {
        var history = new CommandHistory();

        await Assert.ThrowsAsync<InvalidOperationException>(() => history.RunExclusiveAsync<int>(() => throw new InvalidOperationException("falla")));

        Assert.Equal(7, await history.RunExclusiveAsync(() => Task.FromResult(7)));
    }
}
