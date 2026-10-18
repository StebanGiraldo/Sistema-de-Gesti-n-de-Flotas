using FleetManagement.Application.Geo;
using FleetManagement.Domain.Entities;
using NetTopologySuite.Geometries;

namespace FleetManagement.Application.Simulation;

/// <summary>Punto de paso de una ruta (origen, parada intermedia o destino) con la etiqueta que ve el usuario.</summary>
public sealed record RouteStop(GeoCoordinate Location, string Label);

/// <summary>Tramo recto entre dos paradas consecutivas, con el kilómetro de la poligonal en que empieza.</summary>
public sealed record RouteSegment(int Index, RouteStop Start, RouteStop End, double StartKm, double LengthKm)
{
    public double EndKm => StartKm + LengthKm;
}

/// <summary>
/// Poligonal que recorre un vehículo: Origen → paradas (ordenadas por <c>Order</c>) → Destino. Es un objeto de valor
/// inmutable construido una vez desde una <see cref="DeliveryRoute"/>: guarda los puntos y las distancias acumuladas,
/// y responde "¿dónde estoy a X km del origen?" (<see cref="PointAt"/>), "¿en qué tramo voy?" y "¿cuál es la próxima
/// parada?". Toda la geometría de la simulación pasa por aquí, de modo que el vehículo nunca puede salirse de la ruta.
///
/// Decisión de diseño: es una copia de los datos de la ruta en el momento de construirla; si la ruta cambia después, se
/// construye otra <see cref="RoutePath"/>. Por eso es seguro compartirla entre hilos y no tiene estado que sincronizar.
/// </summary>
public sealed class RoutePath
{
    public const string OriginLabel = "Origen";
    public const string DestinationLabel = "Destino";
    public const string WaypointFallbackLabel = "Parada";

    private const double PercentScale = 100.0;
    private const double MinutesPerHour = 60.0;

    private readonly RouteStop[] _stops;
    private readonly double[] _cumulativeKm;

    private RoutePath(Guid routeId, IReadOnlyList<RouteStop> stops)
    {
        RouteId = routeId;
        _stops = stops.ToArray();
        _cumulativeKm = AccumulateDistances(_stops);
        Stops = Array.AsReadOnly(_stops);
        CumulativeKm = Array.AsReadOnly(_cumulativeKm);
    }

    public Guid RouteId { get; }

    /// <summary>Puntos de paso en el orden en que se recorren (siempre al menos origen y destino).</summary>
    public IReadOnlyList<RouteStop> Stops { get; }

    /// <summary>Distancia acumulada desde el origen hasta cada punto de <see cref="Stops"/> (la primera es 0).</summary>
    public IReadOnlyList<double> CumulativeKm { get; }

    public double TotalKm => _cumulativeKm[^1];

    public static RoutePath From(DeliveryRoute route)
    {
        var stops = new List<RouteStop> { new(ToCoordinate(route.Origin), OriginLabel) };
        stops.AddRange(route.Waypoints
            .OrderBy(waypoint => waypoint.Order)
            .Select((waypoint, index) => new RouteStop(ToCoordinate(waypoint.Location), LabelOf(waypoint, index))));
        stops.Add(new RouteStop(ToCoordinate(route.Destination), DestinationLabel));
        return new RoutePath(route.Id, stops);
    }

    /// <summary>Tramo en el que está el vehículo a esa distancia. En el extremo de una parada ya cuenta el tramo siguiente.</summary>
    public RouteSegment SegmentAt(double distanceKm)
    {
        var distance = Math.Clamp(distanceKm, 0, TotalKm);
        var index = SegmentIndexAt(distance);
        return new RouteSegment(index, _stops[index], _stops[index + 1], _cumulativeKm[index], _cumulativeKm[index + 1] - _cumulativeKm[index]);
    }

    /// <summary>Posición a esa distancia del origen. Antes del inicio devuelve el origen y a partir del final el destino exacto.</summary>
    public GeoCoordinate PointAt(double distanceKm)
    {
        if (distanceKm >= TotalKm) return _stops[^1].Location;

        var segment = SegmentAt(distanceKm);
        if (segment.LengthKm <= 0) return segment.Start.Location;

        var fraction = (Math.Max(distanceKm, 0) - segment.StartKm) / segment.LengthKm;
        return GeoMath.Interpolate(segment.Start.Location, segment.End.Location, fraction);
    }

    public string NextStopLabelAt(double distanceKm) => SegmentAt(distanceKm).End.Label;

    /// <summary>Avance del viaje de <paramref name="vehicleId"/> cuando lleva <paramref name="coveredKm"/> recorridos a <paramref name="speedKmh"/>.</summary>
    public TripProgress ProgressAt(Guid vehicleId, double coveredKm, double speedKmh)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(speedKmh);

        var covered = Math.Clamp(coveredKm, 0, TotalKm);
        var remaining = TotalKm - covered;
        var position = PointAt(covered);

        return new TripProgress(
            RouteId, vehicleId, covered, TotalKm, PercentOf(covered), position.Latitude, position.Longitude,
            NextStopLabelAt(covered), remaining, remaining / speedKmh * MinutesPerHour, remaining <= 0);
    }

    private double PercentOf(double coveredKm) => TotalKm <= 0 ? PercentScale : coveredKm / TotalKm * PercentScale;

    private int SegmentIndexAt(double distanceKm)
    {
        var lastSegment = _stops.Length - 2;
        for (var index = 0; index < lastSegment; index++)
        {
            if (distanceKm < _cumulativeKm[index + 1]) return index;
        }
        return lastSegment;
    }

    private static double[] AccumulateDistances(RouteStop[] stops)
    {
        var cumulative = new double[stops.Length];
        for (var index = 1; index < stops.Length; index++)
            cumulative[index] = cumulative[index - 1] + GeoMath.DistanceKm(stops[index - 1].Location, stops[index].Location);
        return cumulative;
    }

    // En NetTopologySuite X es la longitud e Y la latitud.
    private static GeoCoordinate ToCoordinate(Point point) => new(point.Y, point.X);

    private static string LabelOf(Waypoint waypoint, int index) =>
        string.IsNullOrWhiteSpace(waypoint.Label) ? $"{WaypointFallbackLabel} {index + 1}" : waypoint.Label;
}
