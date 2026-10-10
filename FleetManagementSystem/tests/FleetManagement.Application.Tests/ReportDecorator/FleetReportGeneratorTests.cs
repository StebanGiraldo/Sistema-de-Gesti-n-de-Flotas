using FleetManagement.Application.Reports;
using FleetManagement.Application.Tests.Support;
using Xunit;

namespace FleetManagement.Application.Tests.ReportDecorator;

/// <summary>
/// Pruebas del COMPONENTE CONCRETO del Decorator de reportes: el generador base. Se prueba con servicios de
/// prueba para comprobar que solo reúne lo que los servicios existentes entregan, sin inventar nada.
/// </summary>
public class FleetReportGeneratorTests
{
    private static (FleetReportGenerator Generator, FakeVehicleService Vehicles, FakeDeliveryRouteService Routes) Create()
    {
        var vehicles = new FakeVehicleService();
        var routes = new FakeDeliveryRouteService();
        return (new FleetReportGenerator(vehicles, routes), vehicles, routes);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsTheMetadataTheVehiclesAndTheRoutesOfTheExistingServices()
    {
        var (generator, vehicles, routes) = Create();
        vehicles.Vehicles.Add(TestData.Vehicle("TRK-001", "Available"));
        vehicles.Vehicles.Add(TestData.Vehicle("VAN-002", "EnRoute"));
        routes.Routes.Add(TestData.Route("Ruta Centro", "InProgress"));
        var before = DateTime.UtcNow;

        var report = await generator.GenerateAsync();

        Assert.Equal("Fleet", report.Metadata.ReportType);
        Assert.Equal(new[] { "Vehicles", "Routes" }, report.Metadata.IncludedSections);
        Assert.True(report.Metadata.GeneratedAtUtc >= before && report.Metadata.GeneratedAtUtc <= DateTime.UtcNow);
        Assert.Equal(vehicles.Vehicles, report.Vehicles);
        Assert.Equal(new[] { "Ruta Centro" }, report.Routes.Select(r => r.Name).ToArray());
    }

    [Fact]
    public async Task GenerateAsync_DoesNotIncludeAnyOptionalSection()
    {
        var (generator, _, _) = Create();

        var report = await generator.GenerateAsync();

        Assert.Null(report.Statistics);
        Assert.Null(report.MaintenanceAlerts);
        Assert.Null(report.CargoSummary);
    }

    [Fact]
    public async Task GenerateAsync_QueriesEachServiceExactlyOnce()
    {
        var (generator, vehicles, routes) = Create();

        await generator.GenerateAsync();

        Assert.Equal(1, vehicles.GetAllCalls);
        Assert.Equal(1, routes.GetAllCalls);
    }

    [Fact]
    public async Task GenerateAsync_WithAnEmptyFleet_ReturnsAValidEmptyReport()
    {
        var (generator, _, _) = Create();

        var report = await generator.GenerateAsync();

        Assert.Empty(report.Vehicles);
        Assert.Empty(report.Routes);
        Assert.Equal("Fleet", report.Metadata.ReportType);
    }

    [Fact]
    public async Task GenerateAsync_WhenAServiceFails_TheErrorIsNotHiddenAndNoReportIsReturned()
    {
        var (generator, vehicles, routes) = Create();

        vehicles.Failure = new InvalidOperationException("Falla en vehículos");
        var vehicleError = await Assert.ThrowsAsync<InvalidOperationException>(() => generator.GenerateAsync());
        Assert.Equal("Falla en vehículos", vehicleError.Message);

        vehicles.Failure = null;
        routes.Failure = new InvalidOperationException("Falla en rutas");
        var routeError = await Assert.ThrowsAsync<InvalidOperationException>(() => generator.GenerateAsync());
        Assert.Equal("Falla en rutas", routeError.Message);
    }
}
