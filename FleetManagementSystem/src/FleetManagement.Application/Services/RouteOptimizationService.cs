using FleetManagement.Application.DTOs;
using FleetManagement.Application.Flyweights;
using FleetManagement.Application.Geo;
using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Optimization;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using NetTopologySuite.Geometries;

namespace FleetManagement.Application.Services;

/// <summary>
/// Caso de uso "optimizar una ruta": busca el mejor orden de sus paradas con un <see cref="IRouteOptimizer"/> (PATRÓN
/// STRATEGY; en la aplicación, vecino más cercano mejorado con 2-opt). El origen y el destino de la ruta se respetan: solo se
/// reordenan las paradas intermedias.
///
/// Solo propone un cambio si ahorra al menos <see cref="MinimumImprovementKm"/>; si no, deja el orden que ya tenía la
/// ruta (nunca se empeora). Al aplicarlo, la distancia y el tiempo estimados de la ruta se recalculan con la misma regla
/// con la que se calcularon al crearla (línea recta entre puntos y velocidad del tipo de vehículo asignado).
/// </summary>
public class RouteOptimizationService : IRouteOptimizationService
{
    /// <summary>Mejora mínima para proponer un cambio (10 m): por debajo es ruido de redondeo y no vale la pena reordenar.</summary>
    public const double MinimumImprovementKm = 0.01;

    private readonly IDeliveryRouteRepository _routes;
    private readonly IVehicleRepository _vehicles;
    private readonly IRouteOptimizer _optimizer;
    private readonly IFleetAuditLogger _auditLogger;
    private readonly IVehicleTypeProfileFactory? _profiles;

    public RouteOptimizationService(
        IDeliveryRouteRepository routes,
        IVehicleRepository vehicles,
        IRouteOptimizer optimizer,
        IFleetAuditLogger auditLogger,
        IVehicleTypeProfileFactory? profiles = null)
    {
        _routes = routes;
        _vehicles = vehicles;
        _optimizer = optimizer;
        _auditLogger = auditLogger;
        _profiles = profiles;
    }

    public async Task<RouteOptimizationDto> ProposeAsync(Guid routeId)
    {
        var route = await FindAsync(routeId);
        return ToDto(route, await EvaluateAsync(route), applied: false);
    }

    public async Task<RouteOptimizationDto> ApplyAsync(Guid routeId)
    {
        var route = await FindAsync(routeId);
        if (route.Status != DeliveryRouteStatus.Planned)
            throw new InvalidOperationException($"Solo se pueden reordenar las paradas de una ruta planificada; la ruta '{route.Name}' está {RouteStatusText.Describe(route.Status)}.");

        var evaluation = await EvaluateAsync(route);
        if (!evaluation.Improves)
            return ToDto(route, evaluation, applied: false);

        // El DTO se arma antes de tocar la entidad: reordenar cambia los Order de las mismas paradas que describen "lo actual".
        var result = ToDto(route, evaluation, applied: true);

        var byOrder = route.Waypoints.OrderBy(waypoint => waypoint.Order).ToList();
        route.Waypoints = evaluation.Order.Select((index, position) =>
        {
            var waypoint = byOrder[index];
            waypoint.Order = position;
            return waypoint;
        }).ToList();
        route.EstimatedDistanceKm = evaluation.ProposedKm;
        route.EstimatedDurationMinutes = evaluation.ProposedMinutes;
        await _routes.UpdateAsync(route);

        _auditLogger.LogEvent("Ruta", $"Paradas de la ruta '{route.Name}' reordenadas: {evaluation.CurrentKm} km → {evaluation.ProposedKm} km (ahorro de {result.DistanceSavedKm} km).");
        return result;
    }

    private async Task<DeliveryRoute> FindAsync(Guid routeId)
        => await _routes.GetByIdAsync(routeId) ?? throw new KeyNotFoundException("Ruta no encontrada.");

    private async Task<Evaluation> EvaluateAsync(DeliveryRoute route)
    {
        var current = route.Waypoints.OrderBy(waypoint => waypoint.Order).ToList();
        var origin = ToCoordinate(route.Origin);
        var destination = ToCoordinate(route.Destination);
        var stops = current.Select(waypoint => ToCoordinate(waypoint.Location)).ToList();

        var order = Enumerable.Range(0, stops.Count).ToList();
        var currentKm = TourLength.Km(origin, stops, destination);
        var proposedKm = currentKm;

        if (stops.Count >= 2)
        {
            var candidate = _optimizer.Order(origin, stops, destination);
            var candidateKm = TourLength.Km(origin, stops, candidate, destination);
            if (candidateKm < currentKm - MinimumImprovementKm)
            {
                order = candidate.ToList();
                proposedKm = candidateKm;
            }
        }

        var speedKmh = await SpeedKmhAsync(route);
        return new Evaluation(
            current, order,
            Math.Round(currentKm, 2), Math.Round(proposedKm, 2),
            Math.Round(TripEstimate.Minutes(currentKm, speedKmh), 1), Math.Round(TripEstimate.Minutes(proposedKm, speedKmh), 1));
    }

    private async Task<double> SpeedKmhAsync(DeliveryRoute route)
    {
        var vehicle = route.AssignedVehicleId is { } vehicleId ? await _vehicles.GetByIdAsync(vehicleId) : null;
        return VehicleSpeed.KmPerHour(vehicle, _profiles);
    }

    private static RouteOptimizationDto ToDto(DeliveryRoute route, Evaluation evaluation, bool applied)
    {
        var saved = Math.Round(evaluation.CurrentKm - evaluation.ProposedKm, 2);
        return new RouteOptimizationDto(
            route.Id,
            route.Name,
            route.Status.ToString(),
            new RouteSequenceDto(Sequence(evaluation.Current, Enumerable.Range(0, evaluation.Current.Count)), evaluation.CurrentKm, evaluation.CurrentMinutes),
            new RouteSequenceDto(Sequence(evaluation.Current, evaluation.Order), evaluation.ProposedKm, evaluation.ProposedMinutes),
            saved,
            evaluation.CurrentKm > 0 ? Math.Round(saved / evaluation.CurrentKm * 100, 1) : 0,
            Math.Round(evaluation.CurrentMinutes - evaluation.ProposedMinutes, 1),
            evaluation.Improves,
            applied);
    }

    /// <summary>Las paradas en el orden indicado, numeradas desde 0 según su posición (no según el número que guardaban).</summary>
    private static List<WaypointDto> Sequence(IReadOnlyList<Waypoint> waypoints, IEnumerable<int> order)
        => order.Select((index, position) =>
        {
            var waypoint = waypoints[index];
            return new WaypointDto(waypoint.Location.Y, waypoint.Location.X, waypoint.Label, position);
        }).ToList();

    private static GeoCoordinate ToCoordinate(Point point) => new(point.Y, point.X);

    /// <summary>Lo que se calculó de una ruta: las paradas como están, el orden propuesto (índices sobre ellas) y sus medidas ya redondeadas.</summary>
    private sealed record Evaluation(
        IReadOnlyList<Waypoint> Current,
        IReadOnlyList<int> Order,
        double CurrentKm,
        double ProposedKm,
        double CurrentMinutes,
        double ProposedMinutes)
    {
        /// <summary>Solo se cambia el orden cuando de verdad mejora; si no, <see cref="Order"/> es el orden actual.</summary>
        public bool Improves => Order.Where((index, position) => index != position).Any();
    }
}
