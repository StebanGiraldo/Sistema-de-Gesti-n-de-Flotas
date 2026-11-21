using FleetManagement.Application.DTOs;
using FleetManagement.Application.Flyweights;
using FleetManagement.Application.Geo;
using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Mediators;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Assignment;

/// <summary>
/// Implementación de <see cref="IRouteAssignmentService"/>. No repite reglas: la capacidad, el estado del vehículo y el
/// mantenimiento vencido los decide <see cref="IRouteAssignmentValidator"/> (la misma fuente que usa la creación de rutas);
/// aquí solo se añade lo propio de asignar sobre una ruta existente (qué falta, quién está ocupado) y se aplica el cambio.
/// Si la ruta ya está en curso, el cambio pasa por el participante de rutas del MEDIATOR para que el vehículo quede En ruta.
/// </summary>
public class RouteAssignmentService : IRouteAssignmentService
{
    private readonly IDeliveryRouteRepository _routes;
    private readonly IVehicleRepository _vehicles;
    private readonly IDriverRepository _drivers;
    private readonly IRouteAssignmentValidator _validator;
    private readonly IRouteColleague _routeLifecycle;
    private readonly IDeliveryRouteService _routeQueries;
    private readonly IFleetAuditLogger _auditLogger;
    private readonly IVehicleTypeProfileFactory? _profiles;

    public RouteAssignmentService(
        IDeliveryRouteRepository routes,
        IVehicleRepository vehicles,
        IDriverRepository drivers,
        IRouteAssignmentValidator validator,
        IRouteColleague routeLifecycle,
        IDeliveryRouteService routeQueries,
        IFleetAuditLogger auditLogger,
        IVehicleTypeProfileFactory? profiles = null)
    {
        _routes = routes;
        _vehicles = vehicles;
        _drivers = drivers;
        _validator = validator;
        _routeLifecycle = routeLifecycle;
        _routeQueries = routeQueries;
        _auditLogger = auditLogger;
        _profiles = profiles;
    }

    public async Task<DeliveryRouteDto> AssignAsync(Guid routeId, AssignRouteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.DriverId is null && request.VehicleId is null)
            throw new ArgumentException("Indique el conductor o el vehículo que se asignará a la ruta.");

        var route = await _routes.GetByIdAsync(routeId) ?? throw new KeyNotFoundException("Ruta no encontrada.");
        if (route.Status is DeliveryRouteStatus.Completed or DeliveryRouteStatus.Cancelled)
            throw new AssignmentRuleException(new[] { $"La ruta '{route.Name}' ya está cerrada ({route.Status}); no admite asignaciones." });

        var violations = new List<string>();
        var vehicleId = PendingAssignment(route.AssignedVehicleId, request.VehicleId, "vehículo", violations);
        var driverId = PendingAssignment(route.AssignedDriverId, request.DriverId, "conductor", violations);
        if (violations.Count == 0)
            violations.AddRange(await RuleViolationsAsync(route, vehicleId, driverId));
        if (violations.Count > 0)
            throw new AssignmentRuleException(violations);

        // Lo pedido ya estaba asignado tal cual: no hay nada que cambiar ni que auditar.
        if (vehicleId is null && driverId is null)
            return (await _routeQueries.GetRouteByIdAsync(route.Id))!;

        await ApplyAsync(route, vehicleId, driverId);
        _auditLogger.LogEvent("Ruta", DescribeAssignment(route, vehicleId, driverId));

