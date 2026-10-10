using FleetManagement.Application.Reports;
using FleetManagement.Application.Reports.Decorators;
using FleetManagement.Application.Tests.Support;
using Xunit;

namespace FleetManagement.Application.Tests.ReportDecorator;

/// <summary>
/// Pruebas de la COMPOSICIÓN del Decorator de reportes: varios decoradores apilados sobre el generador base real
/// (con servicios de prueba). Comprueban lo que define al patrón: se combinan, el orden es libre, son intercambiables
/// con el componente, el generador original no cambia y la cadena no repite consultas.
/// </summary>
public class ReportCompositionTests
{
    private sealed class World
    {
        public FakeVehicleService Vehicles { get; } = new();
        public FakeDeliveryRouteService Routes { get; } = new();
        public FakeMaintenanceService Maintenance { get; } = new();

        public World()
        {
            Vehicles.Vehicles.Add(TestData.Vehicle("TRK-001", "Available", 45210, 8000));
            Vehicles.Vehicles.Add(TestData.Vehicle("VAN-002", "EnRoute", 18340, 1500));
            Vehicles.Vehicles.Add(TestData.Vehicle("CAR-003", "Maintenance", 62870, 400));
            Routes.Routes.Add(TestData.Route("Ruta Girón", "InProgress", cargo: new[] { TestData.Cargo("Repuestos", 320, 1.8, "High"), TestData.Cargo("Insumos", 45, 0.4, "Urgent") }));
            Routes.Routes.Add(TestData.Route("Ruta Bogotá", "Planned", cargo: new[] { TestData.Cargo("Documentos", 5, 0.05) }));
            Maintenance.Due.Add(TestData.Due("CAR-003"));
        }

        public FleetReportGenerator Generator() => new(Vehicles, Routes);
    }

    [Fact]
    public async Task AllDecoratorsCombined_ProduceTheBaseReportPlusEverySection()
    {
        var world = new World();
        IFleetReportGenerator generator = world.Generator();
        generator = new StatisticsReportDecorator(generator);
        generator = new MaintenanceAlertsReportDecorator(generator, world.Maintenance);
        generator = new CargoSummaryReportDecorator(generator);

        var report = await generator.GenerateAsync();

        Assert.Equal(3, report.Vehicles.Count);
        Assert.Equal(2, report.Routes.Count);
        Assert.Equal(3, report.Statistics!.TotalVehicles);
        Assert.Equal(1, report.MaintenanceAlerts!.OverdueCount);
        Assert.Equal(3, report.CargoSummary!.Total.ItemCount);
        Assert.Equal(new[] { "Vehicles", "Routes", "Statistics", "MaintenanceAlerts", "CargoSummary" }, report.Metadata.IncludedSections);
    }

    [Fact]
    public async Task EachDecoratorWorksAlone_AndAddsNothingBeyondItsOwnSection()
    {
        var world = new World();

        var onlyStatistics = await new StatisticsReportDecorator(world.Generator()).GenerateAsync();
        Assert.NotNull(onlyStatistics.Statistics);
        Assert.Null(onlyStatistics.MaintenanceAlerts);
        Assert.Null(onlyStatistics.CargoSummary);

        var onlyAlerts = await new MaintenanceAlertsReportDecorator(world.Generator(), world.Maintenance).GenerateAsync();
        Assert.Null(onlyAlerts.Statistics);
        Assert.NotNull(onlyAlerts.MaintenanceAlerts);
        Assert.Null(onlyAlerts.CargoSummary);

        var onlyCargo = await new CargoSummaryReportDecorator(world.Generator()).GenerateAsync();
        Assert.Null(onlyCargo.Statistics);
        Assert.Null(onlyCargo.MaintenanceAlerts);
        Assert.NotNull(onlyCargo.CargoSummary);
    }

    [Fact]
    public async Task TheOrderOfTheDecorators_OnlyChangesTheOrderOfTheIncludedSections_NotTheData()
    {
        var world = new World();
        var statisticsFirst = new CargoSummaryReportDecorator(new StatisticsReportDecorator(world.Generator()));
        var cargoFirst = new StatisticsReportDecorator(new CargoSummaryReportDecorator(world.Generator()));

        var a = await statisticsFirst.GenerateAsync();
        var b = await cargoFirst.GenerateAsync();

        Assert.Equal(Json.Of(a.Statistics), Json.Of(b.Statistics));
        Assert.Equal(Json.Of(a.CargoSummary), Json.Of(b.CargoSummary));
        Assert.Equal(new[] { "Vehicles", "Routes", "Statistics", "CargoSummary" }, a.Metadata.IncludedSections);
        Assert.Equal(new[] { "Vehicles", "Routes", "CargoSummary", "Statistics" }, b.Metadata.IncludedSections);
    }

