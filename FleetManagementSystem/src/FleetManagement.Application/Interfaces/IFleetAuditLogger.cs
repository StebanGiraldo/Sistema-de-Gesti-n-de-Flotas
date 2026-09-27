using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Interfaces;

/// <summary>
/// Abstracción del registro de auditoría. La implementación concreta
/// (FleetManagement.Infrastructure.Logging.FleetAuditLogger) aplica el
/// patrón SINGLETON clásico (Lazy&lt;T&gt; + lock) para garantizar una única
/// instancia en toda la aplicación. El resto del código depende de esta
/// interfaz (DIP) y no de la clase concreta.
/// </summary>
public interface IFleetAuditLogger
{
    void LogEvent(string category, string message, string? username = null);
    IReadOnlyList<AuditLogEntryDto> GetRecentLogs(int count = 100);
}
