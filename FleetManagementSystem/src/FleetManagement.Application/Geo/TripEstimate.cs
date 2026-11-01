namespace FleetManagement.Application.Geo;

/// <summary>
/// Regla única para convertir una distancia en minutos de viaje. La usan la creación de rutas y la optimización de
/// paradas, de modo que el tiempo estimado sea el mismo se calcule donde se calcule.
/// </summary>
public static class TripEstimate
{
    /// <summary>Velocidad promedio (km/h) para vías mixtas urbanas e interurbanas, cuando se desconoce el vehículo.</summary>
    public const double DefaultSpeedKmh = 55.0;

    /// <summary>Minutos que tarda recorrer <paramref name="distanceKm"/> a <paramref name="speedKmh"/> (sin redondear).</summary>
    public static double Minutes(double distanceKm, double speedKmh)
    {
        if (speedKmh <= 0)
            throw new ArgumentOutOfRangeException(nameof(speedKmh), speedKmh, "La velocidad debe ser mayor que cero.");

        return distanceKm / speedKmh * 60;
    }
}
