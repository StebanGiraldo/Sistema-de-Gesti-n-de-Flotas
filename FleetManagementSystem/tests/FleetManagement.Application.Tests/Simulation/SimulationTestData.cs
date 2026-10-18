using FleetManagement.Application.Geo;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using NetTopologySuite.Geometries;

namespace FleetManagement.Application.Tests.Simulation;

/// <summary>
/// Rutas y vehículos de prueba para la simulación. Casi todas las rutas siguen el ecuador: allí un grado de longitud
/// mide siempre lo mismo (<see cref="KmPerDegree"/>), así que las distancias esperadas se calculan a mano y exactas.
/// </summary>
public static class SimulationTestData
{
    /// <summary>Kilómetros que mide un grado a lo largo del ecuador (con el mismo radio terrestre que GeoMath).</summary>
    public const double KmPerDegree = GeoMath.EarthRadiusKm * Math.PI / 180.0;

    public static Point PointAt(double latitude, double longitude) => new(longitude, latitude) { SRID = 4326 };

    /// <summary>
    /// Ruta sobre el ecuador: origen en la longitud 0, paradas "Primera" (0,10°), "Segunda" (0,30°) y destino en 0,60°.
    /// Longitud total: 0,6° (66,717 km), tramos de 0,1°, 0,2° y 0,3°. Las paradas se agregan en orden inverso a propósito.
    /// </summary>
    public static DeliveryRoute EquatorRoute(
        DeliveryRouteStatus status = DeliveryRouteStatus.InProgress,
        Guid? vehicleId = null)
    {
        var route = new DeliveryRoute
        {
            Name = "Ruta del ecuador",
            Origin = PointAt(0, 0.00),
            Destination = PointAt(0, 0.60),
            Status = status,
            AssignedVehicleId = vehicleId
        };
        route.Waypoints.Add(new Waypoint { Order = 1, Label = "Segunda", Location = PointAt(0, 0.30) });
        route.Waypoints.Add(new Waypoint { Order = 0, Label = "Primera", Location = PointAt(0, 0.10) });
        return route;
    }

    /// <summary>Ruta con dos curvas (no es una recta), para comprobar que el vehículo sigue cada tramo y no un atajo.</summary>
    public static DeliveryRoute BentRoute(DeliveryRouteStatus status = DeliveryRouteStatus.InProgress, Guid? vehicleId = null)
    {
        var route = new DeliveryRoute
        {
            Name = "Ruta con curvas",
            Origin = PointAt(7.0806, -73.1716),
            Destination = PointAt(7.1193, -73.1227),
            Status = status,
            AssignedVehicleId = vehicleId
        };
        route.Waypoints.Add(new Waypoint { Order = 0, Label = "Terminal de carga", Location = PointAt(7.0850, -73.1690) });
        route.Waypoints.Add(new Waypoint { Order = 1, Label = "Puente", Location = PointAt(7.1050, -73.1450) });
        return route;
    }

    public static Vehicle EnRouteVehicle(VehicleType type = VehicleType.Van, double latitude = 0, double longitude = 0, double mileageKm = 1000)
    {
        var vehicle = new Vehicle
        {
            LicensePlate = $"{type.ToString()[..3].ToUpperInvariant()}-{Guid.NewGuid().ToString("N")[..3].ToUpperInvariant()}",
            Type = type,
            Status = VehicleStatus.EnRoute,
            MileageKm = mileageKm
        };
        vehicle.UpdateLocation(latitude, longitude);
        return vehicle;
    }

    /// <summary>Distancia (en grados, en el plano latitud/longitud) del punto a la poligonal: 0 si está exactamente sobre ella.</summary>
    public static double DistanceToPolylineInDegrees(IReadOnlyList<GeoCoordinate> polyline, double latitude, double longitude)
    {
        var best = double.MaxValue;
        for (var i = 0; i < polyline.Count - 1; i++)
            best = Math.Min(best, DistanceToSegment(polyline[i], polyline[i + 1], latitude, longitude));
        return best;
    }

    private static double DistanceToSegment(GeoCoordinate a, GeoCoordinate b, double latitude, double longitude)
    {
        var dLat = b.Latitude - a.Latitude;
        var dLng = b.Longitude - a.Longitude;
        var lengthSquared = dLat * dLat + dLng * dLng;
        var t = lengthSquared == 0 ? 0 : Math.Clamp(((latitude - a.Latitude) * dLat + (longitude - a.Longitude) * dLng) / lengthSquared, 0, 1);
        var closestLat = a.Latitude + t * dLat;
        var closestLng = a.Longitude + t * dLng;
        return Math.Sqrt(Math.Pow(latitude - closestLat, 2) + Math.Pow(longitude - closestLng, 2));
    }
}
