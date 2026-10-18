using FleetManagement.Application.Flyweights;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Simulation;

/// <summary>
/// Velocidad con la que avanza un viaje, calculada en UN solo lugar: tanto el simulador (cuánto se mueve el vehículo en
/// cada paso) como el servicio de progreso (cuántos minutos faltan) la usan, de modo que la ETA que ve el usuario es
/// exactamente el tiempo que tardará la simulación en llegar si nada cambia.
///
/// La velocidad base sale del perfil compartido del tipo de vehículo (Flyweight); una ruta retrasada avanza más despacio
/// y la aceleración del tiempo convierte "km/h de la realidad" en "km por hora de reloj de la simulación".
/// </summary>
public static class TripSpeed
{
    private const double FullSpeedFactor = 1.0;

    /// <summary>Kilómetros que avanza el vehículo por cada hora de reloj real de la simulación.</summary>
    public static double KmPerHour(VehicleTypeProfile profile, DeliveryRouteStatus routeStatus, SimulationOptions options)
    {
        var statusFactor = routeStatus == DeliveryRouteStatus.Delayed ? options.DelayedSpeedFactor : FullSpeedFactor;
        return profile.MaxSpeedKmh * statusFactor * options.TimeAcceleration;
    }
}
