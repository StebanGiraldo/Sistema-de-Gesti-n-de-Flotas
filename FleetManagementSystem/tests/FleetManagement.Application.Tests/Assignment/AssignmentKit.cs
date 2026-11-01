using FleetManagement.Application.Assignment;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Persistence;
using Xunit;

namespace FleetManagement.Application.Tests.Assignment;

/// <summary>
/// Repositorios reales en memoria más el módulo de mantenimiento de prueba, con atajos para armar el escenario de cada
/// prueba (un vehículo, un conductor, una ruta en cierto estado) en una línea.
/// </summary>
public sealed class AssignmentKit
{
    public AssignmentKit()
    {
        Validator = new RouteAssignmentValidator(Vehicles, Drivers, Routes, Maintenance);
    }

    public InMemoryVehicleRepository Vehicles { get; } = new();
    public InMemoryDriverRepository Drivers { get; } = new();
    public InMemoryDeliveryRouteRepository Routes { get; } = new();
    public FakeMaintenanceService Maintenance { get; } = new();
    public RouteAssignmentValidator Validator { get; }

    public async Task<Vehicle> AddVehicleAsync(string plate = "VAN-001", VehicleStatus status = VehicleStatus.Available, double capacityKg = 1000, double? capacityM3 = null)
        => await Vehicles.AddAsync(new Vehicle { LicensePlate = plate, Status = status, CapacityKg = capacityKg, CapacityM3 = capacityM3 });

    public async Task<Driver> AddDriverAsync(string name = "Ana Pérez")
        => await Drivers.AddAsync(new Driver { FullName = name });

    public async Task<DeliveryRoute> AddRouteAsync(string name, DeliveryRouteStatus status, Vehicle? vehicle = null, Driver? driver = null)
        => await Routes.AddAsync(new DeliveryRoute { Name = name, Status = status, AssignedVehicleId = vehicle?.Id, AssignedDriverId = driver?.Id });

    /// <summary>Hace que el módulo de mantenimiento informe una tarea vencida para ese vehículo.</summary>
    public void MarkOverdue(Vehicle vehicle, string task = "OilChange")
        => Maintenance.Due.Add(new MaintenanceDueDto(vehicle.Id, vehicle.LicensePlate, task, DateTime.UtcNow.AddDays(-1), null, true));

    public static CargoItemDto Cargo(double kg, double m3 = 0) => new("Carga de prueba", kg, m3, "Standard");

    public async Task<AssignmentRuleException> RejectedNewRouteAsync(Guid? vehicleId, Guid? driverId, params CargoItemDto[] cargo)
        => await Assert.ThrowsAsync<AssignmentRuleException>(() => Validator.ValidateNewRouteAsync(vehicleId, driverId, cargo));

    public async Task<AssignmentRuleException> RejectedStartAsync(Guid routeId)
        => await Assert.ThrowsAsync<AssignmentRuleException>(() => Validator.ValidateStartAsync(routeId));
}
