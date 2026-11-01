using FleetManagement.Application.Geo;

namespace FleetManagement.Application.Optimization;

/// <summary>
/// Heurística del vecino más cercano: desde donde está, ir siempre a la parada pendiente más próxima. Es rápida y da un
/// buen punto de partida, pero puede dejar cruces que <see cref="TwoOptRouteOptimizer"/> se encarga de deshacer.
/// Ante un empate gana la parada que venía primero, así el resultado es siempre el mismo para los mismos datos.
/// No usa el destino fijo (solo decide el orden de las paradas intermedias); el refinamiento 2-opt sí lo tiene en cuenta.
/// </summary>
public sealed class NearestNeighborRouteOptimizer : IRouteOptimizer
{
    public IReadOnlyList<int> Order(GeoCoordinate origin, IReadOnlyList<GeoCoordinate> stops, GeoCoordinate? destination = null)
    {
        var pending = Enumerable.Range(0, stops.Count).ToList();
        var order = new List<int>(stops.Count);
        var current = origin;

        while (pending.Count > 0)
        {
            var next = NearestTo(current, stops, pending);
            order.Add(next);
            pending.Remove(next);
            current = stops[next];
        }

        return order;
    }

    private static int NearestTo(GeoCoordinate from, IReadOnlyList<GeoCoordinate> stops, IReadOnlyList<int> candidates)
        => candidates.OrderBy(index => GeoMath.DistanceKm(from, stops[index])).ThenBy(index => index).First();
}
