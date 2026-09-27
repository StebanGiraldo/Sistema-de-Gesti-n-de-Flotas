namespace FleetManagement.Application.DTOs;

/// <summary>Entrada del registro de auditoría centralizado (ver patrón Singleton: FleetAuditLogger).</summary>
public record AuditLogEntryDto(DateTime Timestamp, string Category, string Message, string? Username);
