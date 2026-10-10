using FleetManagement.Application.DTOs;
using FleetManagement.Application.Reports;
using FleetManagement.Application.Services;
using FleetManagement.Application.Tests.Support;
using Xunit;

namespace FleetManagement.Application.Tests.ReportDecorator;

/// <summary>
/// PRUEBAS DE INTEGRACIÓN del Decorator de reportes con los servicios reales (sin mocks, repositorios en memoria):
/// el reporte debe contener exactamente lo que los servicios existentes entregan y coincidir con el manifiesto del
/// patrón Composite, sin duplicar sus reglas ni inventar datos.
/// </summary>
public class FleetReportIntegrationTests
{
    private static FleetReportService CreateReportService(RealFleet fleet)
        => new(new FleetReportGenerator(fleet.Vehicles, fleet.Routes), fleet.Maintenance);

    private static readonly string[] AllSections = { "Statistics", "MaintenanceAlerts", "CargoSummary" };

    [Fact]
    public async Task Report_WithRealServices_ContainsExactlyWhatTheVehicleAndRouteServicesReturn()
    {
        var fleet = new RealFleet();
        await fleet.SeedAsync();

        var report = await CreateReportService(fleet).GenerateFleetReportAsync(null);

        var vehicles = await fleet.Vehicles.GetAllVehiclesAsync();
        var routes = await fleet.Routes.GetAllRoutesAsync();
        Assert.Equal(vehicles, report.Vehicles);
        Assert.Equal(routes.Select(r => r.Id).ToArray(), report.Routes.Select(r => r.Id).ToArray());
        Assert.Equal(5, report.Vehicles.Count);
        Assert.Equal(3, report.Routes.Count);
    }

    [Fact]
    public async Task CargoSummary_MatchesTheCompositeManifestOfEveryRoute()
    {
        var fleet = new RealFleet();
        await fleet.SeedAsync();

        var report = await CreateReportService(fleet).GenerateFleetReportAsync(AllSections);

        Assert.Equal(2, report.CargoSummary!.Routes.Count);   // la ruta sin carga no aparece
        foreach (var entry in report.CargoSummary.Routes)
        {
            var manifest = await fleet.Manifests.GetManifestAsync(entry.RouteId);
            Assert.NotNull(manifest);
            Assert.Equal(manifest!.Manifest.ItemCount, entry.Cargo.ItemCount);
            Assert.Equal(manifest.Manifest.TotalWeightKg, entry.Cargo.TotalWeightKg);
            Assert.Equal(manifest.Manifest.TotalVolumeM3, entry.Cargo.TotalVolumeM3);
        }

        Assert.Equal(3, report.CargoSummary.Total.ItemCount);
        Assert.Equal(370.0, report.CargoSummary.Total.TotalWeightKg);
        Assert.Equal(2.25, report.CargoSummary.Total.TotalVolumeM3);
    }

    [Fact]
    public async Task MaintenanceAlerts_AreTheOnesOfThePredictiveMaintenanceModule()
    {
        var fleet = new RealFleet();
        await fleet.SeedAsync();

        var report = await CreateReportService(fleet).GenerateFleetReportAsync(new[] { "MaintenanceAlerts" });

        var due = await fleet.Maintenance.GetVehiclesDueForMaintenanceAsync();
        Assert.Equal(due, report.MaintenanceAlerts!.Items);
        Assert.Equal(1, report.MaintenanceAlerts.OverdueCount);
        var alert = Assert.Single(report.MaintenanceAlerts.Items);
        Assert.Equal("CAR-003", alert.VehiclePlate);
        Assert.Equal("OilChange", alert.TaskName);
    }

    [Fact]
    public async Task Statistics_WithRealServices_AreComputedFromTheRealData()
    {
        var fleet = new RealFleet();
        await fleet.SeedAsync();

        var report = await CreateReportService(fleet).GenerateFleetReportAsync(new[] { "Statistics" });

        var stats = report.Statistics!;
        Assert.Equal(5, stats.TotalVehicles);
        Assert.Equal(new[] { 2, 1, 1, 1 }, stats.VehiclesByStatus.Values.ToArray());   // Available, EnRoute, Maintenance, OutOfService
        Assert.Equal(40.0, stats.AvailabilityPercent);
        Assert.Equal(46798.0, stats.AverageMileageKm);   // (45210 + 18340 + 62870 + 9120 + 98450) / 5
        Assert.Equal(11130.0, stats.TotalCapacityKg);
        Assert.Equal(new[] { 1, 1, 1, 0, 0 }, stats.RoutesByStatus.Values.ToArray());   // Planned, InProgress, Delayed, Completed, Cancelled
        Assert.Equal(20, stats.TotalDelayMinutes);
    }

    [Fact]
    public async Task TheReport_ReflectsChangesMadeThroughTheExistingServices()
    {
        var fleet = new RealFleet();
        var seeded = await fleet.SeedAsync();
        var reports = CreateReportService(fleet);
        var before = await reports.GenerateFleetReportAsync(AllSections);

        await fleet.Vehicles.UpdateVehicleStatusAsync(seeded["TRK-001"].Id, new UpdateVehicleStatusRequest("OutOfService"));
        await fleet.Maintenance.CreateAsync(new CreateMaintenanceRecordRequest(
            seeded["MOT-004"].Id, "BrakeInspection", DateTime.UtcNow.AddYears(-1), DateTime.UtcNow.AddDays(-5), null, "Vencido", 9000));
        var after = await reports.GenerateFleetReportAsync(AllSections);

        Assert.Equal(before.Statistics!.VehiclesByStatus["OutOfService"] + 1, after.Statistics!.VehiclesByStatus["OutOfService"]);
        Assert.Equal(before.Statistics.VehiclesByStatus["Available"] - 1, after.Statistics.VehiclesByStatus["Available"]);
        Assert.Equal(before.MaintenanceAlerts!.OverdueCount + 1, after.MaintenanceAlerts!.OverdueCount);
        Assert.Contains(after.MaintenanceAlerts.Items, item => item.VehiclePlate == "MOT-004");
    }
}
