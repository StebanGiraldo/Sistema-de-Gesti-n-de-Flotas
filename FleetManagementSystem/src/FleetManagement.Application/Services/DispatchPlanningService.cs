using FleetManagement.Application.Assignment;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Flyweights;
using FleetManagement.Application.Geo;
using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Optimization;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Services;

/// <summary>
/// Caso de uso "proponer un plan de reparto": dado un punto de salida y las entregas por hacer, decide qué vehículo lleva
/// cada una (respetando peso y volumen), en qué orden las visita y cuánto recorre y tarda. Los pasos de cálculo son
/// estrategias intercambiables (<see cref="ICargoAssignmentStrategy"/>, <see cref="IStopSequencer"/>, PATRÓN STRATEGY) que
/// arma el <see cref="DispatchPlanner"/>; este servicio solo reúne los datos, aplica las reglas y presenta el resultado.
///
/// Siempre calcula también el plan "sin optimizar" (primer vehículo con espacio y paradas en el orden recibido) para mostrar
/// cuánto se ahorra. Solo salen a reparto los vehículos que pueden salir hoy: disponibles, sin mantenimiento vencido y sin
/// otro viaje activo; los demás se listan con su motivo. Las entregas que no caben en ninguno quedan sin asignar, también
/// con su motivo.
///
/// <see cref="PlanAsync"/> no guarda nada. <see cref="ApplyAsync"/> crea una ruta planificada por viaje a través de
/// <see cref="IDeliveryRouteService"/> (la versión protegida por el Decorator de reglas de asignación).
/// </summary>
public class DispatchPlanningService : IDispatchPlanningService
{
    /// <summary>Máximo de entregas por plan: acota el costo del cálculo y mantiene el plan legible.</summary>
    public const int MaxDeliveries = 200;

    private const string LongerThanBaselineNote =
        "El plan optimizado recorre más que el orden simple: no busca solo los kilómetros, también atiende primero las entregas urgentes y reduce el número de vehículos.";

    // Línea base: lo que saldría sin optimizar (primer vehículo con espacio y paradas en el orden recibido).
    private static readonly DispatchPlanner Baseline = new(new FirstFitInOrderStrategy(), new GivenOrderStopSequencer());

    private readonly IVehicleRepository _vehicles;
    private readonly IDeliveryRouteRepository _routes;
    private readonly IRouteAssignmentValidator _validator;
    private readonly IDeliveryRouteService _routeService;
    private readonly DispatchPlanner _planner;
    private readonly IFleetAuditLogger _auditLogger;
    private readonly IVehicleTypeProfileFactory? _profiles;

    public DispatchPlanningService(
        IVehicleRepository vehicles,
        IDeliveryRouteRepository routes,
        IRouteAssignmentValidator validator,
        IDeliveryRouteService routeService,
        DispatchPlanner planner,
        IFleetAuditLogger auditLogger,
        IVehicleTypeProfileFactory? profiles = null)
    {
        _vehicles = vehicles;
        _routes = routes;
        _validator = validator;
        _routeService = routeService;
        _planner = planner;
        _auditLogger = auditLogger;
        _profiles = profiles;
    }

    public async Task<DispatchPlanDto> PlanAsync(DispatchPlanRequest request)
    {
        var computation = await ComputeAsync(request);
        return new DispatchPlanDto(
            computation.Depot.Latitude, computation.Depot.Longitude,
            computation.Optimized.Trips.Select(ToTripDto).ToList(),
            computation.Optimized.Unassigned.Select(ToUnassignedDto).ToList(),
            computation.Excluded.Select(excluded => new DispatchExcludedVehicleDto(excluded.VehicleId, excluded.LicensePlate, excluded.Reason)).ToList(),
            ToTotals(computation.Optimized),
            ToTotals(computation.Baseline),
            Compare(computation.Optimized, computation.Baseline));
    }

