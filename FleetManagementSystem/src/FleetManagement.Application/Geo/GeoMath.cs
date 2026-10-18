namespace FleetManagement.Application.Geo;

/// <summary>
/// Cálculos geográficos puros (sin estado ni dependencias): distancia de Haversine e interpolación entre dos puntos.
///
/// Decisión de diseño: esta clase es la ÚNICA fuente de la fórmula de Haversine. Antes vivía privada dentro de
/// <c>DeliveryRouteService</c> (para estimar la distancia de una ruta) y la simulación de movimiento necesita exactamente
/// la misma medida; extraerla evita tener dos copias que se desincronicen (DRY). <c>DeliveryRouteService</c> ahora delega
/// aquí, de modo que sus resultados no cambian.
/// </summary>
public static class GeoMath
{
    /// <summary>Radio medio de la Tierra con el que se calcula la distancia sobre la esfera.</summary>
    public const double EarthRadiusKm = 6371.0;

    private const double DegreesInHalfTurn = 180.0;
    private const double SegmentStart = 0.0;
    private const double SegmentEnd = 1.0;

    /// <summary>Distancia en línea recta (círculo máximo) entre dos coordenadas, en kilómetros.</summary>
    public static double DistanceKm(GeoCoordinate from, GeoCoordinate to)
    {
        var dLat = ToRadians(to.Latitude - from.Latitude);
        var dLng = ToRadians(to.Longitude - from.Longitude);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(from.Latitude)) * Math.Cos(ToRadians(to.Latitude)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
        return EarthRadiusKm * c;
    }

    /// <summary>
    /// Punto situado a la fracción indicada del segmento (0 = <paramref name="from"/>, 1 = <paramref name="to"/>).
    /// Se interpola de forma lineal en latitud/longitud porque el mapa dibuja cada tramo de la ruta como una recta entre
    /// sus puntos: con la interpolación lineal el marcador del vehículo queda exactamente sobre la línea dibujada.
    /// Los extremos se devuelven tal cual (sin pasar por la aritmética) para que el destino sea exacto.
    /// </summary>
    public static GeoCoordinate Interpolate(GeoCoordinate from, GeoCoordinate to, double fraction)
    {
        if (fraction <= SegmentStart) return from;
        if (fraction >= SegmentEnd) return to;

        return new GeoCoordinate(
            from.Latitude + (to.Latitude - from.Latitude) * fraction,
            from.Longitude + (to.Longitude - from.Longitude) * fraction);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / DegreesInHalfTurn;
}
