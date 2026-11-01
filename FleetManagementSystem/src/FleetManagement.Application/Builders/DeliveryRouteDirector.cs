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
    /// <summary>Carga fija de toda ruta express. Son constantes para que quien valida la asignación use los mismos valores.</summary>
    public const string ExpressCargoDescription = "Envío urgente";
    public const double ExpressCargoWeightKg = 50;
    public const double ExpressCargoVolumeM3 = 0.2;

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
            .AddCargoItem(ExpressCargoDescription, ExpressCargoWeightKg, ExpressCargoVolumeM3, CargoPriority.Urgent)
            .ScheduleFor(DateTime.UtcNow)
            .WithEstimatedTrip(estimatedDistanceKm, estimatedDurationMinutes)
            .Build();
    }
}
