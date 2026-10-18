using System.Collections.Concurrent;
using FleetManagement.Application.Simulation;

namespace FleetManagement.Infrastructure.Simulation;

/// <summary>
/// Implementación en memoria de <see cref="ITripProgressTracker"/>. Se registra como Singleton (igual que los repositorios
/// en memoria) porque el avance debe sobrevivir entre un paso de la simulación y el siguiente, y porque lo consultan a la
/// vez el servicio de fondo (que escribe) y las peticiones HTTP de progreso (que leen): por eso usa un
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> y no un diccionario con candados propios.
///
/// Solo guarda un número por ruta (los kilómetros recorridos), así que el estado es mínimo; es estado de instancia, no
/// estático, y cada prueba o cada contenedor de DI tiene el suyo.
/// </summary>
public sealed class InMemoryTripProgressTracker : ITripProgressTracker
{
    private readonly ConcurrentDictionary<Guid, double> _coveredKmByRoute = new();

    public bool TryGetCoveredKm(Guid routeId, out double coveredKm)
        => _coveredKmByRoute.TryGetValue(routeId, out coveredKm);

    public void SetCoveredKm(Guid routeId, double coveredKm)
        => _coveredKmByRoute[routeId] = coveredKm;

    public void Clear(Guid routeId)
        => _coveredKmByRoute.TryRemove(routeId, out _);

    public void ClearExcept(IReadOnlySet<Guid> routeIdsToKeep)
    {
        // Keys es una copia instantánea: se puede quitar mientras se recorre sin invalidar la enumeración.
        foreach (var routeId in _coveredKmByRoute.Keys.Where(id => !routeIdsToKeep.Contains(id)))
            _coveredKmByRoute.TryRemove(routeId, out _);
    }
}
