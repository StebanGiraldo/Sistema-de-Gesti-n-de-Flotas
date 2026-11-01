using FleetManagement.Application.Geo;

namespace FleetManagement.Application.Optimization;

/// <summary>Línea base: las paradas se visitan en el orden en que llegaron, sin reordenar nada.</summary>
public sealed class GivenOrderStopSequencer : IStopSequencer
{
    public IReadOnlyList<PendingDelivery> Sequence(GeoCoordinate depot, IReadOnlyList<PendingDelivery> deliveries)
        => deliveries.ToList();
}
