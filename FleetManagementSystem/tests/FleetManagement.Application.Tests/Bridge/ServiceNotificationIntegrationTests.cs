using FleetManagement.Application.DTOs;
using FleetManagement.Application.Factories;
using FleetManagement.Application.Services;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Logging;
using FleetManagement.Infrastructure.Persistence;
using NetTopologySuite.Geometries;
using Xunit;

namespace FleetManagement.Application.Tests.Bridge;

/// <summary>
/// PRUEBAS DE INTEGRACIÓN DEL PATRÓN BRIDGE con los servicios de aplicación
/// EXISTENTES (<see cref="MaintenanceService"/>, <see cref="TripAlertService"/>,
/// <see cref="VehicleService"/>), usando las implementaciones reales en
/// memoria de Infrastructure (sin mocks) para ejercitar la cadena completa,
/// igual que <c>VehicleServiceFactoryMethodTests</c> hace con Factory Method.
///
/// Cada prueba inyecta un <see cref="RecordingNotificationChannel"/> en el
/// servicio real para comprobar, sin tocar consola, que el servicio construyó
/// y envió la especialización correcta de <c>FleetNotification</c> con el
/// contenido y el destinatario esperados.
/// </summary>
public class ServiceNotificationIntegrationTests
{
    private const string OperationsDeskRecipient = "operaciones@fleetcontrol.demo";

    // ---------- MaintenanceService ----------

    private static MaintenanceService CreateMaintenanceService(
        InMemoryVehicleRepository vehicleRepository,
        InMemoryDriverRepository driverRepository,
        RecordingNotificationChannel channel)
    {
        return new MaintenanceService(
            new InMemoryMaintenanceRepository(),
            vehicleRepository,
            driverRepository,
            FleetAuditLogger.Instance,
            channel);
    }

    [Fact]
    public async Task MaintenanceService_CreateAsync_WithoutAssignedDriver_NotifiesOperationsDesk()
    {
        var vehicleRepository = new InMemoryVehicleRepository();
        var vehicle = await vehicleRepository.AddAsync(new Vehicle { LicensePlate = "TES-001", Brand = "Chevrolet", Model = "NPR", Year = 2020, Type = VehicleType.Truck });
        var channel = new RecordingNotificationChannel();
        var service = CreateMaintenanceService(vehicleRepository, new InMemoryDriverRepository(), channel);

        await service.CreateAsync(new CreateMaintenanceRecordRequest(
            vehicle.Id, "OilChange", DateTime.UtcNow, DateTime.UtcNow.AddMonths(6), 50000, "Cambio de aceite y filtros", 10000));

        var sent = Assert.Single(channel.SentMessages);
        Assert.Equal(OperationsDeskRecipient, sent.Recipient);
        Assert.Contains("TES-001", sent.Subject);
        Assert.Contains("OilChange", sent.Message);
    }

    [Fact]
    public async Task MaintenanceService_CreateAsync_WithAssignedDriver_NotifiesDriverPhone()
    {
        var vehicleRepository = new InMemoryVehicleRepository();
        var driverRepository = new InMemoryDriverRepository();

        var driver = await driverRepository.AddAsync(new Driver { FullName = "Carlos Ramírez", LicenseNumber = "SAN-88231", Phone = "+57 300 555 0101" });
        var vehicle = await vehicleRepository.AddAsync(new Vehicle { LicensePlate = "TES-002", Brand = "Chevrolet", Model = "NPR", Year = 2021, Type = VehicleType.Truck, AssignedDriverId = driver.Id });

        var channel = new RecordingNotificationChannel();
        var service = CreateMaintenanceService(vehicleRepository, driverRepository, channel);

        await service.CreateAsync(new CreateMaintenanceRecordRequest(
            vehicle.Id, "TireRotation", DateTime.UtcNow, null, 60000, "Rotación de llantas", 15000));

        var sent = Assert.Single(channel.SentMessages);
        Assert.Equal(driver.Phone, sent.Recipient);
    }

