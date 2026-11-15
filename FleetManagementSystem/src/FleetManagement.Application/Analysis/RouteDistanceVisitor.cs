using FleetManagement.Application.DTOs;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Analysis;

/// <summary>
/// CONCRETE VISITOR #2 (patrón VISITOR): análisis de recorridos y distancias. Visita solo rutas y calcula cuántos kilómetros hay
/// en cada estado (completados, en curso, planificados), la ruta más larga, el promedio y los retrasos acumulados. La distancia
/// de cada ruta es la estimada que la ruta ya guarda (<c>EstimatedDistanceKm</c>); este visitante no vuelve a calcularla.
///
/// Las rutas canceladas no se recorren: no suman a los totales ni al promedio, pero el desglose por estado las muestra aparte.
/// </summary>
public sealed class RouteDistanceVisitor : FleetVisitor<RouteDistanceAnalysisDto>
{
    private const int DistanceDecimals = 3;
    private const int DurationDecimals = 1;

    private readonly Dictionary<Guid, RouteElement> _routes = new();

    public override void VisitRoute(RouteElement route) => _routes[route.Id] = route;

    public override RouteDistanceAnalysisDto Result
    {
        get
        {
            var travelled = _routes.Values.Where(route => route.Status != DeliveryRouteStatus.Cancelled).ToList();

            return new RouteDistanceAnalysisDto(
                travelled.Count,
                DistanceOf(travelled),
                DistanceOf(travelled.Where(route => route.Status == DeliveryRouteStatus.Completed)),
                DistanceOf(travelled.Where(route => route.IsActive)),
                DistanceOf(travelled.Where(route => route.Status == DeliveryRouteStatus.Planned)),
                travelled.Count == 0 ? 0 : Math.Round(travelled.Average(route => route.DistanceKm), DistanceDecimals),
                LongestOf(travelled),
                travelled.Count(route => route.DelayMinutes > 0),
                travelled.Sum(route => route.DelayMinutes),
                ByStatus(),
                _routes.Values.OrderByDescending(route => route.DistanceKm).ThenBy(route => route.Name, StringComparer.Ordinal).Select(ToDto).ToList());
        }
    }

    private static double DistanceOf(IEnumerable<RouteElement> routes)
        => Math.Round(routes.Sum(route => route.DistanceKm), DistanceDecimals);

    private static RouteDistanceDto? LongestOf(IEnumerable<RouteElement> routes)
        => routes.OrderByDescending(route => route.DistanceKm).ThenBy(route => route.Name, StringComparer.Ordinal).Select(ToDto).FirstOrDefault();

    private IReadOnlyList<RouteStatusDistanceDto> ByStatus()
        => Enum.GetValues<DeliveryRouteStatus>()
            .Select(status =>
            {
                var inStatus = _routes.Values.Where(route => route.Status == status).ToList();
                return new RouteStatusDistanceDto(status.ToString(), inStatus.Count, DistanceOf(inStatus));
            })
            .ToList();

    private static RouteDistanceDto ToDto(RouteElement route)
        => new(
            route.Id,
            route.Name,
            route.Status.ToString(),
            Math.Round(route.DistanceKm, DistanceDecimals),
            route.StopCount,
            Math.Round(route.EstimatedDurationMinutes, DurationDecimals),
            route.DelayMinutes);
}
