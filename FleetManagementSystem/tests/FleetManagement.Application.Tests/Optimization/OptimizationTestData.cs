using FleetManagement.Application.Geo;
using FleetManagement.Application.Optimization;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Tests.Optimization;

/// <summary>
/// Datos de prueba para la optimización. Casi todo ocurre sobre el ecuador, donde un grado de longitud mide siempre lo
/// mismo (<see cref="KmPerDegree"/>) y los grados de latitud y longitud valen igual: las distancias se calculan a mano.
/// </summary>
public static class OptimizationTestData
{
    public const double KmPerDegree = GeoMath.EarthRadiusKm * Math.PI / 180.0;

    public static GeoCoordinate At(double latitude, double longitude) => new(latitude, longitude);

    /// <summary>Punto sobre el ecuador a <paramref name="longitude"/> grados.</summary>
    public static GeoCoordinate OnEquator(double longitude) => new(0, longitude);

    public static PendingDelivery Delivery(string description, double kg, CargoPriority priority = CargoPriority.Standard, double m3 = 0, double longitude = 0.1, double latitude = 0)
        => new(description, kg, m3, priority, new GeoCoordinate(latitude, longitude), description);

    public static CandidateVehicle Vehicle(string plate, double capacityKg, double? capacityM3 = null, double longitude = 0, double speedKmh = 80, VehicleType type = VehicleType.Van)
        => new(Guid.NewGuid(), plate, type, capacityKg, capacityM3, new GeoCoordinate(0, longitude), speedKmh);

    /// <summary>Puntos al azar (con semilla fija: siempre los mismos) en una caja cerca de Bucaramanga.</summary>
    public static List<GeoCoordinate> RandomPoints(Random random, int count)
        => Enumerable.Range(0, count).Select(_ => new GeoCoordinate(6.9 + random.NextDouble() * 0.3, -73.3 + random.NextDouble() * 0.3)).ToList();

    /// <summary>El mejor orden posible, probando todos (solo para pocas paradas): sirve para saber qué tan lejos queda la heurística.</summary>
    public static double BestPossibleKm(GeoCoordinate origin, IReadOnlyList<GeoCoordinate> stops, GeoCoordinate? destination = null)
    {
        var best = double.MaxValue;
        foreach (var order in Permutations(Enumerable.Range(0, stops.Count).ToList()))
            best = Math.Min(best, TourLength.Km(origin, stops, order, destination));
        return best;
    }

    /// <summary>El mejor orden posible (índices de <paramref name="stops"/>), probando todos: solo para pocas paradas.</summary>
    public static List<int> BestPossibleOrder(GeoCoordinate origin, IReadOnlyList<GeoCoordinate> stops, GeoCoordinate? destination = null)
    {
        List<int>? bestOrder = null;
        var best = double.MaxValue;
        foreach (var order in Permutations(Enumerable.Range(0, stops.Count).ToList()))
        {
            var km = TourLength.Km(origin, stops, order, destination);
            if (km >= best) continue;
            best = km;
            bestOrder = order;
        }

        return bestOrder ?? new List<int>();
    }

    private static IEnumerable<List<int>> Permutations(List<int> items)
    {
        if (items.Count <= 1)
        {
            yield return items.ToList();
            yield break;
        }

        for (var i = 0; i < items.Count; i++)
        {
            var rest = items.Where((_, index) => index != i).ToList();
            foreach (var permutation in Permutations(rest))
            {
                permutation.Insert(0, items[i]);
                yield return permutation;
            }
        }
    }
}

/// <summary>Optimizador de mentira que devuelve las paradas tal cual llegaron: sirve de punto de partida "malo" para probar el 2-opt.</summary>
public sealed class InOrderOptimizer : IRouteOptimizer
{
    public IReadOnlyList<int> Order(GeoCoordinate origin, IReadOnlyList<GeoCoordinate> stops, GeoCoordinate? destination = null)
        => Enumerable.Range(0, stops.Count).ToList();
}