        return (await _routeQueries.GetRouteByIdAsync(route.Id))!;
    }

    public async Task<RouteAssignmentOptionsDto> GetOptionsAsync(Guid routeId)
    {
        var route = await _routes.GetByIdAsync(routeId) ?? throw new KeyNotFoundException("Ruta no encontrada.");
        var activeRoutes = await ActiveRoutesExceptAsync(route);

        var drivers = (await _drivers.GetAllAsync())
            .Where(driver => activeRoutes.All(active => active.AssignedDriverId != driver.Id))
            .Select(driver => new AssignableDriverDto(driver.Id, driver.FullName, driver.LicenseNumber))
            .OrderBy(driver => driver.FullName)
            .ToList();

        var vehicles = new List<AssignableVehicleDto>();
        foreach (var vehicle in (await _vehicles.GetAllAsync()).OrderBy(v => v.LicensePlate))
        {
            if (activeRoutes.Any(active => active.AssignedVehicleId == vehicle.Id)) continue;
            if ((await VehicleViolationsAsync(route, vehicle.Id)).Count > 0) continue;
            vehicles.Add(new AssignableVehicleDto(vehicle.Id, vehicle.LicensePlate, vehicle.Type.ToString(), vehicle.Status.ToString(), vehicle.CapacityKg, vehicle.CapacityM3));
        }

        return new RouteAssignmentOptionsDto(drivers, vehicles);
    }

    /// <summary>Lo que realmente hay que asignar: <c>null</c> si no se pidió, o si la ruta ya lo tiene igual. Si ya tiene a otro, es una violación.</summary>
    private static Guid? PendingAssignment(Guid? current, Guid? requested, string what, List<string> violations)
    {
        if (requested is null || requested == current) return null;
        if (current is not null)
            violations.Add($"La ruta ya tiene {what} asignado; esta operación solo completa lo que falta.");
        return current is null ? requested : null;
    }

    private async Task<IReadOnlyList<string>> RuleViolationsAsync(DeliveryRoute route, Guid? vehicleId, Guid? driverId)
    {
        var violations = new List<string>();
        try
        {
            await _validator.ValidateNewRouteAsync(vehicleId, driverId, CargoOf(route));
        }
        catch (AssignmentRuleException ex)
        {
            violations.AddRange(ex.Violations);
        }

        var activeRoutes = await ActiveRoutesExceptAsync(route);
        if (driverId is { } driver && activeRoutes.FirstOrDefault(r => r.AssignedDriverId == driver) is { } driverTrip)
            violations.Add($"El conductor {(await _drivers.GetByIdAsync(driver))?.FullName ?? "elegido"} ya está en otro viaje activo (ruta '{driverTrip.Name}').");
        if (vehicleId is { } vehicle && IsActive(route) && activeRoutes.FirstOrDefault(r => r.AssignedVehicleId == vehicle) is { } vehicleTrip)
            violations.Add($"El vehículo {(await _vehicles.GetByIdAsync(vehicle))?.LicensePlate ?? "elegido"} ya está en otro viaje activo (ruta '{vehicleTrip.Name}').");

        return violations;
    }

    private async Task<IReadOnlyList<string>> VehicleViolationsAsync(DeliveryRoute route, Guid vehicleId)
    {
        try
        {
            await _validator.ValidateNewRouteAsync(vehicleId, null, CargoOf(route));
            return Array.Empty<string>();
        }
        catch (AssignmentRuleException ex)
        {
            return ex.Violations;
        }
    }

    private async Task ApplyAsync(DeliveryRoute route, Guid? vehicleId, Guid? driverId)
    {
        if (vehicleId is { } vehicle)
        {
            route.AssignedVehicleId = vehicle;
            // El tiempo estimado depende de la velocidad del tipo de vehículo (igual que al crear la ruta con vehículo).
            var speedKmh = VehicleSpeed.KmPerHour(await _vehicles.GetByIdAsync(vehicle), _profiles);
            route.EstimatedDurationMinutes = Math.Round(TripEstimate.Minutes(route.EstimatedDistanceKm, speedKmh), 1);
        }
        if (driverId is { } driver)
            route.AssignedDriverId = driver;

        // En curso: se avisa al mediador con el mismo estado para que el vehículo recién asignado quede En ruta.
        if (IsActive(route))
            await _routeLifecycle.ChangeStatusAsync(route, route.Status);
        else
            await _routes.UpdateAsync(route);
    }

    private string DescribeAssignment(DeliveryRoute route, Guid? vehicleId, Guid? driverId)
    {
        var parts = new List<string>();
        if (driverId is not null) parts.Add("conductor");
        if (vehicleId is not null) parts.Add("vehículo");
        return $"Ruta '{route.Name}': se asignó {string.Join(" y ", parts)}.";
    }

    private async Task<IReadOnlyList<DeliveryRoute>> ActiveRoutesExceptAsync(DeliveryRoute route)
        => (await _routes.GetAllAsync()).Where(other => other.Id != route.Id && IsActive(other)).ToList();

    private static IEnumerable<CargoItemDto> CargoOf(DeliveryRoute route)
        => route.CargoItems.Select(c => new CargoItemDto(c.Description, c.WeightKg, c.VolumeM3, c.Priority.ToString()));

    private static bool IsActive(DeliveryRoute route)
        => route.Status is DeliveryRouteStatus.InProgress or DeliveryRouteStatus.Delayed;
}
