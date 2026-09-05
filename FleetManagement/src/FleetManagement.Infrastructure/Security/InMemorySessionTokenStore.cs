using System.Collections.Concurrent;
using FleetManagement.Application.Interfaces;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Infrastructure.Security;

/// <summary>
/// Almacén de sesiones en memoria (alternativa simplificada a JWT para este
/// prototipo). Se registra en el contenedor de DI con ciclo de vida
/// Singleton (services.AddSingleton) para que las sesiones persistan
/// mientras el proceso esté en ejecución; esto es una decisión de ciclo de
/// vida gestionada por el contenedor de ASP.NET Core, distinta del patrón
/// GoF Singleton usado deliberadamente en FleetAuditLogger.
/// </summary>
public class InMemorySessionTokenStore : ISessionTokenStore
{
    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();

    public string CreateSession(Guid userId, string username, UserRole role)
    {
        var token = Guid.NewGuid().ToString("N");
        _sessions[token] = new SessionInfo(userId, username, role, DateTime.UtcNow);
        return token;
    }

    public SessionInfo? GetSession(string token)
    {
        return _sessions.TryGetValue(token, out var session) ? session : null;
    }

    public void RemoveSession(string token)
    {
        _sessions.TryRemove(token, out _);
    }
}
