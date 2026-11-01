using FleetManagement.Application.Geo;

namespace FleetManagement.Application.Optimization;

/// <summary>Largo de un recorrido: es la medida con la que se compara un orden de paradas contra otro.</summary>
public static class TourLength
{
    /// <summary>
    /// Kilómetros de ir de <paramref name="origin"/> por las paradas, en el orden dado, hasta <paramref name="destination"/>
    /// (si lo hay). Cada tramo es en línea recta (Haversine, <see cref="GeoMath"/>), igual que la estimación de las rutas.
    /// </summary>
    public static double Km(GeoCoordinate origin, IReadOnlyList<GeoCoordinate> orderedStops, GeoCoordinate? destination = null)
    {
        var km = 0.0;
        var current = origin;
        foreach (var stop in orderedStops)
        {
            km += GeoMath.DistanceKm(current, stop);
            current = stop;
        }

        return destination is { } end ? km + GeoMath.DistanceKm(current, end) : km;
    }

    /// <summary>Igual que <see cref="Km(GeoCoordinate, IReadOnlyList{GeoCoordinate}, GeoCoordinate?)"/>, con las paradas dadas por <paramref name="order"/> (índices).</summary>
    public static double Km(GeoCoordinate origin, IReadOnlyList<GeoCoordinate> stops, IReadOnlyList<int> order, GeoCoordinate? destination = null)
        => Km(origin, order.Select(index => stops[index]).ToList(), destination);
}
