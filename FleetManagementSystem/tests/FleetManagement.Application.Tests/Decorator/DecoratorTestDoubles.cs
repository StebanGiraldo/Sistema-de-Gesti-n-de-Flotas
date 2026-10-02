using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Tests.Bridge;

namespace FleetManagement.Application.Tests.Decorator;

/// <summary>
/// Bitácora de auditoría de prueba (test double escrito a mano, sin librerías
/// de mocking): guarda los eventos en memoria para poder hacer aserciones
/// precisas sin depender del Singleton compartido FleetAuditLogger.Instance.
/// </summary>
public class RecordingAuditLogger : IFleetAuditLogger
{
    public record Entry(string Category, string Message);

    private readonly List<Entry> _entries = new();

    public IReadOnlyList<Entry> Entries => _entries;

    public void LogEvent(string category, string message, string? username = null)
        => _entries.Add(new Entry(category, message));

    public IReadOnlyList<AuditLogEntryDto> GetRecentLogs(int count = 100) => new List<AuditLogEntryDto>();
}

/// <summary>
/// Canal (Implementor del Bridge) que falla las primeras N llamadas y luego
/// entrega con normalidad, para simular errores transitorios de una pasarela
/// SMTP/SMS. Con int.MaxValue falla siempre.
/// </summary>
public class FlakyNotificationChannel : INotificationChannel
{
    private readonly int _failuresBeforeSuccess;

    public FlakyNotificationChannel(int failuresBeforeSuccess)
    {
        _failuresBeforeSuccess = failuresBeforeSuccess;
    }

    public int Calls { get; private set; }

    /// <summary>Lo que el canal entregó con éxito.</summary>
    public RecordingNotificationChannel Delivered { get; } = new();

    public Task SendAsync(string recipient, string subject, string message)
    {
        Calls++;
        if (Calls <= _failuresBeforeSuccess)
            throw new InvalidOperationException($"Fallo transitorio del canal (llamada {Calls}).");

        return Delivered.SendAsync(recipient, subject, message);
    }
}
