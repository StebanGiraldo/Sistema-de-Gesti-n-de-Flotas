namespace FleetManagement.Application.Commands;

/// <summary>Una operación del historial y cuándo se ejecutó.</summary>
public sealed record HistoryEntry(IUndoableAction Action, DateTime At);

/// <summary>
/// Historial de un usuario: lo que puede deshacer y lo que puede rehacer. Guarda como máximo <c>capacity</c> operaciones
/// (la más antigua se olvida). Toda lectura o cambio debe hacerse dentro de <see cref="RunExclusiveAsync{T}"/>: dos
/// peticiones del mismo usuario (por ejemplo, dos "deshacer" seguidos) no pueden pisarse.
/// </summary>
public sealed class CommandHistory
{
    public const int DefaultCapacity = 20;

    private readonly int _capacity;
    private readonly List<HistoryEntry> _undo = new(); // la última es la más reciente
    private readonly List<HistoryEntry> _redo = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CommandHistory(int capacity = DefaultCapacity)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "El historial debe guardar al menos una operación.");

        _capacity = capacity;
    }

    public async Task<T> RunExclusiveAsync<T>(Func<Task<T>> operation)
    {
        await _gate.WaitAsync();
        try
        {
            return await operation();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>De la más reciente a la más antigua: la primera es la que se deshace a continuación.</summary>
    public IReadOnlyList<HistoryEntry> Undoable => Enumerable.Reverse(_undo).ToList();

    /// <summary>De la más reciente a la más antigua: la primera es la que se rehace a continuación.</summary>
    public IReadOnlyList<HistoryEntry> Redoable => Enumerable.Reverse(_redo).ToList();

    public HistoryEntry? NextToUndo => _undo.Count == 0 ? null : _undo[^1];

    public HistoryEntry? NextToRedo => _redo.Count == 0 ? null : _redo[^1];

    /// <summary>Una operación nueva: entra al historial y lo que se había deshecho ya no se puede rehacer.</summary>
    public void Record(IUndoableAction action, DateTime at)
    {
        _undo.Add(new HistoryEntry(action, at));
        _redo.Clear();
        if (_undo.Count > _capacity)
            _undo.RemoveAt(0);
    }

    public void MoveNextUndoToRedo() => Move(_undo, _redo);

    public void MoveNextRedoToUndo() => Move(_redo, _undo);

    public void DiscardNextToUndo() => _undo.RemoveAt(_undo.Count - 1);

    public void DiscardNextToRedo() => _redo.RemoveAt(_redo.Count - 1);

    private static void Move(List<HistoryEntry> from, List<HistoryEntry> to)
    {
        to.Add(from[^1]);
        from.RemoveAt(from.Count - 1);
    }
}
