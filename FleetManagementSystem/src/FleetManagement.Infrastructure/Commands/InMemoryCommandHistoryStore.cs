using System.Collections.Concurrent;
using FleetManagement.Application.Commands;

namespace FleetManagement.Infrastructure.Commands;

/// <summary>Un historial por usuario, en memoria: se pierde al reiniciar la API, como el resto de los datos del prototipo.</summary>
public sealed class InMemoryCommandHistoryStore : ICommandHistoryStore
{
    private readonly ConcurrentDictionary<string, CommandHistory> _histories = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _capacity;

    public InMemoryCommandHistoryStore(int capacity = CommandHistory.DefaultCapacity)
    {
        _capacity = capacity;
    }

    public CommandHistory For(string userKey)
    {
        if (string.IsNullOrWhiteSpace(userKey))
            throw new ArgumentException("El usuario del historial no puede estar vacío.", nameof(userKey));

        return _histories.GetOrAdd(userKey.Trim(), _ => new CommandHistory(_capacity));
    }
}
