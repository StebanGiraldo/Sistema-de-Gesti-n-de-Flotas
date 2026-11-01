using FleetManagement.Application.Geo;

namespace FleetManagement.Application.Optimization;

/// <summary>
/// PATRÓN STRATEGY: decide en qué orden conviene visitar un conjunto de paradas. Cada algoritmo (vecino más cercano,
/// 2-opt...) es una implementación intercambiable; quien lo usa solo conoce esta interfaz.
///
/// Es una función pura: trabaja con coordenadas, no con rutas ni vehículos, así que sirve igual para reordenar las paradas
/// de una ruta existente que para armar el recorrido de cada vehículo en un plan de reparto.
/// </summary>
public interface IRouteOptimizer
{
    /// <summary>
    /// Orden de visita: una permutación de 0..<c>stops.Count - 1</c> (cada parada aparece exactamente una vez). El recorrido
    /// sale de <paramref name="origin"/>. Si hay <paramref name="destination"/>, el recorrido termina allí y ese punto no se
    /// mueve; si es <c>null</c>, termina en la última parada visitada.
    /// </summary>
    IReadOnlyList<int> Order(GeoCoordinate origin, IReadOnlyList<GeoCoordinate> stops, GeoCoordinate? destination = null);
}
