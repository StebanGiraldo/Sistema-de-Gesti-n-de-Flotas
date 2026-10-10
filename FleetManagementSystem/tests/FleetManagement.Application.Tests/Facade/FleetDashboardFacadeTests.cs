using FleetManagement.Application.DTOs;
using FleetManagement.Application.Facades;
using FleetManagement.Application.Reports;
using FleetManagement.Application.Reports.Decorators;
using FleetManagement.Application.Tests.Support;
using Xunit;

namespace FleetManagement.Application.Tests.Facade;

/// <summary>
/// Pruebas del FACADE del resumen del dashboard (<c>GET api/dashboard/summary</c>) con los servicios de vehículos,
/// rutas, mantenimiento y alertas sustituidos por dobles de prueba. Los dobles lanzan NotSupportedException ante
/// cualquier operación distinta de las cuatro consultas que el Facade necesita, de modo que estas pruebas también
/// garantizan que no hace nada más (por ejemplo, no consulta el manifiesto Composite de cada ruta).
/// </summary>
public class FleetDashboardFacadeTests
{
    private sealed class Subsystems
    {
        public FakeVehicleService Vehicles { get; } = new();
        public FakeDeliveryRouteService Routes { get; } = new();
        public FakeMaintenanceService Maintenance { get; } = new();
        public FakeTripAlertService Alerts { get; } = new();

        public IFleetDashboardFacade Facade() => new FleetDashboardFacade(Vehicles, Routes, Maintenance, Alerts);

        /// <summary>3 vehículos, 3 rutas (una sin carga y con retraso), 1 mantenimiento vencido y 4 alertas.</summary>
        public static Subsystems WithData()
        {
            var world = new Subsystems();
            world.Vehicles.Vehicles.Add(TestData.Vehicle("TRK-001", "Available", 45210, 8000));
            world.Vehicles.Vehicles.Add(TestData.Vehicle("VAN-002", "EnRoute", 18340, 1500));
            world.Vehicles.Vehicles.Add(TestData.Vehicle("CAR-003", "Maintenance", 62870, 400));
            world.Routes.Routes.Add(TestData.Route("Ruta Girón", "InProgress", cargo: new[] { TestData.Cargo("Repuestos", 320, 1.8, "High"), TestData.Cargo("Insumos", 45, 0.4, "Urgent") }));
            world.Routes.Routes.Add(TestData.Route("Ruta Bogotá", "Planned", cargo: new[] { TestData.Cargo("Documentos", 5, 0.05) }));
            world.Routes.Routes.Add(TestData.Route("Ruta sin carga", "Delayed", delayMinutes: 20));
            world.Maintenance.Due.Add(TestData.Due("CAR-003"));
            world.Alerts.Alerts.Add(TestData.Alert("Open"));
            world.Alerts.Alerts.Add(TestData.Alert("Open"));
            world.Alerts.Alerts.Add(TestData.Alert("Acknowledged"));
            world.Alerts.Alerts.Add(TestData.Alert("Resolved"));
            return world;
        }
    }

    [Fact]
    public async Task GetSummary_BuildsTheWholeSummaryFromTheFourSubsystems()
    {
        var world = Subsystems.WithData();
        IFleetDashboardFacade facade = world.Facade();

        var summary = await facade.GetSummaryAsync();

        var stats = summary.Statistics;
        Assert.Equal(3, stats.TotalVehicles);
        Assert.Equal(new[] { 1, 1, 1, 0 }, stats.VehiclesByStatus.Values.ToArray());   // Available, EnRoute, Maintenance, OutOfService
        Assert.Equal(33.3, stats.AvailabilityPercent);
        Assert.Equal(42140.0, stats.AverageMileageKm);   // (45210 + 18340 + 62870) / 3
        Assert.Equal(9900.0, stats.TotalCapacityKg);
        Assert.Equal(3, stats.TotalRoutes);
        Assert.Equal(new[] { 1, 1, 1, 0, 0 }, stats.RoutesByStatus.Values.ToArray());   // Planned, InProgress, Delayed, Completed, Cancelled
        Assert.Equal(20, stats.TotalDelayMinutes);
        Assert.Equal(new CargoTotalsDto(3, 370.0, 2.25), summary.Cargo);
        Assert.Equal(1, summary.OverdueMaintenanceCount);
        Assert.Equal(new[] { 2, 1, 1 }, summary.AlertsByStatus.Values.ToArray());   // Open, Acknowledged, Resolved
    }

    [Fact]
    public async Task GetSummary_QueriesEachSubsystemExactlyOnce()
    {
        var world = Subsystems.WithData();

        await world.Facade().GetSummaryAsync();

        Assert.Equal(1, world.Vehicles.GetAllCalls);
        Assert.Equal(1, world.Routes.GetAllCalls);
        Assert.Equal(1, world.Maintenance.DueCalls);
        Assert.Equal(1, world.Alerts.GetAllCalls);
    }

    [Fact]
    public async Task GetSummary_GivesTheSameFiguresAsTheReport_BecauseBothShareTheSameCalculations()
    {
        var world = Subsystems.WithData();
        IFleetReportGenerator report = new CargoSummaryReportDecorator(
            new MaintenanceAlertsReportDecorator(
                new StatisticsReportDecorator(new FleetReportGenerator(world.Vehicles, world.Routes)),
                world.Maintenance));

        var summary = await world.Facade().GetSummaryAsync();
        var fullReport = await report.GenerateAsync();

        Assert.Equal(Json.Of(fullReport.Statistics), Json.Of(summary.Statistics));
        Assert.Equal(fullReport.CargoSummary!.Total, summary.Cargo);
        Assert.Equal(fullReport.MaintenanceAlerts!.OverdueCount, summary.OverdueMaintenanceCount);
    }