    [Fact]
    public void EveryDecorator_IsInterchangeableWithTheComponent()
    {
        var world = new World();
        IFleetReportGenerator component = world.Generator();

        IFleetReportGenerator[] everyKind =
        {
            component,
            new StatisticsReportDecorator(component),
            new MaintenanceAlertsReportDecorator(component, world.Maintenance),
            new CargoSummaryReportDecorator(new StatisticsReportDecorator(component))   // un decorador también puede envolver a otro
        };

        Assert.All(everyKind, generator => Assert.IsAssignableFrom<IFleetReportGenerator>(generator));
        Assert.Equal(3, everyKind.Count(generator => generator is FleetReportDecorator));
    }

    [Fact]
    public async Task TheOriginalGenerator_KeepsItsBehavior_BeforeAndAfterBeingWrapped()
    {
        var world = new World();
        var original = world.Generator();

        var before = await original.GenerateAsync();
        var decorated = new CargoSummaryReportDecorator(new StatisticsReportDecorator(original));
        await decorated.GenerateAsync();
        var after = await original.GenerateAsync();

        Assert.Equal(new[] { "Vehicles", "Routes" }, before.Metadata.IncludedSections);
        Assert.Equal(new[] { "Vehicles", "Routes" }, after.Metadata.IncludedSections);
        Assert.Null(after.Statistics);
        Assert.Null(after.CargoSummary);
        Assert.Equal(before.Vehicles, after.Vehicles);
    }

    [Fact]
    public async Task TheDecoratedReport_CarriesTheSameVehiclesAndRoutesAsTheBaseReport()
    {
        var world = new World();

        var baseReport = await world.Generator().GenerateAsync();
        var decorated = await new MaintenanceAlertsReportDecorator(new StatisticsReportDecorator(world.Generator()), world.Maintenance).GenerateAsync();

        Assert.Equal(baseReport.Vehicles, decorated.Vehicles);
        Assert.Equal(baseReport.Routes.Select(r => r.Id).ToArray(), decorated.Routes.Select(r => r.Id).ToArray());
    }

    [Fact]
    public async Task TheWholeChain_QueriesEachSubsystemExactlyOnce()
    {
        var world = new World();
        IFleetReportGenerator generator = new CargoSummaryReportDecorator(
            new MaintenanceAlertsReportDecorator(
                new StatisticsReportDecorator(world.Generator()),
                world.Maintenance));

        await generator.GenerateAsync();

        Assert.Equal(1, world.Vehicles.GetAllCalls);
        Assert.Equal(1, world.Routes.GetAllCalls);
        Assert.Equal(1, world.Maintenance.DueCalls);
    }

    [Fact]
    public async Task TheReport_ContainsOnlyWhatTheSubsystemsReturned_WithoutPlaceholdersOrInventedData()
    {
        var empty = new World();
        empty.Vehicles.Vehicles.Clear();
        empty.Routes.Routes.Clear();
        empty.Maintenance.Due.Clear();
        IFleetReportGenerator generator = new CargoSummaryReportDecorator(
            new MaintenanceAlertsReportDecorator(new StatisticsReportDecorator(empty.Generator()), empty.Maintenance));

        var report = await generator.GenerateAsync();

        Assert.Empty(report.Vehicles);
        Assert.Empty(report.Routes);
        Assert.Equal(0, report.Statistics!.TotalVehicles);
        Assert.Empty(report.MaintenanceAlerts!.Items);
        Assert.Empty(report.CargoSummary!.Routes);
        Assert.Equal(0, report.CargoSummary.Total.ItemCount);
    }

    [Fact]
    public async Task IfAnySubsystemFails_TheWholeChainFailsWithThatError()
    {
        var world = new World();
        world.Maintenance.Failure = new InvalidOperationException("Falla en mantenimiento");
        IFleetReportGenerator generator = new MaintenanceAlertsReportDecorator(new StatisticsReportDecorator(world.Generator()), world.Maintenance);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => generator.GenerateAsync());

        Assert.Equal("Falla en mantenimiento", error.Message);
    }
}
