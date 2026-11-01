using FleetManagement.Application.Geo;

namespace FleetManagement.Application.Optimization;

/// <summary>
/// Mejora un orden de paradas con 2-opt: mientras exista un tramo que, puesto al revés, acorte el recorrido, se invierte.
/// Parte del orden que entregue <c>seed</c> (normalmente el vecino más cercano) y nunca lo empeora: solo acepta cambios
/// que ahorran distancia. Conserva fijos el origen y, si lo hay, el destino.
///
/// Es una heurística: no garantiza el recorrido óptimo, pero deshace los cruces que deja el vecino más cercano y es
/// suficiente para los tamaños de un reparto (decenas de paradas).
/// </summary>
public sealed class TwoOptRouteOptimizer : IRouteOptimizer
{
    // Un cambio solo se acepta si ahorra algo de verdad; sin este margen, el error de redondeo podría alternar dos órdenes para siempre.
    private const double MinimumGainKm = 1e-9;
    private const int MaxPasses = 1000;

    private readonly IRouteOptimizer _seed;

    public TwoOptRouteOptimizer(IRouteOptimizer seed)
    {
        _seed = seed;
    }

    public IReadOnlyList<int> Order(GeoCoordinate origin, IReadOnlyList<GeoCoordinate> stops, GeoCoordinate? destination = null)
    {
        var seedOrder = _seed.Order(origin, stops, destination);
        if (stops.Count < 2) return seedOrder.ToList();

        // Nodos: 0 = origen, 1..n = paradas (índice + 1), n + 1 = destino (si lo hay). El recorrido es una lista de nodos.
        var nodes = new List<GeoCoordinate> { origin };
        nodes.AddRange(stops);
        if (destination is { } end) nodes.Add(end);

        var tour = new List<int> { 0 };
        tour.AddRange(seedOrder.Select(index => index + 1));
        if (destination is not null) tour.Add(nodes.Count - 1);

        Improve(tour, DistanceMatrix(nodes), stops.Count);
        return tour.Skip(1).Take(stops.Count).Select(node => node - 1).ToList();
    }

    private static void Improve(List<int> tour, double[,] distance, int lastStop)
    {
        for (var pass = 0; pass < MaxPasses && ImproveOnce(tour, distance, lastStop); pass++)
        {
            // cada pasada aplica las inversiones que ahorran; termina cuando ya no queda ninguna
        }
    }

    private static bool ImproveOnce(List<int> tour, double[,] distance, int lastStop)
    {
        var improved = false;
        for (var i = 1; i < lastStop; i++)
        {
            for (var j = i + 1; j <= lastStop; j++)
            {
                if (ReversalGain(tour, distance, i, j) <= MinimumGainKm) continue;
                tour.Reverse(i, j - i + 1);
                improved = true;
            }
        }

        return improved;
    }

    // Invertir tour[i..j] solo cambia las aristas de los extremos del tramo; el resto del recorrido queda igual.
    private static double ReversalGain(IReadOnlyList<int> tour, double[,] distance, int i, int j)
    {
        var before = distance[tour[i - 1], tour[i]];
        var after = distance[tour[i - 1], tour[j]];
        if (j + 1 < tour.Count)   // hay algo después del tramo (otra parada o el destino); en un recorrido abierto, la última parada no tiene nada
        {
            before += distance[tour[j], tour[j + 1]];
            after += distance[tour[i], tour[j + 1]];
        }

        return before - after;
    }

    private static double[,] DistanceMatrix(IReadOnlyList<GeoCoordinate> nodes)
    {
        var matrix = new double[nodes.Count, nodes.Count];
        for (var a = 0; a < nodes.Count; a++)
        {
            for (var b = a + 1; b < nodes.Count; b++)
                matrix[a, b] = matrix[b, a] = GeoMath.DistanceKm(nodes[a], nodes[b]);
        }

        return matrix;
    }
}
