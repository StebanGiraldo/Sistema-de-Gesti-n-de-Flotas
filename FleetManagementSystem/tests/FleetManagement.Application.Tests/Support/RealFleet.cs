using FleetManagement.Application.Builders;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Factories;
using FleetManagement.Application.Services;
using FleetManagement.Application.Tests.Bridge;
using FleetManagement.Application.Tests.Decorator;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Persistence;

namespace FleetManagement.Application.Tests.Support;

/// <summary>
/// Flota de prueba con los SERVICIOS REALES de la aplicación (vehículos, rutas, mantenimiento, alertas y manifiesto
/// de carga) sobre los repositorios reales en memoria, sin mocks. La usan las pruebas de integración del reporte
/// (Decorator) y del resumen (Facade) para comprobar que ambos muestran exactamente lo que esos servicios entregan.
/// Los canales de notificación y la bitácora son de prueba (en memoria) para no escribir en consola.
/// </summary>
public sealed class RealFleet
{
    public RealFleet()
    {
        Vehicles = new VehicleService(VehicleRepository, DriverRepository, new VehicleFactoryProvider(), new FleetOnboardingFactoryProvider(), Audit, Channel);
        Routes = new DeliveryRouteService(RouteRepository, VehicleRepository, DriverRepository, new DeliveryRouteBuilder(), new DeliveryRouteDirector(), Audit);
        Maintenance = new MaintenanceService(MaintenanceRepository, VehicleRepository, DriverRepository, Audit, Channel);
        Alerts = new TripAlertService(AlertRepository, RouteRepository, Audit, Channel);
        Manifests = new CargoManifestService(RouteRepository);
    }

    /// <summary>Bitácora de prueba: permite comprobar que una operación de solo lectura no deja registros.</summary>
    public RecordingAuditLogger Audit { get; } = new();

    /// <summary>Canal de notificación de prueba: permite comprobar que una operación de solo lectura no envía avisos.</summary>
    public RecordingNotificationChannel Channel { get; } = new();

    public InMemoryVehicleRepository VehicleRepository { get; } = new();
    public InMemoryDriverRepository DriverRepository { get; } = new();
    public InMemoryDeliveryRouteRepository RouteRepository { get; } = new();
    public InMemoryMaintenanceRepository MaintenanceRepository { get; } = new();
    public InMemoryTripAlertRepository AlertRepository { get; } = new();

    public VehicleService Vehicles { get; }
    public DeliveryRouteService Routes { get; }
    public MaintenanceService Maintenance { get; }
    public TripAlertService Alerts { get; }
    public CargoManifestService Manifests { get; }

    /// <summary>
    /// Datos parecidos a los de demostración de la aplicación: 5 vehículos en distintos estados, 3 rutas (una sin carga y
    /// una retrasada) con 3 artículos de carga y un mantenimiento vencido. Devuelve los vehículos para poder usarlos.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, Vehicle>> SeedAsync()
    {
        var vehicles = new Dictionary<string, Vehicle>
        {
            ["TRK-001"] = new() { LicensePlate = "TRK-001", Brand = "Volvo", Model = "FH 460", Year = 2022, Type = VehicleType.Truck, Status = VehicleStatus.Available, CapacityKg = 8000, MileageKm = 45210 },
            ["VAN-002"] = new() { LicensePlate = "VAN-002", Brand = "Renault", Model = "Master", Year = 2023, Type = VehicleType.Van, Status = VehicleStatus.EnRoute, CapacityKg = 1500, MileageKm = 18340 },
            ["CAR-003"] = new() { LicensePlate = "CAR-003", Brand = "Chevrolet", Model = "N300", Year = 2021, Type = VehicleType.Car, Status = VehicleStatus.Maintenance, CapacityKg = 400, MileageKm = 62870 },
            ["MOT-004"] = new() { LicensePlate = "MOT-004", Brand = "AKT", Model = "NKD 125", Year = 2023, Type = VehicleType.Motorcycle, Status = VehicleStatus.Available, CapacityKg = 30, MileageKm = 9120 },
            ["VAN-005"] = new() { LicensePlate = "VAN-005", Brand = "Renault", Model = "Kangoo", Year = 2020, Type = VehicleType.Van, Status = VehicleStatus.OutOfService, CapacityKg = 1200, MileageKm = 98450 }
        };
        foreach (var vehicle in vehicles.Values)
            await VehicleRepository.AddAsync(vehicle);

        var giron = new DeliveryRoute { Name = "Ruta Girón - Bucaramanga Centro", Status = DeliveryRouteStatus.InProgress, AssignedVehicleId = vehicles["VAN-002"].Id };
        giron.CargoItems.Add(new CargoItem { Description = "Repuestos industriales", WeightKg = 320, VolumeM3 = 1.8, Priority = CargoPriority.High });
        giron.CargoItems.Add(new CargoItem { Description = "Insumos médicos", WeightKg = 45, VolumeM3 = 0.4, Priority = CargoPriority.Urgent });
        await RouteRepository.AddAsync(giron);

        var bogota = new DeliveryRoute { Name = "Ruta Bucaramanga - Bogotá", Status = DeliveryRouteStatus.Planned };
        bogota.CargoItems.Add(new CargoItem { Description = "Documentación legal", WeightKg = 5, VolumeM3 = 0.05, Priority = CargoPriority.Standard });
        await RouteRepository.AddAsync(bogota);

        await RouteRepository.AddAsync(new DeliveryRoute { Name = "Ruta sin carga", Status = DeliveryRouteStatus.Delayed, DelayMinutes = 20 });

        await MaintenanceRepository.AddAsync(new MaintenanceRecord
        {
            VehicleId = vehicles["CAR-003"].Id,
            Type = MaintenanceType.OilChange,
            PerformedAt = DateTime.UtcNow.AddMonths(-4),
            NextDueDate = DateTime.UtcNow.AddDays(-10),   // vencido a propósito
            Notes = "Cambio de aceite",
            MileageAtServiceKm = 60000
        });
        await MaintenanceRepository.AddAsync(new MaintenanceRecord
        {
            VehicleId = vehicles["TRK-001"].Id,
            Type = MaintenanceType.BrakeInspection,
            PerformedAt = DateTime.UtcNow.AddMonths(-1),
            NextDueDate = DateTime.UtcNow.AddMonths(2),   // todavía no vence
            Notes = "Inspección de frenos",
            MileageAtServiceKm = 45210
        });

        return vehicles;
    }

    public async Task<TripAlertDto> CreateAlertAsync(string routeName, string type = "Delay")
    {
        var route = (await Routes.GetAllRoutesAsync()).First(r => r.Name == routeName);
        return await Alerts.CreateAlertAsync(new CreateTripAlertRequest(route.Id, type, "Alerta de prueba", 10));
    }
}
