using FleetManagement.Application.DTOs;
using FleetManagement.Application.Facades;
using FleetManagement.Application.Reports;
using FleetManagement.Application.Services;
using FleetManagement.Application.Tests.Support;
using Xunit;

namespace FleetManagement.Application.Tests.Facade;

/// <summary>
/// PRUEBAS DE INTEGRACIÓN del Facade del resumen con los servicios reales (sin mocks, repositorios en memoria):
/// el resumen debe mostrar exactamente lo que esos servicios entregan, coincidir con el manifiesto Composite y con
/// el reporte (Decorator), reflejar los cambios hechos a través de los servicios existentes (incluida la regla de
/// retraso de las alertas) y no modificar nada.
/// </summary>
public class FleetDashboardFacadeIntegrationTests
{
    private const string GironRoute = "Ruta Girón - Bucaramanga Centro";
    private const string BogotaRoute = "Ruta Bucaramanga - Bogotá";

    private static FleetDashboardFacade CreateFacade(RealFleet fleet)
        => new(fleet.Vehicles, fleet.Routes, fleet.Maintenance, fleet.Alerts);

    private static FleetReportService CreateReportService(RealFleet fleet)
        => new(new FleetReportGenerator(fleet.Vehicles, fleet.Routes), fleet.Maintenance);

    [Fact]
    public async Task Summary_WithRealServices_ShowsTheFiguresOfTheSeedData()
    {
        var fleet = new RealFleet();
        await fleet.SeedAsync();

        var summary = await CreateFacade(fleet).GetSummaryAsync();

        var stats = summary.Statistics;
        Assert.Equal(5, stats.TotalVehicles);
        Assert.Equal(new[] { 2, 1, 1, 1 }, stats.VehiclesByStatus.Values.ToArray());   // Available, EnRoute, Maintenance, OutOfService
        Assert.Equal(40.0, stats.AvailabilityPercent);
        Assert.Equal(46798.0, stats.AverageMileageKm);   // (45210 + 18340 + 62870 + 9120 + 98450) / 5
        Assert.Equal(11130.0, stats.TotalCapacityKg);
        Assert.Equal(3, stats.TotalRoutes);
        Assert.Equal(new[] { 1, 1, 1, 0, 0 }, stats.RoutesByStatus.Values.ToArray());   // Planned, InProgress, Delayed, Completed, Cancelled
        Assert.Equal(20, stats.TotalDelayMinutes);
        Assert.Equal(new CargoTotalsDto(3, 370.0, 2.25), summary.Cargo);
        Assert.Equal(1, summary.OverdueMaintenanceCount);   // el cambio de aceite de CAR-003; el de frenos de TRK-001 aún no vence
        Assert.Equal(new[] { 0, 0, 0 }, summary.AlertsByStatus.Values.ToArray());
    }

    [Fact]
    public async Task Cargo_MatchesTheSumOfTheCompositeManifestsOfEveryRoute()
    {
        var fleet = new RealFleet();
        await fleet.SeedAsync();

        var summary = await CreateFacade(fleet).GetSummaryAsync();

        var itemCount = 0;
        var weightKg = 0.0;
        var volumeM3 = 0.0;
        foreach (var route in await fleet.Routes.GetAllRoutesAsync())
        {
            var manifest = await fleet.Manifests.GetManifestAsync(route.Id);
            Assert.NotNull(manifest);
            itemCount += manifest!.Manifest.ItemCount;
            weightKg += manifest.Manifest.TotalWeightKg;
            volumeM3 += manifest.Manifest.TotalVolumeM3;
        }

        Assert.Equal(itemCount, summary.Cargo.ItemCount);
        Assert.Equal(Math.Round(weightKg, 3), summary.Cargo.TotalWeightKg);
        Assert.Equal(Math.Round(volumeM3, 3), summary.Cargo.TotalVolumeM3);
    }

    [Fact]
    public async Task Summary_AgreesWithTheFullReport_OverTheSameRealData()
    {
        var fleet = new RealFleet();
        await fleet.SeedAsync();
        await fleet.CreateAlertAsync(BogotaRoute);

        var summary = await CreateFacade(fleet).GetSummaryAsync();
        var report = await CreateReportService(fleet).GenerateFleetReportAsync(new[] { "Statistics", "MaintenanceAlerts", "CargoSummary" });

        Assert.Equal(Json.Of(report.Statistics), Json.Of(summary.Statistics));
        Assert.Equal(report.CargoSummary!.Total, summary.Cargo);
        Assert.Equal(report.MaintenanceAlerts!.OverdueCount, summary.OverdueMaintenanceCount);
    }

