using FleetManagement.Application.DTOs;
using FleetManagement.Application.Services;
using FleetManagement.Application.Tests.AlertHandling;
using FleetManagement.Application.Tests.Bridge;
using FleetManagement.Application.Tests.Decorator;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Persistence;
using Xunit;

namespace FleetManagement.Application.Tests.Fixes;

/// <summary>Correcciones de lógica de esta revisión: cada prueba describe el fallo que había y lo que debe pasar ahora.</summary>
public class LogicFixesTests
{
    // ---------- Una alerta de retraso no reabre una ruta cerrada ----------

    [Theory]
    [InlineData(DeliveryRouteStatus.Completed)]
    [InlineData(DeliveryRouteStatus.Cancelled)]
    public async Task ADelayAlertOnAClosedRoute_IsRecorded_ButDoesNotReopenTheRoute(DeliveryRouteStatus closed)
    {
        var kit = new AlertHandlingKit();
        var route = await kit.Routes.AddAsync(new DeliveryRoute { Name = "Ruta cerrada", Status = closed });

        await kit.ReportAsync(route, "Delay", delayMinutes: 25);

        var stored = (await kit.Routes.GetByIdAsync(route.Id))!;
        Assert.Equal(closed, stored.Status);
        Assert.Equal(0, stored.DelayMinutes);
        Assert.Single(await kit.Inner.GetByRouteAsync(route.Id));   // la alerta sí queda registrada
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.Planned)]
    [InlineData(DeliveryRouteStatus.InProgress)]
    [InlineData(DeliveryRouteStatus.Delayed)]
    public async Task ADelayAlertOnAnOpenRoute_StillAddsTheMinutesAndMarksItDelayed(DeliveryRouteStatus open)
    {
        var kit = new AlertHandlingKit();
        var route = await kit.Routes.AddAsync(new DeliveryRoute { Name = "Ruta abierta", Status = open });

        await kit.ReportAsync(route, "Delay", delayMinutes: 25);

        var stored = (await kit.Routes.GetByIdAsync(route.Id))!;
        Assert.Equal(DeliveryRouteStatus.Delayed, stored.Status);
        Assert.Equal(25, stored.DelayMinutes);
    }

    // ---------- Un mantenimiento antiguo no hace retroceder la última fecha ----------

    [Fact]
    public async Task RegisteringAnOlderMaintenance_DoesNotMoveBackTheLastMaintenanceDate()
    {
        var vehicles = new CopyingVehicleRepository();
        var service = new MaintenanceService(new InMemoryMaintenanceRepository(), vehicles, new InMemoryDriverRepository(), new RecordingAuditLogger(), new RecordingNotificationChannel());
        var recent = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var old = new DateTime(2025, 1, 10, 8, 0, 0, DateTimeKind.Utc);
        var vehicle = await vehicles.AddAsync(new Vehicle { LicensePlate = "FIX-001", Type = VehicleType.Van, Status = VehicleStatus.Available });

        await service.CreateAsync(new CreateMaintenanceRecordRequest(vehicle.Id, "OilChange", recent, null, 60000, "Reciente", 50000));
        await service.CreateAsync(new CreateMaintenanceRecordRequest(vehicle.Id, "TireRotation", old, null, 60000, "Carga tardía de historial", 40000));

        Assert.Equal(recent, (await vehicles.GetByIdAsync(vehicle.Id))!.LastMaintenanceDate);
    }
}
