using FleetManagement.Application.Commands;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces;

namespace FleetManagement.Application.Tests.Command;

/// <summary>
/// Bitácora de prueba (escrita a mano, sin librerías de mocking) que, a diferencia de <c>RecordingAuditLogger</c>, también
/// guarda el usuario de cada evento: aquí importa saber QUIÉN pidió cada operación.
/// </summary>
public sealed class UserRecordingAuditLogger : IFleetAuditLogger
{
    public sealed record Entry(string Category, string Message, string? User);

    private readonly List<Entry> _entries = new();

    public IReadOnlyList<Entry> Entries => _entries;

    public void LogEvent(string category, string message, string? username = null)
        => _entries.Add(new Entry(category, message, username));

    public IReadOnlyList<AuditLogEntryDto> GetRecentLogs(int count = 100) => new List<AuditLogEntryDto>();
}

/// <summary>
/// Orden de prueba: no hace nada real. Cuenta cuántas veces se ejecutó, deshizo y rehizo, anota en un diario compartido
/// el orden en que pasó cada cosa y deja que la prueba decida si una de esas tres acciones falla o se queda esperando.
/// Sirve para probar el invocador y el historial sin depender de ninguna ruta ni vehículo.
/// </summary>
public sealed class ScriptedCommand : IUndoableCommand<int>
{
    private readonly List<string>? _journal;

    public ScriptedCommand(string description, List<string>? journal = null)
    {
        Description = description;
        _journal = journal;
    }

    public string Description { get; }

    public bool HasEffect { get; set; } = true;

    public int ExecuteCalls { get; private set; }
    public int UndoCalls { get; private set; }
    public int RedoCalls { get; private set; }

    public Exception? ExecuteFailure { get; set; }
    public Exception? UndoFailure { get; set; }
    public Exception? RedoFailure { get; set; }

    /// <summary>Si la prueba lo pone, deshacer se queda esperando hasta que ese Task termine.</summary>
    public Task? UndoGate { get; set; }

    public Task<int> ExecuteAsync()
    {
        ExecuteCalls++;
        _journal?.Add($"ejecutar {Description}");
        if (ExecuteFailure is not null) throw ExecuteFailure;
        return Task.FromResult(ExecuteCalls);
    }

    public async Task UndoAsync()
    {
        UndoCalls++;
        _journal?.Add($"deshacer {Description}");
        if (UndoGate is not null) await UndoGate;
        if (UndoFailure is not null) throw UndoFailure;
    }

    public Task RedoAsync()
    {
        RedoCalls++;
        _journal?.Add($"rehacer {Description}");
        if (RedoFailure is not null) throw RedoFailure;
        return Task.CompletedTask;
    }
}
