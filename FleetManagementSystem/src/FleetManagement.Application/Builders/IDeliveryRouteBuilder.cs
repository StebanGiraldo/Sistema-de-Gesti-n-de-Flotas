using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Builders;

/// <summary>
/// PATRÓN BUILDER (creacional).
///
/// Construye un DeliveryRoute paso a paso mediante una interfaz fluida. Una
/// ruta tiene múltiples partes opcionales y de longitud variable (paradas
/// intermedias, artículos de carga, vehículo, conductor, fecha), por lo que
/// un único constructor con muchos parámetros sería propenso a errores y
/// difícil de leer.
///
/// Beneficio: el llamador agrega sólo las piezas que necesita, en el orden
/// que le resulte natural, y Build() valida el resultado antes de
/// entregarlo, evitando construir rutas incompletas o inconsistentes.
/// </summary>
public interface IDeliveryRouteBuilder
{
    IDeliveryRouteBuilder WithName(string name);
    IDeliveryRouteBuilder WithOrigin(double latitude, double longitude);
    IDeliveryRouteBuilder WithDestination(double latitude, double longitude);
    IDeliveryRouteBuilder AddWaypoint(double latitude, double longitude, string label);
    IDeliveryRouteBuilder AddCargoItem(string description, double weightKg, double volumeM3, CargoPriority priority);
    IDeliveryRouteBuilder AssignVehicle(Guid vehicleId);
    IDeliveryRouteBuilder AssignDriver(Guid driverId);
    IDeliveryRouteBuilder ScheduleFor(DateTime date);
    IDeliveryRouteBuilder WithEstimatedTrip(double distanceKm, double durationMinutes);

    /// <summary>Entrega la ruta construida y reinicia el builder para un uso posterior.</summary>
    DeliveryRoute Build();
}
