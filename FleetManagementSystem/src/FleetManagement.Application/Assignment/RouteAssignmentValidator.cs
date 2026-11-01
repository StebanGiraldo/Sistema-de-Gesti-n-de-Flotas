using System.Globalization;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Assignment;

/// <summary>
/// Implementación de <see cref="IRouteAssignmentValidator"/>. Cada regla es un método pequeño que devuelve su mensaje
/// (o <c>null</c> si se cumple); el validador junta los mensajes y lanza una sola excepción.
///
/// El estado "vencido" del mantenimiento lo decide <see cref="IMaintenanceService"/> (módulo predictivo): aquí no se
/// repite esa lógica, así que si el módulo mejora, estas reglas lo aprovechan sin cambios.
/// </summary>
public class RouteAssignmentValidator : IRouteAssignmentValidator
{
    // Las sumas de pesos decimales (0.1 + 0.2) no son exactas: sin tolerancia, una carga justo del tamaño del vehículo fallaría.
    private const double Tolerance = 1e-9;

    private readonly IVehicleRepository _vehicleRepository;
    private readonly IDriverRepository _driverRepository;
    private readonly IDeliveryRouteRepository _routeRepository;
    private readonly IMaintenanceService _maintenanceService;

    public RouteAssignmentValidator(
        IVehicleRepository vehicleRepository,
        IDriverRepository driverRepository,
        IDeliveryRouteRepository routeRepository,
        IMaintenanceService maintenanceService)
    {
        _vehicleRepository = vehicleRepository;
        _driverRepository = driverRepository;
        _routeRepository = routeRepository;
        _maintenanceService = maintenanceService;
    }

    public async Task ValidateNewRouteAsync(Guid? vehicleId, Guid? driverId, IEnumerable<CargoItemDto>? cargo)
    {
        var violations = new List<string>();
        if (vehicleId is { } vehicle)
            violations.AddRange(await NewRouteVehicleViolationsAsync(vehicle, (cargo ?? Enumerable.Empty<CargoItemDto>()).ToList()));
        if (driverId is { } driver && await _driverRepository.GetByIdAsync(driver) is null)
            violations.Add("El conductor asignado no existe.");

        ThrowIfAny(violations);
    }

    public async Task ValidateStartAsync(Guid routeId)
    {
        var route = await _routeRepository.GetByIdAsync(routeId);
        // Si no existe, el servicio responde 404; si ya está en curso, no hay nada nuevo que validar (repetir "En progreso" es inofensivo).
        if (route is null || IsActive(route)) return;

        var otherActiveRoutes = (await _routeRepository.GetAllAsync()).Where(r => r.Id != route.Id && IsActive(r)).ToList();
        var violations = new List<string>();
        if (route.AssignedVehicleId is { } vehicleId)
            violations.AddRange(await StartVehicleViolationsAsync(vehicleId, otherActiveRoutes));
        if (route.AssignedDriverId is { } driverId)
            violations.AddRange(await DriverBusyViolationAsync(driverId, otherActiveRoutes));

        ThrowIfAny(violations);
    }

    private async Task<IReadOnlyList<string>> NewRouteVehicleViolationsAsync(Guid vehicleId, IReadOnlyCollection<CargoItemDto> cargo)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(vehicleId);
        if (vehicle is null) return new[] { "El vehículo asignado no existe." };

        return new[]
        {
            OperabilityViolation(vehicle),
            await MaintenanceViolationAsync(vehicle),
            WeightViolation(vehicle, cargo),
            VolumeViolation(vehicle, cargo)
        }.OfType<string>().ToList();
    }

    private async Task<IReadOnlyList<string>> StartVehicleViolationsAsync(Guid vehicleId, IReadOnlyCollection<DeliveryRoute> otherActiveRoutes)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(vehicleId);
        if (vehicle is null) return new[] { "El vehículo asignado ya no existe." };

        // Se mira si hay otra RUTA activa con el vehículo, no solo su estado "En ruta": ese estado se puede cambiar a mano.
        var busyWith = otherActiveRoutes.FirstOrDefault(r => r.AssignedVehicleId == vehicleId);
        return new[]
        {
            OperabilityViolation(vehicle),
            await MaintenanceViolationAsync(vehicle),
            busyWith is null ? null : $"El vehículo {vehicle.LicensePlate} ya está en otro viaje activo (ruta '{busyWith.Name}')."
        }.OfType<string>().ToList();
    }

    private async Task<IReadOnlyList<string>> DriverBusyViolationAsync(Guid driverId, IReadOnlyCollection<DeliveryRoute> otherActiveRoutes)
    {
        var busyWith = otherActiveRoutes.FirstOrDefault(r => r.AssignedDriverId == driverId);
        if (busyWith is null) return Array.Empty<string>();

        var driver = await _driverRepository.GetByIdAsync(driverId);
        return new[] { $"El conductor {driver?.FullName ?? "asignado"} ya está en otro viaje activo (ruta '{busyWith.Name}')." };
    }

    private static string? OperabilityViolation(Vehicle vehicle) => vehicle.Status switch
    {
        VehicleStatus.Maintenance => $"El vehículo {vehicle.LicensePlate} está en mantenimiento; no se puede usar en una ruta.",
        VehicleStatus.OutOfService => $"El vehículo {vehicle.LicensePlate} está fuera de servicio; no se puede usar en una ruta.",
        _ => null
    };

    private async Task<string?> MaintenanceViolationAsync(Vehicle vehicle)
    {
        var overdueTasks = (await _maintenanceService.GetVehiclesDueForMaintenanceAsync())
            .Where(due => due.VehicleId == vehicle.Id && due.IsOverdue)
            .Select(due => due.TaskName)
            .Distinct()
            .ToList();

        return overdueTasks.Count == 0
            ? null
            : $"El vehículo {vehicle.LicensePlate} tiene mantenimiento vencido ({string.Join(", ", overdueTasks)}); regístrelo antes de usarlo.";
    }

    private static string? WeightViolation(Vehicle vehicle, IEnumerable<CargoItemDto> cargo)
    {
        var totalKg = cargo.Sum(item => item.WeightKg);
        return totalKg <= vehicle.CapacityKg + Tolerance
            ? null
            : $"La carga ({Format(totalKg)} kg) supera la capacidad del vehículo {vehicle.LicensePlate} ({Format(vehicle.CapacityKg)} kg).";
    }

    private static string? VolumeViolation(Vehicle vehicle, IEnumerable<CargoItemDto> cargo)
    {
        if (vehicle.CapacityM3 is not { } capacityM3) return null; // volumen no declarado: no hay límite que comprobar

        var totalM3 = cargo.Sum(item => item.VolumeM3);
        return totalM3 <= capacityM3 + Tolerance
            ? null
            : $"El volumen de la carga ({Format(totalM3)} m³) supera la capacidad del vehículo {vehicle.LicensePlate} ({Format(capacityM3)} m³).";
    }

    private static bool IsActive(DeliveryRoute route)
        => route.Status is DeliveryRouteStatus.InProgress or DeliveryRouteStatus.Delayed;

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static void ThrowIfAny(IReadOnlyList<string> violations)
    {
        if (violations.Count > 0) throw new AssignmentRuleException(violations);
    }
}
