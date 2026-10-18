using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Simulation;

/// <summary>Un viaje en curso: la ruta que se recorre y el vehículo que la recorre.</summary>
public sealed record ActiveTrip(DeliveryRoute Route, Vehicle Vehicle);

/// <summary>
/// Define en un solo lugar qué es un "viaje activo" para que el simulador (que mueve los vehículos) y el servicio de
/// progreso (que lo informa) nunca discrepen: un vehículo EnRoute cuya ruta asignada está InProgress o Delayed.
/// Un vehículo EnRoute sin ruta activa no forma parte de ningún viaje, y por eso la simulación no lo mueve.
/// </summary>
public static class ActiveTrips
{
    public static IReadOnlyList<ActiveTrip> Find(IEnumerable<DeliveryRoute> routes, IEnumerable<Vehicle> vehicles)
    {
        var activeRoutes = routes.Where(IsActive).ToList();
        var trips = new List<ActiveTrip>();

        foreach (var vehicle in vehicles.Where(IsEnRoute))
        {
            // Si por un error de datos un vehículo tuviera dos rutas activas se sigue la primera: solo está en un lugar.
            var route = activeRoutes.FirstOrDefault(candidate => candidate.AssignedVehicleId == vehicle.Id);
            if (route is not null)
                trips.Add(new ActiveTrip(route, vehicle));
        }

        return trips;
    }

    private static bool IsEnRoute(Vehicle vehicle) => vehicle.Status == VehicleStatus.EnRoute;

    private static bool IsActive(DeliveryRoute route)
        => route.Status is DeliveryRouteStatus.InProgress or DeliveryRouteStatus.Delayed;
}