    public async Task<DispatchApplyResultDto> ApplyAsync(DispatchPlanRequest request)
    {
        var computation = await ComputeAsync(request);
        var plan = computation.Optimized;
        if (plan.Trips.Count == 0)
            throw new InvalidOperationException("Ninguna entrega se pudo asignar a un vehículo disponible; no se creó ninguna ruta.");

        // Todo o nada: se comprueban TODOS los viajes antes de crear el primero, para no dejar un reparto a medias.
        foreach (var trip in plan.Trips)
            await _validator.ValidateNewRouteAsync(trip.Load.Vehicle.Id, null, CargoOf(trip));

        var created = new List<DeliveryRouteDto>();
        foreach (var trip in plan.Trips)
            created.Add(await _routeService.CreateRouteAsync(ToRouteRequest(computation.Depot, trip, request.ScheduledDate)));

        _auditLogger.LogEvent("Despacho", $"Plan de reparto aplicado: {created.Count} ruta(s) planificada(s) con {plan.AssignedCount} entrega(s); {plan.Unassigned.Count} sin asignar.");

        return new DispatchApplyResultDto(created, plan.Unassigned.Select(ToUnassignedDto).ToList(), ToTotals(plan));
    }

    // ------------------------------------------------------------------
    // Cálculo
    // ------------------------------------------------------------------

    private async Task<Computation> ComputeAsync(DispatchPlanRequest request)
    {
        var depot = ParseDepot(request);
        var deliveries = ParseDeliveries(request.Deliveries);
        var (candidates, excluded) = await FindCandidatesAsync(request.VehicleIds);

        return new Computation(
            depot,
            _planner.Plan(depot, deliveries, candidates),
            Baseline.Plan(depot, deliveries, candidates),
            excluded);
    }

    private async Task<(List<CandidateVehicle> Candidates, List<ExcludedVehicle> Excluded)> FindCandidatesAsync(IReadOnlyCollection<Guid>? requestedIds)
    {
        var vehicles = (await _vehicles.GetAllAsync()).ToList();
        if (requestedIds is { Count: > 0 })
        {
            var wanted = requestedIds.ToHashSet();
            if (wanted.Any(id => vehicles.All(vehicle => vehicle.Id != id)))
                throw new ArgumentException("Alguno de los vehículos indicados no existe.");

            vehicles = vehicles.Where(vehicle => wanted.Contains(vehicle.Id)).ToList();
        }

        var activeRoutes = (await _routes.GetAllAsync()).Where(IsActive).ToList();
        var candidates = new List<CandidateVehicle>();
        var excluded = new List<ExcludedVehicle>();

        // Se recorren por placa: el repositorio no garantiza ningún orden y el plan debe ser el mismo para los mismos datos.
        foreach (var vehicle in vehicles.OrderBy(vehicle => vehicle.LicensePlate, StringComparer.Ordinal))
        {
            var reason = await WhyCannotDepartAsync(vehicle, activeRoutes);
            if (reason is null)
                candidates.Add(ToCandidate(vehicle));
            else
                excluded.Add(new ExcludedVehicle(vehicle.Id, vehicle.LicensePlate, reason));
        }

        return (candidates, excluded);
    }

    /// <summary>
    /// Por qué un vehículo no puede salir a un viaje nuevo ahora (null si puede). Que no esté en mantenimiento ni fuera de
    /// servicio y que no tenga mantenimiento vencido lo decide el validador de asignación: es la misma regla que se aplicará al
    /// crear la ruta, así que no se repite aquí. Lo propio de planificar es no proponer un vehículo que ya está en un viaje.
    /// </summary>
    private async Task<string?> WhyCannotDepartAsync(Vehicle vehicle, IReadOnlyCollection<DeliveryRoute> activeRoutes)
    {
        var reasons = new List<string>();

        var activeRoute = activeRoutes.FirstOrDefault(route => route.AssignedVehicleId == vehicle.Id);
        if (activeRoute is not null)
            reasons.Add($"El vehículo {vehicle.LicensePlate} ya está en el viaje activo '{activeRoute.Name}'.");
        else if (vehicle.Status == VehicleStatus.EnRoute)
            reasons.Add($"El vehículo {vehicle.LicensePlate} figura como en ruta.");

        try
        {
            await _validator.ValidateNewRouteAsync(vehicle.Id, null, null);
        }
        catch (AssignmentRuleException rejected)
        {
            reasons.AddRange(rejected.Violations);
        }

        return reasons.Count == 0 ? null : string.Join(" ", reasons);
    }

