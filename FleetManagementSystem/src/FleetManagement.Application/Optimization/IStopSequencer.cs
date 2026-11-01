using FleetManagement.Application.Geo;

namespace FleetManagement.Application.Optimization;

/// <summary>
/// PATRÓN STRATEGY: ordena las entregas de UN vehículo. <see cref="PriorityStopSequencer"/> las ordena para recorrer lo
/// menos posible (urgentes primero); <see cref="GivenOrderStopSequencer"/> las deja como llegaron (línea base).
/// </summary>
public interface IStopSequencer
{
    /// <summary>Las mismas entregas, en el orden en que el vehículo debe visitarlas saliendo de <paramref name="depot"/>.</summary>
    IReadOnlyList<PendingDelivery> Sequence(GeoCoordinate depot, IReadOnlyList<PendingDelivery> deliveries);
}
