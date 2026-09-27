using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Builders;

/// <summary>
/// Director opcional del patrón Builder: encapsula "recetas" de construcción
/// reutilizables para los casos más comunes del panel de administración
/// (por ejemplo, una ruta express con carga urgente y salida inmediata),
/// evitando repetir la misma secuencia de llamadas al builder en varios
/// lugares del código. Usado por DeliveryRouteService.CreateExpressRouteAsync
/// y expuesto en POST /api/routes/express.
/// </summary>
public class DeliveryRouteDirector
{
    public DeliveryRoute BuildExpressRoute(
        IDeliveryRouteBuilder builder,
        string name,
        double originLat, double originLng,
        double destinationLat, double destinationLng,
        Guid vehicleId,
        Guid driverId,
        double estimatedDistanceKm,
        double estimatedDurationMinutes)
    {
        return builder
            .WithName(name)
            .WithOrigin(originLat, originLng)
            .WithDestination(destinationLat, destinationLng)
            .AssignVehicle(vehicleId)
            .AssignDriver(driverId)
            .AddCargoItem("Envío urgente", 50, 0.2, CargoPriority.Urgent)
            .ScheduleFor(DateTime.UtcNow)
            .WithEstimatedTrip(estimatedDistanceKm, estimatedDurationMinutes)
            .Build();
    }
}