    private CandidateVehicle ToCandidate(Vehicle vehicle) => new(
        vehicle.Id,
        vehicle.LicensePlate,
        vehicle.Type,
        vehicle.CapacityKg,
        vehicle.CapacityM3,
        new GeoCoordinate(vehicle.CurrentLocation.Y, vehicle.CurrentLocation.X),
        VehicleSpeed.KmPerHour(vehicle, _profiles));

    private static bool IsActive(DeliveryRoute route)
        => route.Status is DeliveryRouteStatus.InProgress or DeliveryRouteStatus.Delayed;

    // ------------------------------------------------------------------
    // Validación de la solicitud
    // ------------------------------------------------------------------

    private static GeoCoordinate ParseDepot(DispatchPlanRequest request)
    {
        if (!IsValidCoordinate(request.OriginLat, request.OriginLng))
            throw new ArgumentException("El punto de salida no es una coordenada válida (latitud entre -90 y 90, longitud entre -180 y 180).");

        return new GeoCoordinate(request.OriginLat, request.OriginLng);
    }

    private static List<PendingDelivery> ParseDeliveries(IReadOnlyList<DispatchDeliveryRequest>? requested)
    {
        if (requested is null || requested.Count == 0)
            throw new ArgumentException("Indique al menos una entrega para repartir.");
        if (requested.Count > MaxDeliveries)
            throw new ArgumentException($"Un plan admite como máximo {MaxDeliveries} entregas; se indicaron {requested.Count}.");

        return requested.Select((item, index) => ParseDelivery(item, index + 1)).ToList();
    }

