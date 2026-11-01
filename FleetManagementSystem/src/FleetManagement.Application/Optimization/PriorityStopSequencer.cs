using FleetManagement.Application.Geo;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Optimization;

/// <summary>
/// Primero las entregas urgentes (entre sí, por el camino más corto desde el punto de salida) y después las demás (por el
/// camino más corto desde la última urgente). Que lo urgente vaya antes puede alargar el recorrido: es una decisión de
/// negocio, no un error, y por eso un plan con urgentes puede no ahorrar tantos kilómetros como uno sin ellas.
/// Cuando no hay urgentes, todo el recorrido se optimiza de una vez. El orden dentro de cada grupo lo decide el
/// <see cref="IRouteOptimizer"/> recibido.
/// </summary>
public sealed class PriorityStopSequencer : IStopSequencer
{
    private readonly IRouteOptimizer _optimizer;

    public PriorityStopSequencer(IRouteOptimizer optimizer)
    {
        _optimizer = optimizer;
    }

    public IReadOnlyList<PendingDelivery> Sequence(GeoCoordinate depot, IReadOnlyList<PendingDelivery> deliveries)
    {
        var urgent = deliveries.Where(delivery => delivery.Priority == CargoPriority.Urgent).ToList();
        var rest = deliveries.Where(delivery => delivery.Priority != CargoPriority.Urgent).ToList();

        var firstLeg = Optimize(depot, urgent);
        var restStart = firstLeg.Count > 0 ? firstLeg[^1].Destination : depot;
        return firstLeg.Concat(Optimize(restStart, rest)).ToList();
    }

    private IReadOnlyList<PendingDelivery> Optimize(GeoCoordinate from, IReadOnlyList<PendingDelivery> deliveries)
    {
        var order = _optimizer.Order(from, deliveries.Select(delivery => delivery.Destination).ToList());
        return order.Select(index => deliveries[index]).ToList();
    }
}
