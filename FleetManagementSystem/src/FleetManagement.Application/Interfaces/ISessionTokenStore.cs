using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Interfaces;

/// <summary>Información de una sesión activa emitida tras un login exitoso.</summary>
public record SessionInfo(Guid UserId, string Username, UserRole Role, DateTime CreatedAt);

/// <summary>
/// Abstracción de almacenamiento de sesiones. Se usa un token de sesión en
/// memoria en lugar de JWT para mantener el prototipo libre de dependencias
/// externas adicionales (ver README, sección "Notas sobre producción" para
/// cómo migrar a JWT/OAuth real).
/// </summary>
public interface ISessionTokenStore
{
    string CreateSession(Guid userId, string username, UserRole role);
    SessionInfo? GetSession(string token);
    void RemoveSession(string token);
}