    // ---------- TripAlertService ----------

    [Fact]
    public async Task TripAlertService_CreateAlertAsync_NotifiesOperationsDesk_NotTheDriver()
    {
        var routeRepository = new InMemoryDeliveryRouteRepository();
        var route = await routeRepository.AddAsync(new DeliveryRoute
        {
            Name = "Ruta Centro",
            Origin = new Point(-73.1227, 7.1193) { SRID = 4326 },
            Destination = new Point(-73.1000, 7.1500) { SRID = 4326 },
            AssignedDriverId = Guid.NewGuid() // aunque haya conductor asignado a la ruta, el destinatario debe ser operaciones.
        });

        var channel = new RecordingNotificationChannel();
        var service = new TripAlertService(new InMemoryTripAlertRepository(), routeRepository, FleetAuditLogger.Instance, channel);

        await service.CreateAlertAsync(new CreateTripAlertRequest(route.Id, "Breakdown", "Falla mecánica en el motor", 30));

        var sent = Assert.Single(channel.SentMessages);
        Assert.Equal(OperationsDeskRecipient, sent.Recipient);
        Assert.Contains("Ruta Centro", sent.Subject);
        Assert.Contains("Breakdown", sent.Message);
        Assert.Contains("Falla mecánica en el motor", sent.Message);
        Assert.Contains("30 min", sent.Message);
    }

    // ---------- VehicleService ----------

    private static VehicleService CreateVehicleService(
        InMemoryVehicleRepository vehicleRepository,
        InMemoryDriverRepository driverRepository,
        RecordingNotificationChannel channel)
    {
        return new VehicleService(
            vehicleRepository,
            driverRepository,
            new VehicleFactoryProvider(),
            new FleetOnboardingFactoryProvider(),
            FleetAuditLogger.Instance,
            channel);
    }

    [Fact]
    public async Task VehicleService_UpdateVehicleStatusAsync_WithoutAssignedDriver_NotifiesOperationsDesk()
    {
        var vehicleRepository = new InMemoryVehicleRepository();
        var vehicle = await vehicleRepository.AddAsync(new Vehicle { LicensePlate = "TES-003", Brand = "Renault", Model = "Kangoo", Year = 2022, Type = VehicleType.Van });
        var channel = new RecordingNotificationChannel();
        var service = CreateVehicleService(vehicleRepository, new InMemoryDriverRepository(), channel);

        await service.UpdateVehicleStatusAsync(vehicle.Id, new UpdateVehicleStatusRequest("OutOfService"));

        var sent = Assert.Single(channel.SentMessages);
        Assert.Equal(OperationsDeskRecipient, sent.Recipient);
        Assert.Contains("TES-003", sent.Subject);
        Assert.Contains("Available -> OutOfService", sent.Message);
    }

    [Fact]
    public async Task VehicleService_UpdateVehicleStatusAsync_WithAssignedDriver_NotifiesDriverPhone()
    {
        var vehicleRepository = new InMemoryVehicleRepository();
        var driverRepository = new InMemoryDriverRepository();

        var driver = await driverRepository.AddAsync(new Driver { FullName = "Laura Gómez", LicenseNumber = "SAN-77410", Phone = "+57 300 555 0102" });
        var vehicle = await vehicleRepository.AddAsync(new Vehicle { LicensePlate = "TES-004", Brand = "Renault", Model = "Kangoo", Year = 2022, Type = VehicleType.Van, AssignedDriverId = driver.Id });

        var channel = new RecordingNotificationChannel();
        var service = CreateVehicleService(vehicleRepository, driverRepository, channel);

        await service.UpdateVehicleStatusAsync(vehicle.Id, new UpdateVehicleStatusRequest("Maintenance"));

        var sent = Assert.Single(channel.SentMessages);
        Assert.Equal(driver.Phone, sent.Recipient);
        Assert.Contains("Available -> Maintenance", sent.Message);
    }
}