    [Fact]
    public async Task Summary_ReflectsTheAlertsAndTheDelayRuleOfTheExistingAlertService()
    {
        var fleet = new RealFleet();
        await fleet.SeedAsync();
        var facade = CreateFacade(fleet);

        // Cada alerta con retraso suma 10 minutos a su ruta y la pasa a "Delayed": es regla de TripAlertService,
        // el resumen solo debe mostrar su efecto, no volver a calcularlo.
        var bogotaAlert = await fleet.CreateAlertAsync(BogotaRoute, "Delay");
        await fleet.CreateAlertAsync(GironRoute, "Breakdown");
        var open = await facade.GetSummaryAsync();

        Assert.Equal(new[] { 2, 0, 0 }, open.AlertsByStatus.Values.ToArray());
        Assert.Equal(new[] { 0, 0, 3, 0, 0 }, open.Statistics.RoutesByStatus.Values.ToArray());   // las 3 rutas quedaron retrasadas
        Assert.Equal(40, open.Statistics.TotalDelayMinutes);   // 20 que ya tenía + 10 + 10

        await fleet.Alerts.ResolveAlertAsync(bogotaAlert.Id);
        var resolved = await facade.GetSummaryAsync();

        Assert.Equal(new[] { 1, 0, 1 }, resolved.AlertsByStatus.Values.ToArray());
    }

    [Fact]
    public async Task Summary_ReflectsVehicleStatusChangesMadeThroughTheExistingService()
    {
        var fleet = new RealFleet();
        var seeded = await fleet.SeedAsync();
        var facade = CreateFacade(fleet);

        await fleet.Vehicles.UpdateVehicleStatusAsync(seeded["TRK-001"].Id, new UpdateVehicleStatusRequest("OutOfService"));
        var summary = await facade.GetSummaryAsync();

        Assert.Equal(new[] { 1, 1, 1, 2 }, summary.Statistics.VehiclesByStatus.Values.ToArray());
        Assert.Equal(20.0, summary.Statistics.AvailabilityPercent);
    }

    [Fact]
    public async Task Summary_ReflectsMaintenanceRecordsCreatedThroughTheExistingService()
    {
        var fleet = new RealFleet();
        var seeded = await fleet.SeedAsync();
        var facade = CreateFacade(fleet);

        await fleet.Maintenance.CreateAsync(new CreateMaintenanceRecordRequest(
            seeded["MOT-004"].Id, "BrakeInspection", DateTime.UtcNow.AddYears(-1), DateTime.UtcNow.AddDays(-5), null, "Vencido", 9000));
        var summary = await facade.GetSummaryAsync();

        Assert.Equal(2, summary.OverdueMaintenanceCount);
    }

    [Fact]
    public async Task Summary_IsReadOnly_ItChangesNothingNorWritesTheAuditLogNorSendsNotifications()
    {
        var fleet = new RealFleet();
        await fleet.SeedAsync();
        await fleet.CreateAlertAsync(BogotaRoute);   // para que también haya una alerta en la comparación
        var auditEntriesBefore = fleet.Audit.Entries.Count;
        var notificationsBefore = fleet.Channel.SentMessages.Count;
        var before = await SnapshotAsync(fleet);

        var facade = CreateFacade(fleet);
        await facade.GetSummaryAsync();
        await facade.GetSummaryAsync();

        Assert.Equal(before, await SnapshotAsync(fleet));
        Assert.Equal(auditEntriesBefore, fleet.Audit.Entries.Count);
        Assert.Equal(notificationsBefore, fleet.Channel.SentMessages.Count);
    }

    [Fact]
    public async Task Summary_OfAnEmptyFleet_WithRealServices_IsAllZeros()
    {
        var fleet = new RealFleet();

        var summary = await CreateFacade(fleet).GetSummaryAsync();

        Assert.Equal(0, summary.Statistics.TotalVehicles);
        Assert.Equal(0.0, summary.Statistics.AvailabilityPercent);
        Assert.Equal(0, summary.Statistics.TotalRoutes);
        Assert.Equal(new CargoTotalsDto(0, 0.0, 0.0), summary.Cargo);
        Assert.Equal(0, summary.OverdueMaintenanceCount);
        Assert.All(summary.AlertsByStatus.Values, count => Assert.Equal(0, count));
    }

    /// <summary>Todo lo que los servicios entregan hoy, serializado, para comparar "antes" y "después".</summary>
    private static async Task<string> SnapshotAsync(RealFleet fleet) => Json.Of(new object[]
    {
        await fleet.Vehicles.GetAllVehiclesAsync(),
        await fleet.Routes.GetAllRoutesAsync(),
        await fleet.Alerts.GetAllAsync(),
        await fleet.Maintenance.GetAllAsync(),
        await fleet.Maintenance.GetVehiclesDueForMaintenanceAsync()
    });
}