    private static PendingDelivery ParseDelivery(DispatchDeliveryRequest? item, int number)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Description))
            throw new ArgumentException($"La entrega {number} no tiene descripción.");

        var description = item.Description.Trim();
        if (!double.IsFinite(item.WeightKg) || item.WeightKg <= 0)
            throw new ArgumentException($"La entrega '{description}' debe pesar más de 0 kg.");
        if (!double.IsFinite(item.VolumeM3) || item.VolumeM3 < 0)
            throw new ArgumentException($"El volumen de la entrega '{description}' no puede ser negativo.");
        if (!IsValidCoordinate(item.Latitude, item.Longitude))
            throw new ArgumentException($"El destino de la entrega '{description}' no es una coordenada válida (latitud entre -90 y 90, longitud entre -180 y 180).");

        return new PendingDelivery(
            description,
            item.WeightKg,
            item.VolumeM3,
            ParsePriority(item.Priority, description),
            new GeoCoordinate(item.Latitude, item.Longitude),
            description);
    }

    private static CargoPriority ParsePriority(string? text, string description)
    {
        if (string.IsNullOrWhiteSpace(text))
            return CargoPriority.Standard;

        if (Enum.TryParse<CargoPriority>(text.Trim(), true, out var priority) && Enum.IsDefined(priority))
            return priority;

        throw new ArgumentException($"La prioridad '{text}' de la entrega '{description}' no es válida (Standard, High, Urgent o Fragile).");
    }

    private static bool IsValidCoordinate(double latitude, double longitude)
        => double.IsFinite(latitude) && double.IsFinite(longitude)
           && latitude is >= -90 and <= 90
           && longitude is >= -180 and <= 180;

    // ------------------------------------------------------------------
    // Presentación
    // ------------------------------------------------------------------

    private static DispatchTripDto ToTripDto(PlannedTrip trip)
    {
        var vehicle = trip.Load.Vehicle;
        var loadKg = trip.Load.TotalWeightKg;
        return new DispatchTripDto(
            vehicle.Id,
            vehicle.LicensePlate,
            vehicle.Type.ToString(),
            vehicle.CapacityKg,
            vehicle.CapacityM3,
            Math.Round(loadKg, 3),
            Math.Round(trip.Load.TotalVolumeM3, 3),
            vehicle.CapacityKg > 0 ? Math.Round(loadKg / vehicle.CapacityKg * 100, 1) : 0,
            vehicle.SpeedKmh,
            trip.Stops.Select(ToDeliveryDto).ToList(),
            Math.Round(trip.DistanceKm, 2),
            Math.Round(trip.Minutes, 1));
    }

    private static DispatchDeliveryDto ToDeliveryDto(PendingDelivery delivery) => new(
        delivery.Description,
        delivery.WeightKg,
        delivery.VolumeM3,
        delivery.Priority.ToString(),
        delivery.Destination.Latitude,
        delivery.Destination.Longitude);

    private static DispatchUnassignedDto ToUnassignedDto(UnassignedDelivery unassigned)
        => new(ToDeliveryDto(unassigned.Delivery), unassigned.Reason);

    private static DispatchTotalsDto ToTotals(PlannedDispatch plan) => new(
        plan.AssignedCount,
        plan.Unassigned.Count,
        plan.VehiclesUsed,
        Math.Round(plan.TotalKm, 2),
        Math.Round(plan.TotalMinutes, 1),
        Math.Round(plan.LongestTripMinutes, 1));

    private static DispatchComparisonDto Compare(PlannedDispatch optimized, PlannedDispatch baseline)
    {
        if (optimized.AssignedCount != baseline.AssignedCount)
            return new DispatchComparisonDto(false, 0, 0, 0,
                $"Los dos planes no asignan la misma cantidad de entregas ({optimized.AssignedCount} frente a {baseline.AssignedCount}); sus kilómetros y tiempos no son comparables.");

        var baselineKm = Math.Round(baseline.TotalKm, 2);
        var kmSaved = Math.Round(baselineKm - Math.Round(optimized.TotalKm, 2), 2);
        var minutesSaved = Math.Round(Math.Round(baseline.TotalMinutes, 1) - Math.Round(optimized.TotalMinutes, 1), 1);
        return new DispatchComparisonDto(
            true,
            kmSaved,
            baselineKm > 0 ? Math.Round(kmSaved / baselineKm * 100, 1) : 0,
            minutesSaved,
            kmSaved < 0 ? LongerThanBaselineNote : null);
    }

    // ------------------------------------------------------------------
    // Aplicar el plan
    // ------------------------------------------------------------------

    /// <summary>
    /// Un viaje como ruta: sale del punto de partida, las paradas son todas las entregas menos la última y la última es el
    /// destino. Así la distancia que calcula la ruta (origen → paradas → destino) es exactamente la del plan.
    /// </summary>
    private static CreateDeliveryRouteRequest ToRouteRequest(GeoCoordinate depot, PlannedTrip trip, DateTime? scheduledDate)
    {
        var last = trip.Stops[^1];
        var waypoints = trip.Stops
            .Take(trip.Stops.Count - 1)
            .Select((stop, index) => new WaypointDto(stop.Destination.Latitude, stop.Destination.Longitude, stop.DestinationLabel, index))
            .ToList();

        return new CreateDeliveryRouteRequest(
            $"Despacho {trip.Load.Vehicle.LicensePlate} ({trip.Stops.Count} {(trip.Stops.Count == 1 ? "entrega" : "entregas")})",
            depot.Latitude, depot.Longitude,
            last.Destination.Latitude, last.Destination.Longitude,
            waypoints,
            CargoOf(trip),
            trip.Load.Vehicle.Id,
            null,
            scheduledDate);
    }

    private static List<CargoItemDto> CargoOf(PlannedTrip trip)
        => trip.Stops.Select(stop => new CargoItemDto(stop.Description, stop.WeightKg, stop.VolumeM3, stop.Priority.ToString())).ToList();

    private sealed record ExcludedVehicle(Guid VehicleId, string LicensePlate, string Reason);

    private sealed record Computation(GeoCoordinate Depot, PlannedDispatch Optimized, PlannedDispatch Baseline, IReadOnlyList<ExcludedVehicle> Excluded);
}
