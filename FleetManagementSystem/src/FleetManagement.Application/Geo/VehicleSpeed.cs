using FleetManagement.Application.Flyweights;
using FleetManagement.Domain.Entities;

namespace FleetManagement.Application.Geo;

/// <summary>
/// Velocidad (km/h) con la que se estima cuánto tarda un vehículo en un recorrido: la de su tipo (perfil FLYWEIGHT), que es
/// la misma que usa la simulación de movimiento, así el tiempo estimado y el avance coinciden. Sin vehículo, o sin perfiles,
/// se usa la velocidad promedio por defecto. Regla única para la creación de rutas, la optimización de paradas y el plan
/// de reparto.
/// </summary>
public static class VehicleSpeed
{
    public static double KmPerHour(Vehicle? vehicle, IVehicleTypeProfileFactory? profiles)
        => vehicle is null || profiles is null ? TripEstimate.DefaultSpeedKmh : profiles.GetProfile(vehicle.Type).MaxSpeedKmh;
}