    [Fact]
    public async Task GetSummary_OfAnEmptyFleet_IsAllZeros_WithoutErrorsNorInventedData()
    {
        var world = new Subsystems();

        var summary = await world.Facade().GetSummaryAsync();

        Assert.Equal(0, summary.Statistics.TotalVehicles);
        Assert.Equal(0.0, summary.Statistics.AvailabilityPercent);
        Assert.Equal(0.0, summary.Statistics.AverageMileageKm);
        Assert.Equal(0.0, summary.Statistics.TotalCapacityKg);
        Assert.Equal(0, summary.Statistics.TotalRoutes);
        Assert.Equal(0, summary.Statistics.TotalDelayMinutes);
        Assert.All(summary.Statistics.VehiclesByStatus.Values, count => Assert.Equal(0, count));
        Assert.All(summary.Statistics.RoutesByStatus.Values, count => Assert.Equal(0, count));
        Assert.Equal(new CargoTotalsDto(0, 0.0, 0.0), summary.Cargo);
        Assert.Equal(0, summary.OverdueMaintenanceCount);
        Assert.Equal(new[] { "Open", "Acknowledged", "Resolved" }, summary.AlertsByStatus.Keys.ToArray());
        Assert.All(summary.AlertsByStatus.Values, count => Assert.Equal(0, count));
    }

    [Fact]
    public async Task IfAnySubsystemFails_TheSameExceptionReachesTheCaller_WithoutBeingHiddenBehindASummary()
    {
        var failures = new (string Subsystem, Action<Subsystems, Exception> Break)[]
        {
            ("vehículos", (world, error) => world.Vehicles.Failure = error),
            ("rutas", (world, error) => world.Routes.Failure = error),
            ("mantenimiento", (world, error) => world.Maintenance.Failure = error),
            ("alertas", (world, error) => world.Alerts.Failure = error)
        };

        foreach (var (subsystem, breakIt) in failures)
        {
            var world = Subsystems.WithData();
            var failure = new InvalidOperationException($"Falla en {subsystem}");
            breakIt(world, failure);

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => world.Facade().GetSummaryAsync());

            Assert.Same(failure, thrown);
        }
    }

    [Fact]
    public async Task AlertsByStatus_ListsEveryStatus_CountingEachAlertInItsOwnStatus()
    {
        var world = new Subsystems();
        foreach (var status in new[] { "Open", "Open", "Open", "Resolved" })
            world.Alerts.Alerts.Add(TestData.Alert(status));

        var summary = await world.Facade().GetSummaryAsync();

        Assert.Equal(new[] { "Open", "Acknowledged", "Resolved" }, summary.AlertsByStatus.Keys.ToArray());
        Assert.Equal(new[] { 3, 0, 1 }, summary.AlertsByStatus.Values.ToArray());
    }

    [Fact]
    public async Task OverdueMaintenanceCount_CountsOnlyTheItemsMarkedAsOverdue()
    {
        var world = new Subsystems();
        world.Maintenance.Due.Add(TestData.Due("CAR-003"));
        world.Maintenance.Due.Add(TestData.Due("VAN-002", "BrakeInspection"));
        world.Maintenance.Due.Add(TestData.Due("TRK-001", "OilChange", isOverdue: false));

        var summary = await world.Facade().GetSummaryAsync();

        Assert.Equal(2, summary.OverdueMaintenanceCount);
    }

    [Fact]
    public async Task Cargo_AddsUpEveryRoute_IncludingTheOnesWithoutCargo()
    {
        var world = new Subsystems();
        world.Routes.Routes.Add(TestData.Route("Con carga A", cargo: new[] { TestData.Cargo("Cajas", 10.5, 0.25) }));
        world.Routes.Routes.Add(TestData.Route("Sin carga"));
        world.Routes.Routes.Add(TestData.Route("Con carga B", cargo: new[] { TestData.Cargo("Sobres", 0.25, 0.001), TestData.Cargo("Pallets", 100, 1.5) }));

        var summary = await world.Facade().GetSummaryAsync();

        Assert.Equal(new CargoTotalsDto(3, 110.75, 1.751), summary.Cargo);
    }

    [Fact]
    public async Task TheSummary_IsStampedWithTheCurrentUtcTime()
    {
        var world = Subsystems.WithData();

        var before = DateTime.UtcNow;
        var summary = await world.Facade().GetSummaryAsync();
        var after = DateTime.UtcNow;

        Assert.True(summary.GeneratedAtUtc >= before && summary.GeneratedAtUtc <= after);
        Assert.Equal(DateTimeKind.Utc, summary.GeneratedAtUtc.Kind);
    }

    [Fact]
    public async Task TheFacade_KeepsNoState_EveryCallReadsTheCurrentDataOfTheSubsystems()
    {
        var world = Subsystems.WithData();
        var facade = world.Facade();

        var first = await facade.GetSummaryAsync();
        var again = await facade.GetSummaryAsync();
        world.Vehicles.Vehicles.Add(TestData.Vehicle("MOT-004", "Available", 9120, 30));
        var afterChange = await facade.GetSummaryAsync();

        Assert.Equal(Json.Of(first.Statistics), Json.Of(again.Statistics));
        Assert.Equal(first.Cargo, again.Cargo);
        Assert.Equal(Json.Of(first.AlertsByStatus), Json.Of(again.AlertsByStatus));
        Assert.Equal(first.Statistics.TotalVehicles + 1, afterChange.Statistics.TotalVehicles);
        Assert.Equal(3, world.Vehicles.GetAllCalls);   // sin caché: cada llamada vuelve a consultar
    }
}
