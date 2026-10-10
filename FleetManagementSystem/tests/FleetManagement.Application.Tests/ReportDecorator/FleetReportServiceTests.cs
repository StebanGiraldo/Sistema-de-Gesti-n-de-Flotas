using FleetManagement.Application.DTOs;
using FleetManagement.Application.Reports;
using FleetManagement.Application.Services;
using FleetManagement.Application.Tests.Support;
using Xunit;

namespace FleetManagement.Application.Tests.ReportDecorator;

/// <summary>
/// Pruebas del caso de uso que arma la cadena de decoradores a partir de las secciones que pide el cliente
/// (<c>GET api/reports/fleet?sections=...</c>): interpretación de la petición, orden y errores de validación.
/// </summary>
public class FleetReportServiceTests
{
    private sealed class Counting : IFleetReportGenerator
    {
        private readonly IFleetReportGenerator _inner;
        public Counting(IFleetReportGenerator inner) { _inner = inner; }
        public int GenerateCalls { get; private set; }
        public Task<FleetReportDto> GenerateAsync() { GenerateCalls++; return _inner.GenerateAsync(); }
    }

    private static (FleetReportService Service, Counting BaseGenerator) Create()
    {
        var vehicles = new FakeVehicleService();
        vehicles.Vehicles.Add(TestData.Vehicle("TRK-001", "Available", 100, 8000));
        var routes = new FakeDeliveryRouteService();
        routes.Routes.Add(TestData.Route("Ruta Girón", "InProgress", cargo: new[] { TestData.Cargo("Cajas", 10, 0.5) }));
        var maintenance = new FakeMaintenanceService();
        maintenance.Due.Add(TestData.Due("CAR-003"));

        var counting = new Counting(new FleetReportGenerator(vehicles, routes));
        return (new FleetReportService(counting, maintenance), counting);
    }

    [Fact]
    public async Task WithoutSections_ReturnsTheBaseReport()
    {
        var (service, _) = Create();

        foreach (var requested in new IEnumerable<string>?[] { null, Array.Empty<string>(), new[] { "" }, new[] { " , ," } })
        {
            var report = await service.GenerateFleetReportAsync(requested);

            Assert.Equal(new[] { "Vehicles", "Routes" }, report.Metadata.IncludedSections);
            Assert.Null(report.Statistics);
            Assert.Null(report.MaintenanceAlerts);
            Assert.Null(report.CargoSummary);
        }
    }

    [Theory]
    [InlineData("Statistics")]
    [InlineData("MaintenanceAlerts")]
    [InlineData("CargoSummary")]
    public async Task ASingleSection_AddsExactlyThatSection(string section)
    {
        var (service, _) = Create();

        var report = await service.GenerateFleetReportAsync(new[] { section });

        Assert.Equal(new[] { "Vehicles", "Routes", section }, report.Metadata.IncludedSections);
        Assert.Equal(section == "Statistics", report.Statistics is not null);
        Assert.Equal(section == "MaintenanceAlerts", report.MaintenanceAlerts is not null);
        Assert.Equal(section == "CargoSummary", report.CargoSummary is not null);
    }

    [Fact]
    public async Task SectionNames_AreNotCaseSensitive()
    {
        var (service, _) = Create();

        var report = await service.GenerateFleetReportAsync(new[] { "statistics", "MAINTENANCEALERTS", "cargoSummary" });

        Assert.Equal(new[] { "Vehicles", "Routes", "Statistics", "MaintenanceAlerts", "CargoSummary" }, report.Metadata.IncludedSections);
    }

    [Fact]
    public async Task CommaSeparatedSections_AndRepeatedEntries_AreBothAccepted()
    {
        var (service, _) = Create();

        var commaSeparated = await service.GenerateFleetReportAsync(new[] { "Statistics, CargoSummary" });
        var repeated = await service.GenerateFleetReportAsync(new[] { "Statistics", "CargoSummary" });

        Assert.Equal(new[] { "Vehicles", "Routes", "Statistics", "CargoSummary" }, commaSeparated.Metadata.IncludedSections);
        Assert.Equal(commaSeparated.Metadata.IncludedSections, repeated.Metadata.IncludedSections);
    }

    [Fact]
    public async Task ADuplicatedSection_IsAppliedOnlyOnce()
    {
        var (service, _) = Create();

        var report = await service.GenerateFleetReportAsync(new[] { "Statistics,statistics", "Statistics" });

        Assert.Equal(new[] { "Vehicles", "Routes", "Statistics" }, report.Metadata.IncludedSections);
    }

    [Fact]
    public async Task TheRequestOrder_IsTheOrderInWhichTheDecoratorsAreApplied()
    {
        var (service, _) = Create();

        var report = await service.GenerateFleetReportAsync(new[] { "CargoSummary,Statistics,MaintenanceAlerts" });

        Assert.Equal(new[] { "Vehicles", "Routes", "CargoSummary", "Statistics", "MaintenanceAlerts" }, report.Metadata.IncludedSections);
    }

    [Fact]
    public async Task AnUnknownSection_IsRejectedListingTheValidOnes_AndNoReportIsGenerated()
    {
        var (service, baseGenerator) = Create();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateFleetReportAsync(new[] { "Statistics,Inventada" }));

        Assert.Contains("Inventada", error.Message);
        Assert.Contains("Statistics, MaintenanceAlerts, CargoSummary", error.Message);
        Assert.Equal(0, baseGenerator.GenerateCalls);   // ni siquiera se consultaron los subsistemas
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("99")]
    public async Task ASectionGivenAsANumber_IsRejected(string number)
    {
        var (service, _) = Create();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateFleetReportAsync(new[] { number }));

        Assert.Contains(number, error.Message);
    }

    [Fact]
    public async Task EachRequest_BuildsItsOwnChain_SoOneRequestNeverLeaksIntoTheNext()
    {
        var (service, baseGenerator) = Create();

        var first = await service.GenerateFleetReportAsync(new[] { "Statistics,CargoSummary" });
        var second = await service.GenerateFleetReportAsync(null);

        Assert.Equal(new[] { "Vehicles", "Routes", "Statistics", "CargoSummary" }, first.Metadata.IncludedSections);
        Assert.Equal(new[] { "Vehicles", "Routes" }, second.Metadata.IncludedSections);
        Assert.Equal(2, baseGenerator.GenerateCalls);
    }
}
