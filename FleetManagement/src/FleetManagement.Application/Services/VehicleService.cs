using FleetManagement.Application.DTOs;
using FleetManagement.Application.Factories;
using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Services;

/// <summary>
/// Caso de uso de Vehículos. Cumple el Principio de Responsabilidad Única
/// (SRP): coordina repositorios y fábricas, pero no contiene lógica HTTP
/// (eso vive en FleetController) ni lógica de acceso a datos (eso vive en
/// InMemoryVehicleRepository).
/// </summary>
public class VehicleService : IVehicleService
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IDriverRepository _driverRepository;
    private readonly VehicleFactoryProvider _vehicleFactoryProvider;             // Factory Method
    private readonly FleetOnboardingFactoryProvider _onboardingFactoryProvider;  // Abstract Factory
    private readonly IFleetAuditLogger _auditLogger;                             // Singleton (vía interfaz)

    public VehicleService(
        IVehicleRepository vehicleRepository,
        IDriverRepository driverRepository,
        VehicleFactoryProvider vehicleFactoryProvider,
        FleetOnboardingFactoryProvider onboardingFactoryProvider,
        IFleetAuditLogger auditLogger)
    {
        _vehicleRepository = vehicleRepository;
        _driverRepository = driverRepository;
        _vehicleFactoryProvider = vehicleFactoryProvider;
        _onboardingFactoryProvider = onboardingFactoryProvider;
        _auditLogger = auditLogger;
    }

    public async Task<IReadOnlyList<VehicleDto>> GetAllVehiclesAsync()
    {
        var vehicles = await _vehicleRepository.GetAllAsync();
        var drivers = await _driverRepository.GetAllAsync();
        return vehicles.Select(v => MapToDto(v, drivers)).ToList();
    }

    public async Task<VehicleDto?> GetVehicleByIdAsync(Guid id)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(id);
        if (vehicle is null) return null;
        var drivers = await _driverRepository.GetAllAsync();
        return MapToDto(vehicle, drivers);
    }

    public async Task<VehicleDto> CreateVehicleAsync(CreateVehicleRequest request)
    {
        if (!Enum.TryParse<VehicleType>(request.Type, true, out var vehicleType))
            throw new ArgumentException($"Tipo de vehículo inválido: '{request.Type}'. Valores válidos: Truck, Van, Car, Motorcycle.");

        var existing = await _vehicleRepository.GetByLicensePlateAsync(request.LicensePlate);
        if (existing is not null)
            throw new ArgumentException($"Ya existe un vehículo registrado con la placa '{request.LicensePlate}'.");

        // 1) FACTORY METHOD: construir el vehículo con los valores por defecto de su tipo.
        var vehicleFactory = _vehicleFactoryProvider.GetFactory(vehicleType);
        var vehicle = vehicleFactory.CreateVehicle(request.LicensePlate, request.Brand, request.Model, request.Year, request.Latitude, request.Longitude);

        // 2) ABSTRACT FACTORY: construir la familia de objetos de soporte (plan de
        //    mantenimiento + perfil de navegación) coherente con ese mismo tipo de vehículo.
        var onboardingFactory = _onboardingFactoryProvider.GetFactory(vehicleType);
        var maintenancePlan = onboardingFactory.CreateMaintenancePlan();
        var navigationProfile = onboardingFactory.CreateNavigationProfile();

        var saved = await _vehicleRepository.AddAsync(vehicle);

        _auditLogger.LogEvent(
            "Vehículo",
            $"Vehículo {saved.LicensePlate} ({vehicleType}) registrado. Plan de mantenimiento asignado con {maintenancePlan.Tasks.Count} tareas; perfil de navegación '{navigationProfile.ProfileName}' (máx. {navigationProfile.MaxSpeedKmh} km/h).");

        var drivers = await _driverRepository.GetAllAsync();
        return MapToDto(saved, drivers);
    }

    public async Task<VehicleDto> CloneVehicleAsync(Guid templateVehicleId, CloneVehicleRequest request)
    {
        var template = await _vehicleRepository.GetByIdAsync(templateVehicleId)
            ?? throw new KeyNotFoundException("Vehículo plantilla no encontrado.");

        var existing = await _vehicleRepository.GetByLicensePlateAsync(request.NewLicensePlate);
        if (existing is not null)
            throw new ArgumentException($"Ya existe un vehículo registrado con la placa '{request.NewLicensePlate}'.");

        // PROTOTYPE: clonar el vehículo plantilla (copia profunda) en lugar de reconstruirlo desde cero.
        var clone = template.Clone();
        clone.LicensePlate = request.NewLicensePlate;

        var saved = await _vehicleRepository.AddAsync(clone);

        _auditLogger.LogEvent(
            "Vehículo",
            $"Vehículo {saved.LicensePlate} creado clonando la plantilla {template.LicensePlate} (patrón Prototype).");

        var drivers = await _driverRepository.GetAllAsync();
        return MapToDto(saved, drivers);
    }

    public async Task UpdateVehicleStatusAsync(Guid id, UpdateVehicleStatusRequest request)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Vehículo no encontrado.");

        if (!Enum.TryParse<VehicleStatus>(request.Status, true, out var newStatus))
            throw new ArgumentException($"Estado de vehículo inválido: '{request.Status}'.");

        var previousStatus = vehicle.Status;
        vehicle.Status = newStatus;
        await _vehicleRepository.UpdateAsync(vehicle);

        _auditLogger.LogEvent("Vehículo", $"Vehículo {vehicle.LicensePlate} cambió de estado: {previousStatus} -> {newStatus}.");
    }

    private static VehicleDto MapToDto(Vehicle v, IReadOnlyList<Driver> drivers)
    {
        var driver = v.AssignedDriverId is null ? null : drivers.FirstOrDefault(d => d.Id == v.AssignedDriverId);
        return new VehicleDto(
            v.Id, v.LicensePlate, v.Brand, v.Model, v.Year, v.Type.ToString(), v.Status.ToString(),
            v.CapacityKg, v.MileageKm, v.CurrentLocation.Y, v.CurrentLocation.X,
            v.AssignedDriverId, driver?.FullName, v.RegisteredAt, v.LastMaintenanceDate);
    }
}
