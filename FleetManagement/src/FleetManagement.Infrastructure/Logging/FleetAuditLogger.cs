using System.Collections.Concurrent;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces;

namespace FleetManagement.Infrastructure.Logging;

/// <summary>
/// PATRÓN SINGLETON (creacional) — thread-safe.
///
/// Garantiza que exista UNA ÚNICA instancia de esta clase en toda la
/// aplicación, encargada de centralizar el registro de auditoría de eventos
/// críticos (inicios de sesión, cambios de estado de vehículos, creación de
/// rutas, alertas reportadas, mantenimientos registrados, etc.), sin
/// importar cuántas clases distintas necesiten registrar eventos.
///
/// IMPLEMENTACIÓN THREAD-SAFE:
/// Se usa Lazy&lt;T&gt; con LazyThreadSafetyMode.ExecutionAndPublication, el
/// modo por defecto de Lazy&lt;T&gt; en .NET, que garantiza mediante un
/// bloqueo interno que la fábrica del valor sólo se ejecuta UNA vez incluso
/// si múltiples hilos acceden a "Instance" de forma concurrente por primera
/// vez. Además, la escritura sobre la cola de eventos está protegida con un
/// "lock" explícito (_writeLock) porque, aunque ConcurrentQueue&lt;T&gt; ya es
/// thread-safe para Enqueue/TryDequeue individuales, la operación compuesta
/// "encolar y luego recortar el historial al límite máximo" sí necesita
/// exclusión mutua para no perder o duplicar el recorte bajo concurrencia.
///
/// RECONCILIACIÓN CON EL CONTENEDOR DE INYECCIÓN DE DEPENDENCIAS:
/// El constructor es privado (nadie puede hacer "new FleetAuditLogger()"
/// desde fuera). En Program.cs se registra así:
///
///     builder.Services.AddSingleton&lt;IFleetAuditLogger&gt;(_ =&gt; FleetAuditLogger.Instance);
///
/// De esta forma, el propio Singleton GoF sigue siendo la única fuente de
/// verdad (incluso si algo accediera a FleetAuditLogger.Instance por fuera
/// del contenedor), y a la vez el contenedor de DI expone esa misma
/// instancia a través de la abstracción IFleetAuditLogger (DIP), que es lo
/// que consumen VehicleService, DeliveryRouteService, MaintenanceService,
/// TripAlertService, AuthService y AuditController.
/// </summary>
public sealed class FleetAuditLogger : IFleetAuditLogger
{
    private static readonly Lazy<FleetAuditLogger> LazyInstance =
        new(() => new FleetAuditLogger(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Punto de acceso global clásico al Singleton (uso típico fuera de DI, si fuera necesario).</summary>
    public static FleetAuditLogger Instance => LazyInstance.Value;

    private readonly ConcurrentQueue<AuditLogEntryDto> _logs = new();
    private readonly object _writeLock = new();
    private const int MaxEntries = 1000;

    // Constructor privado: única forma de instanciar la clase es a través de "Instance".
    private FleetAuditLogger()
    {
    }

    public void LogEvent(string category, string message, string? username = null)
    {
        var entry = new AuditLogEntryDto(DateTime.UtcNow, category, message, username);
        lock (_writeLock)
        {
            _logs.Enqueue(entry);
            while (_logs.Count > MaxEntries && _logs.TryDequeue(out _))
            {
                // Descarta las entradas más antiguas para evitar crecimiento indefinido de memoria.
            }
        }
    }

    public IReadOnlyList<AuditLogEntryDto> GetRecentLogs(int count = 100)
    {
        return _logs.Reverse().Take(Math.Max(0, count)).ToList();
    }
}
