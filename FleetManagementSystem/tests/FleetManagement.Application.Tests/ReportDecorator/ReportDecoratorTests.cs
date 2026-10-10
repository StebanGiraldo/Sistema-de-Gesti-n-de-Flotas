using FleetManagement.Application.DTOs;
using FleetManagement.Application.Reports;
using FleetManagement.Application.Reports.Decorators;
using FleetManagement.Application.Tests.Support;
using Xunit;

namespace FleetManagement.Application.Tests.ReportDecorator;

/// <summary>
/// Pruebas de CADA decorador concreto del reporte, aislado: se envuelve un "componente" mínimo
/// (<see cref="StubReportGenerator"/>) para comprobar que el decorador añade SU sección, no toca nada más y no
/// repite consultas.
/// </summary>
public class ReportDecoratorTests
{
    // ------------------------------------------------------------------ Statistics

    [Fact]
    public async Task StatisticsDecorator_AddsTheStatisticsComputedFromTheDataOfTheInnerReport()
    {
        var inner = new StubReportGenerator(TestData.BaseReport(
            vehicles: new[] { TestData.Vehicle("A-1", "Available", 100, 500), TestData.Vehicle("E-1", "EnRoute", 300, 1500) },
            routes: new[] { TestData.Route("R1", "Delayed", delayMinutes: 15) }));

        var report = await new StatisticsReportDecorator(inner).GenerateAsync();

        var stats = Assert.IsType<FleetStatisticsDto>(report.Statistics);
        Assert.Equal(2, stats.TotalVehicles);
        Assert.Equal(1, stats.VehiclesByStatus["Available"]);
        Assert.Equal(1, stats.VehiclesByStatus["EnRoute"]);
        Assert.Equal(50.0, stats.AvailabilityPercent);
        Assert.Equal(200.0, stats.AverageMileageKm);
        Assert.Equal(2000.0, stats.TotalCapacityKg);
        Assert.Equal(1, stats.RoutesByStatus["Delayed"]);
        Assert.Equal(15, stats.TotalDelayMinutes);
    }

    // ------------------------------------------------------------------ MaintenanceAlerts

    [Fact]
    public async Task MaintenanceAlertsDecorator_AddsTheOverdueItemsReturnedByTheMaintenanceService()
    {
        var maintenance = new FakeMaintenanceService();
        maintenance.Due.Add(TestData.Due("CAR-003", "OilChange"));
        maintenance.Due.Add(TestData.Due("TRK-001", "BrakeInspection"));
        var decorator = new MaintenanceAlertsReportDecorator(new StubReportGenerator(TestData.BaseReport()), maintenance);

        var report = await decorator.GenerateAsync();

        var section = Assert.IsType<MaintenanceAlertsSectionDto>(report.MaintenanceAlerts);
        Assert.Equal(2, section.OverdueCount);
        Assert.Equal(maintenance.Due, section.Items);   // exactamente lo que entregó el servicio, sin regla propia
        Assert.Equal(1, maintenance.DueCalls);
    }

    [Fact]
    public async Task MaintenanceAlertsDecorator_CountsAsOverdueOnlyTheItemsMarkedAsOverdue()
    {
        var maintenance = new FakeMaintenanceService();
        maintenance.Due.Add(TestData.Due("CAR-003"));
        maintenance.Due.Add(TestData.Due("TRK-001", isOverdue: false));
        var decorator = new MaintenanceAlertsReportDecorator(new StubReportGenerator(TestData.BaseReport()), maintenance);

        var report = await decorator.GenerateAsync();

        Assert.Equal(1, report.MaintenanceAlerts!.OverdueCount);
        Assert.Equal(2, report.MaintenanceAlerts.Items.Count);
    }

    [Fact]
    public async Task MaintenanceAlertsDecorator_WithNothingDue_AddsAnEmptySectionInsteadOfInventingAlerts()
    {
        var decorator = new MaintenanceAlertsReportDecorator(new StubReportGenerator(TestData.BaseReport()), new FakeMaintenanceService());

        var report = await decorator.GenerateAsync();

        Assert.NotNull(report.MaintenanceAlerts);
        Assert.Equal(0, report.MaintenanceAlerts!.OverdueCount);
        Assert.Empty(report.MaintenanceAlerts.Items);
    }

