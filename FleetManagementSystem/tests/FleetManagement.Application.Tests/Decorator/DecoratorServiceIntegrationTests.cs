using FleetManagement.Application.DTOs;
using FleetManagement.Application.Factories;
using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Services;
using FleetManagement.Application.Tests.Bridge;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Persistence;
using NetTopologySuite.Geometries;
using Xunit;

namespace FleetManagement.Application.Tests.Decorator;

/// <summary>
/// PRUEBAS DE INTEGRACIÓN del Decorator con los servicios reales (sin mocks,
/// con repositorios en memoria): comprueban que MaintenanceService,
/// TripAlertService y VehicleService envían su notificación del Bridge
/// envuelta en decoradores (prioridad derivada del dominio, reintentos y
/// auditoría) y que su comportamiento original se conserva.
/// </summary>
public class DecoratorServiceIntegrationTests
{
    private static async Task<(MaintenanceService Service, Vehicle Vehicle)> CreateMaintenanceServiceAsync(
        INotificationChannel channel, RecordingAuditLogger audit)
    {
        var vehicleRepository = new InMemoryVehicleRepository();
        var vehicle = await vehicleRepository.AddAsync(new Vehicle { LicensePlate = "TES-010", Brand = "Chevrolet", Model = "NPR", Year = 2020, Type = VehicleType.Truck });
        var service = new MaintenanceService(new InMemoryMaintenanceRepository(), vehicleRepository, new InMemoryDriverRepository(), audit, channel);
        return (service, vehicle);
    }

    private static CreateMaintenanceRecordRequest MaintenanceRequest(Guid vehicleId)
        => new(vehicleId, "OilChange", DateTime.UtcNow, DateTime.UtcNow.AddMonths(6), 50000, "Cambio de aceite", 10000);

    [Fact]
    public async Task MaintenanceService_RetriesATransientChannelFailure_AndStillDeliversTheNotification()
    {
        var channel = new FlakyNotificationChannel(failuresBeforeSuccess: 1);
        var audit = new RecordingAuditLogger();
        var (service, vehicle) = await CreateMaintenanceServiceAsync(channel, audit);

        await service.CreateAsync(MaintenanceRequest(vehicle.Id));

        Assert.Equal(2, channel.Calls);
        var sent = Assert.Single(channel.Delivered.SentMessages);
        Assert.Contains("TES-010", sent.Subject);

        var entry = Assert.Single(audit.Entries, e => e.Category == "Notificación");
        Assert.Contains("MaintenanceNotification", entry.Message);
        Assert.Contains("prioridad Normal", entry.Message);
        Assert.Contains("2 de 2 intento(s)", entry.Message);
        Assert.Contains(audit.Entries, e => e.Category == "Mantenimiento");   // la auditoría original del servicio se conserva
    }

    [Fact]
    public async Task MaintenanceService_WhenTheChannelKeepsFailing_TheFailureIsNotHidden_AndItIsAudited()
    {
        var channel = new FlakyNotificationChannel(int.MaxValue);
        var audit = new RecordingAuditLogger();
        var (service, vehicle) = await CreateMaintenanceServiceAsync(channel, audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(MaintenanceRequest(vehicle.Id)));

        Assert.Equal(2, channel.Calls);   // prioridad Normal = 2 intentos
        var entry = Assert.Single(audit.Entries, e => e.Category == "Notificación");
        Assert.Contains("falló", entry.Message);
        Assert.Contains("2 de 2 intento(s)", entry.Message);
    }

    [Theory]
    [InlineData("Accident", "Critical")]
    [InlineData("Breakdown", "High")]
    [InlineData("Delay", "Normal")]
    [InlineData("Other", "Low")]
    public async Task TripAlertService_SendsTheNotificationWithThePriorityOfTheIncidentType(string alertType, string expectedPriority)
    {
        var routeRepository = new InMemoryDeliveryRouteRepository();
        var route = await routeRepository.AddAsync(new DeliveryRoute
        {
            Name = "Ruta Centro",
            Origin = new Point(-73.12, 7.11) { SRID = 4326 },
            Destination = new Point(-73.10, 7.15) { SRID = 4326 }
        });
        var channel = new RecordingNotificationChannel();
        var audit = new RecordingAuditLogger();
        var service = new TripAlertService(new InMemoryTripAlertRepository(), routeRepository, audit, channel);

        await service.CreateAlertAsync(new CreateTripAlertRequest(route.Id, alertType, "Descripción de prueba", 15));

        Assert.Single(channel.SentMessages);
        var entry = Assert.Single(audit.Entries, e => e.Category == "Notificación");
        Assert.Contains("TripAlertNotification", entry.Message);
        Assert.Contains($"prioridad {expectedPriority}", entry.Message);
        Assert.Contains(audit.Entries, e => e.Category == "Alerta");   // la auditoría original del servicio se conserva
    }

    [Theory]
    [InlineData("OutOfService", "High")]
    [InlineData("Maintenance", "Normal")]
    [InlineData("EnRoute", "Low")]
    public async Task VehicleService_SendsTheNotificationWithThePriorityOfTheNewStatus(string newStatus, string expectedPriority)
    {
        var vehicleRepository = new InMemoryVehicleRepository();
        var vehicle = await vehicleRepository.AddAsync(new Vehicle { LicensePlate = "TES-020", Brand = "Renault", Model = "Kangoo", Year = 2022, Type = VehicleType.Van });
        var channel = new RecordingNotificationChannel();
        var audit = new RecordingAuditLogger();
        var service = new VehicleService(
            vehicleRepository, new InMemoryDriverRepository(), new VehicleFactoryProvider(), new FleetOnboardingFactoryProvider(), audit, channel);

        await service.UpdateVehicleStatusAsync(vehicle.Id, new UpdateVehicleStatusRequest(newStatus));

        Assert.Single(channel.SentMessages);
        var entry = Assert.Single(audit.Entries, e => e.Category == "Notificación");
        Assert.Contains("VehicleNotification", entry.Message);
        Assert.Contains($"prioridad {expectedPriority}", entry.Message);
        Assert.Contains(audit.Entries, e => e.Category == "Vehículo");   // la auditoría original del servicio se conserva
    }
}
