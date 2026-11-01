using FleetManagement.Application.Geo;

namespace FleetManagement.Application.Optimization;

/// <summary>
/// PATRÓN STRATEGY: decide qué vehículo lleva cada entrega. Hay una estrategia ingenua (<see cref="FirstFitInOrderStrategy"/>,
/// la línea base con la que se compara) y una que aprovecha mejor los vehículos (<see cref="PriorityBestFitStrategy"/>).
/// Ninguna sobrepasa la capacidad de un vehículo: lo que no cabe queda sin asignar, con su motivo.
/// </summary>
public interface ICargoAssignmentStrategy
{
    /// <param name="deliveries">Entregas por repartir, en el orden en que se recibieron.</param>
    /// <param name="vehicles">Vehículos disponibles. No se modifican.</param>
    /// <param name="depot">Punto de salida de todos los viajes.</param>
    CargoAssignment Assign(IReadOnlyList<PendingDelivery> deliveries, IReadOnlyList<CandidateVehicle> vehicles, GeoCoordinate depot);
}