    [Fact]
    public async Task MaintenanceAlertsDecorator_WhenTheMaintenanceServiceFails_TheErrorIsNotHidden()
    {
        var maintenance = new FakeMaintenanceService { Failure = new InvalidOperationException("Falla en mantenimiento") };
        var decorator = new MaintenanceAlertsReportDecorator(new StubReportGenerator(TestData.BaseReport()), maintenance);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => decorator.GenerateAsync());

        Assert.Equal("Falla en mantenimiento", error.Message);
    }

    // ------------------------------------------------------------------ CargoSummary

    [Fact]
    public async Task CargoSummaryDecorator_AddsTheTotalsPerRouteAndTheGeneralTotal()
    {
        var inner = new StubReportGenerator(TestData.BaseReport(routes: new[]
        {
            TestData.Route("Ruta Girón", "InProgress", cargo: new[]
            {
                TestData.Cargo("Repuestos industriales", 320, 1.8, "High"),
                TestData.Cargo("Insumos médicos", 45, 0.4, "Urgent")
            }),
            TestData.Route("Ruta Bogotá", "Planned", cargo: new[] { TestData.Cargo("Documentación legal", 5, 0.05) })
        }));

        var report = await new CargoSummaryReportDecorator(inner).GenerateAsync();

        var summary = Assert.IsType<CargoSummaryDto>(report.CargoSummary);
        Assert.Equal(new CargoTotalsDto(3, 370.0, 2.25), summary.Total);
        Assert.Equal(new[] { "Ruta Girón", "Ruta Bogotá" }, summary.Routes.Select(r => r.RouteName).ToArray());
        Assert.Equal(new CargoTotalsDto(2, 365.0, 2.2), summary.Routes[0].Cargo);
        Assert.Equal(new CargoTotalsDto(1, 5.0, 0.05), summary.Routes[1].Cargo);
    }

    [Fact]
    public async Task CargoSummaryDecorator_ListsOnlyTheRoutesThatCarryCargo()
    {
        var withCargo = TestData.Route("Con carga", cargo: new[] { TestData.Cargo("Cajas", 10, 0.5) });
        var inner = new StubReportGenerator(TestData.BaseReport(routes: new[] { TestData.Route("Sin carga"), withCargo }));

        var report = await new CargoSummaryReportDecorator(inner).GenerateAsync();

        var entry = Assert.Single(report.CargoSummary!.Routes);
        Assert.Equal(withCargo.Id, entry.RouteId);
        Assert.Equal("Con carga", entry.RouteName);
    }

    [Fact]
    public async Task CargoSummaryDecorator_KeepsTheRouteStatus_SoTheReaderCanFilterIt()
    {
        var inner = new StubReportGenerator(TestData.BaseReport(routes: new[]
        {
            TestData.Route("Cancelada", "Cancelled", cargo: new[] { TestData.Cargo("Cajas", 10, 0.5) })
        }));

        var report = await new CargoSummaryReportDecorator(inner).GenerateAsync();

        Assert.Equal("Cancelled", report.CargoSummary!.Routes.Single().RouteStatus);
    }

    [Fact]
    public async Task CargoSummaryDecorator_WithNoCargoAtAll_ReturnsZeroTotalsAndNoRoutes()
    {
        var inner = new StubReportGenerator(TestData.BaseReport(routes: new[] { TestData.Route("Vacía") }));

        var report = await new CargoSummaryReportDecorator(inner).GenerateAsync();

        Assert.Equal(new CargoTotalsDto(0, 0.0, 0.0), report.CargoSummary!.Total);
        Assert.Empty(report.CargoSummary.Routes);
    }

    [Fact]
    public async Task CargoSummaryDecorator_GeneralTotalIsComputedFromTheItems_NotFromTheRoundedRouteTotals()
    {
        // Tres rutas con 0.0004 kg: cada total por ruta se redondea a 0, pero el total general real es 0.0012 -> 0.001.
        var routes = Enumerable.Range(1, 3)
            .Select(i => TestData.Route($"Ruta {i}", cargo: new[] { TestData.Cargo("Muestra", 0.0004, 0.0004) }))
            .ToArray();
        var inner = new StubReportGenerator(TestData.BaseReport(routes: routes));

        var report = await new CargoSummaryReportDecorator(inner).GenerateAsync();

        Assert.All(report.CargoSummary!.Routes, r => Assert.Equal(0.0, r.Cargo.TotalWeightKg));
        Assert.Equal(0.001, report.CargoSummary.Total.TotalWeightKg);
    }

    // ------------------------------------------------------------------ Comportamiento común del decorador base

    [Fact]
    public async Task EveryDecorator_RegistersItsSectionInTheMetadata_AfterTheOnesOfTheInnerReport()
    {
        var maintenance = new FakeMaintenanceService();
        var inner = new StubReportGenerator(TestData.BaseReport());

        var statistics = await new StatisticsReportDecorator(inner).GenerateAsync();
        var alerts = await new MaintenanceAlertsReportDecorator(inner, maintenance).GenerateAsync();
        var cargo = await new CargoSummaryReportDecorator(inner).GenerateAsync();

        Assert.Equal(new[] { "Vehicles", "Routes", "Statistics" }, statistics.Metadata.IncludedSections);
        Assert.Equal(new[] { "Vehicles", "Routes", "MaintenanceAlerts" }, alerts.Metadata.IncludedSections);
        Assert.Equal(new[] { "Vehicles", "Routes", "CargoSummary" }, cargo.Metadata.IncludedSections);
    }

    [Fact]
    public async Task EveryDecorator_ReturnsTheRestOfTheInnerReportUntouched()
    {
        var baseReport = TestData.BaseReport(
            vehicles: new[] { TestData.Vehicle("A-1") },
            routes: new[] { TestData.Route("R1", cargo: new[] { TestData.Cargo("Cajas", 1, 1) }) });
        var inner = new StubReportGenerator(baseReport);

        var reports = new[]
        {
            await new StatisticsReportDecorator(inner).GenerateAsync(),
            await new MaintenanceAlertsReportDecorator(inner, new FakeMaintenanceService()).GenerateAsync(),
            await new CargoSummaryReportDecorator(inner).GenerateAsync()
        };

        Assert.All(reports, report =>
        {
            Assert.Same(baseReport.Vehicles, report.Vehicles);
            Assert.Same(baseReport.Routes, report.Routes);
            Assert.Equal(baseReport.Metadata.ReportType, report.Metadata.ReportType);
            Assert.Equal(baseReport.Metadata.GeneratedAtUtc, report.Metadata.GeneratedAtUtc);
        });
    }

    [Fact]
    public async Task EveryDecorator_AsksTheInnerGeneratorExactlyOnce()
    {
        var inner = new StubReportGenerator(TestData.BaseReport());

        await new StatisticsReportDecorator(inner).GenerateAsync();
        Assert.Equal(1, inner.GenerateCalls);

        await new CargoSummaryReportDecorator(inner).GenerateAsync();
        Assert.Equal(2, inner.GenerateCalls);
    }

    [Fact]
    public void EveryDecorator_RejectsMissingDependencies()
    {
        var inner = new StubReportGenerator(TestData.BaseReport());

        Assert.Throws<ArgumentNullException>(() => new StatisticsReportDecorator(null!));
        Assert.Throws<ArgumentNullException>(() => new CargoSummaryReportDecorator(null!));
        Assert.Throws<ArgumentNullException>(() => new MaintenanceAlertsReportDecorator(null!, new FakeMaintenanceService()));
        Assert.Throws<ArgumentNullException>(() => new MaintenanceAlertsReportDecorator(inner, null!));
    }
}
