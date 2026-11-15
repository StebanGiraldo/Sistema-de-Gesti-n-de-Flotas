using FleetManagement.Application.Analysis;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Services;
using FleetManagement.Application.Tests.Simulation;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Domain.States;
using Xunit;

namespace FleetManagement.Application.Tests.Visitor;

/// <summary>
/// El servicio de análisis (<see cref="FleetAnalysisService"/>) junta los cuatro visitantes sobre los datos REALES de la aplicación:
/// los repositorios en memoria que llenan los servicios reales. Datos de la prueba (los de <see cref="RealFleet.SeedAsync"/>): cinco
/// vehículos (Disponible, En ruta, En mantenimiento, Disponible y Fuera de servicio), tres rutas (una en curso con VAN-002, una
/// planificada sin vehículo y una retrasada sin vehículo con 20 min) y dos mantenimientos (CAR-003 a 2 870 km de su servicio y TRK-001 a 0).
/// </summary>
public class FleetAnalysisServiceTests
{
    private readonly RealFleet _fleet = new();
    private readonly ManualTimeProvider _clock = new();

    private FleetAnalysisService Service(IVehicleStateResolver? states = null, InsightThresholds? thresholds = null)
        => new(_fleet.VehicleRepository, _fleet.RouteRepository, _fleet.MaintenanceRepository, states ?? VehicleStateResolver.Default, _clock, thresholds);

    private async Task<(FleetAnalysisDto Analysis, IReadOnlyDictionary<string, Vehicle> Vehicles)> SeededAnalysisAsync()
    {
        var vehicles = await _fleet.SeedAsync();
        return (await Service().AnalyzeFleetAsync(), vehicles);
    }

    // ---------------- Lo que dice sobre la flota real ----------------

    [Fact]
    public async Task ItAnalyzesEveryElementOfTheFleet_AndSaysWhen()
    {
        var (analysis, _) = await SeededAnalysisAsync();

        Assert.Equal(_clock.GetUtcNow(), analysis.GeneratedAt);
        Assert.Equal((5, 3, 2), (analysis.Elements.Vehicles, analysis.Elements.Routes, analysis.Elements.MaintenanceRecords));
    }

    [Fact]
    public async Task TheUtilization_ReflectsTheStatesOfTheRealVehicles()
    {
        var (analysis, _) = await SeededAnalysisAsync();
        var utilization = analysis.Utilization;

        Assert.Equal(5, utilization.TotalVehicles);
        Assert.Equal(3, utilization.OperableVehicles);        // Disponible, En ruta y Disponible; el de mantenimiento y el fuera de servicio no
        Assert.Equal(1, utilization.InUseVehicles);           // solo VAN-002 tiene una ruta en curso
        Assert.Equal(33.3, utilization.UtilizationPercent);
        Assert.Equal(233990, utilization.TotalMileageKm);     // 45 210 + 18 340 + 62 870 + 9 120 + 98 450
        Assert.Equal(46798, utilization.AverageMileageKm);
        Assert.True(utilization.Vehicles.Single(vehicle => vehicle.LicensePlate == "VAN-002").InUse);
    }

    [Fact]
    public async Task TheRouteAnalysis_CountsTheThreeRoutesAndTheirStatuses()
    {
        var (analysis, _) = await SeededAnalysisAsync();
        var routes = analysis.Routes;

        Assert.Equal(3, routes.TotalRoutes);
        Assert.Equal(1, routes.DelayedRoutes);
        Assert.Equal(20, routes.TotalDelayMinutes);
        Assert.Equal(new[] { 1, 1, 1, 0, 0 }, routes.ByStatus.Select(status => status.Routes));   // Planned, InProgress, Delayed, Completed, Cancelled
    }

    [Fact]
    public async Task TheMaintenanceAnalysis_SeparatesTheVehiclesThatHaveHistoryFromThoseThatDoNot()
    {
        var (analysis, _) = await SeededAnalysisAsync();
        var maintenance = analysis.Maintenance;

        Assert.Equal(2, maintenance.TotalRecords);
        Assert.Equal(2, maintenance.VehiclesWithRecords);
        Assert.Equal(new[] { "MOT-004", "VAN-002", "VAN-005" }, maintenance.VehiclesWithoutRecords);
        Assert.Null(maintenance.AverageKmBetweenServices);                                       // un solo servicio por vehículo
        Assert.Equal(2870, maintenance.Vehicles.Single(vehicle => vehicle.LicensePlate == "CAR-003").KmSinceLastService);
        Assert.Equal(0, maintenance.Vehicles.Single(vehicle => vehicle.LicensePlate == "TRK-001").KmSinceLastService);
    }

    [Fact]
    public async Task TheInsights_SayWhatAnOperatorShouldAttendFirst()
    {
        var (analysis, _) = await SeededAnalysisAsync();

        Assert.Equal(
            new[]
            {
                ("Critical", InsightCodes.ActiveRouteWithoutOperableVehicle, "Ruta sin carga",
                    "La ruta 'Ruta sin carga' está en curso pero no puede avanzar. No tiene vehículo asignado: nadie la recorre."),
                ("Warning", InsightCodes.ServiceDistanceExceeded, "VAN-002",
                    "El vehículo VAN-002 no tiene ningún mantenimiento registrado y lleva 18340 km (umbral: 10000 km)."),
                ("Warning", InsightCodes.ServiceDistanceExceeded, "VAN-005",
                    "El vehículo VAN-005 no tiene ningún mantenimiento registrado y lleva 98450 km (umbral: 10000 km)."),
                ("Info", InsightCodes.PlannedRouteWithoutVehicle, "Ruta Bucaramanga - Bogotá",
                    "La ruta planificada 'Ruta Bucaramanga - Bogotá' todavía no tiene vehículo asignado.")
            },
            analysis.Insights.Select(insight => (insight.Severity, insight.Code, insight.Subject, insight.Message)));
    }

    [Fact]
    public async Task AnEmptyFleet_IsAnalyzedWithoutErrors()
    {
        var analysis = await Service().AnalyzeFleetAsync();

        Assert.Equal((0, 0, 0), (analysis.Elements.Vehicles, analysis.Elements.Routes, analysis.Elements.MaintenanceRecords));
        Assert.Empty(analysis.Insights);
        Assert.Null(analysis.Routes.LongestRoute);
        Assert.Null(analysis.Maintenance.AverageKmBetweenServices);
    }

    // ---------------- Sigue a los otros patrones ----------------

    [Fact]
    public async Task ChangingAVehicleStateThroughTheService_ChangesTheAnalysis()
    {
        var (_, vehicles) = await SeededAnalysisAsync();

        await _fleet.Vehicles.UpdateVehicleStatusAsync(vehicles["TRK-001"].Id, new UpdateVehicleStatusRequest("Maintenance"));   // State: Disponible → Mantenimiento
        var analysis = await Service().AnalyzeFleetAsync();

        Assert.Equal(2, analysis.Utilization.OperableVehicles);                         // ya no puede operar
        Assert.Equal("Maintenance", analysis.Utilization.Vehicles.Single(vehicle => vehicle.LicensePlate == "TRK-001").Status);
    }

    [Fact]
    public async Task ALeftoverAvailableVehicleOnAnActiveRoute_ShowsUpAsAWarning()
    {
        var (_, vehicles) = await SeededAnalysisAsync();

        // Permitido por el estado (En ruta → Disponible), pero la ruta de VAN-002 sigue en curso y ya nadie la mueve.
        await _fleet.Vehicles.UpdateVehicleStatusAsync(vehicles["VAN-002"].Id, new UpdateVehicleStatusRequest("Available"));
        var analysis = await Service().AnalyzeFleetAsync();

        var insight = Assert.Single(analysis.Insights, found => found.Code == InsightCodes.ActiveRouteVehicleNotEnRoute);
        Assert.Equal("Ruta Girón - Bucaramanga Centro", insight.Subject);
        Assert.Contains("VAN-002", insight.Message);
    }

    [Fact]
    public async Task CompletingTheTripThroughTheRealRouteService_ReleasesTheVehicle_AndTheAnalysisSeesIt()
    {
        var (_, vehicles) = await SeededAnalysisAsync();
        var route = (await _fleet.Routes.GetAllRoutesAsync()).Single(found => found.Name == "Ruta Girón - Bucaramanga Centro");

        await _fleet.Routes.UpdateRouteStatusAsync(route.Id, "Completed");   // Mediator + State: VAN-002 queda Disponible
        var analysis = await Service().AnalyzeFleetAsync();

        Assert.Equal(VehicleStatus.Available, vehicles["VAN-002"].Status);
        Assert.Equal(0, analysis.Utilization.InUseVehicles);
        Assert.Equal(1, analysis.Routes.ByStatus.Single(status => status.Status == "Completed").Routes);
        Assert.DoesNotContain(analysis.Insights, insight => insight.Code == InsightCodes.ActiveRouteVehicleNotEnRoute);
    }

    [Fact]
    public async Task TheService_UsesTheStateResolverItIsGiven()
    {
        await _fleet.SeedAsync();
        var permissive = new ConfigurableResolver().Override(new ConfigurableState(VehicleStatus.Maintenance, canOperate: true, canTrackTrip: false));

        var analysis = await Service(permissive).AnalyzeFleetAsync();

        Assert.Equal(4, analysis.Utilization.OperableVehicles);   // ahora el vehículo en mantenimiento también "puede operar"
    }

    [Fact]
    public async Task TheService_UsesTheThresholdsItIsGiven()
    {
        await _fleet.SeedAsync();

        var analysis = await Service(thresholds: new InsightThresholds(DelayAlertMinutes: 10, ServiceDistanceAlertKm: 50000)).AnalyzeFleetAsync();

        Assert.Contains(analysis.Insights, insight => insight.Code == InsightCodes.ActiveRouteDelayed && insight.Subject == "Ruta sin carga");   // 20 min ≥ 10
        Assert.DoesNotContain(analysis.Insights, insight => insight.Code == InsightCodes.ServiceDistanceExceeded && insight.Subject == "VAN-002");  // 18 340 < 50 000
        Assert.Contains(analysis.Insights, insight => insight.Code == InsightCodes.ServiceDistanceExceeded && insight.Subject == "VAN-005");       // 98 450 ≥ 50 000
    }

    // ---------------- Solo lectura ----------------

    [Fact]
    public async Task AnalyzingTheFleet_ChangesNothing_NeitherDataNorAuditNorNotifications()
    {
        await _fleet.SeedAsync();
        var vehiclesBefore = Json.Of(await _fleet.Vehicles.GetAllVehiclesAsync());
        var routesBefore = Json.Of(await _fleet.Routes.GetAllRoutesAsync());
        var auditBefore = _fleet.Audit.Entries.Count;
        var notificationsBefore = _fleet.Channel.SentMessages.Count;

        await Service().AnalyzeFleetAsync();

        Assert.Equal(vehiclesBefore, Json.Of(await _fleet.Vehicles.GetAllVehiclesAsync()));
        Assert.Equal(routesBefore, Json.Of(await _fleet.Routes.GetAllRoutesAsync()));
        Assert.Equal(auditBefore, _fleet.Audit.Entries.Count);
        Assert.Equal(notificationsBefore, _fleet.Channel.SentMessages.Count);
    }

    [Fact]
    public async Task AnalyzingTwice_GivesTheSameAnswer()
    {
        await _fleet.SeedAsync();
        var service = Service();

        Assert.Equal(Json.Of(await service.AnalyzeFleetAsync()), Json.Of(await service.AnalyzeFleetAsync()));
    }

    [Fact]
    public async Task EachRepositoryIsReadOnce_NoMatterHowManyElementsThereAre()
    {
        var log = new EventLog();
        var vehicles = new FakeVehicleRepository(log);
        var routes = new FakeRouteRepository();
        for (var index = 0; index < 20; index++)
        {
            var vehicle = await vehicles.AddAsync(FleetData.Vehicle($"VAN-{index:000}", status: VehicleStatus.EnRoute));
            await routes.AddAsync(FleetData.Route($"Ruta {index}", DeliveryRouteStatus.InProgress, vehicle));
        }
        var service = new FleetAnalysisService(vehicles, routes, _fleet.MaintenanceRepository, VehicleStateResolver.Default, _clock);

        await service.AnalyzeFleetAsync();

        Assert.Equal((1, 0), (vehicles.GetAllCalls, vehicles.GetByIdCalls));   // sin una consulta por cada vehículo (nada de N+1)
        Assert.Equal((1, 0), (routes.GetAllCalls, routes.GetByIdCalls));
    }

    [Fact]
    public async Task EveryCallBuildsItsOwnVisitors_SoTwoAnalysesNeverShareState()
    {
        var (first, vehicles) = await SeededAnalysisAsync();
        await _fleet.Vehicles.UpdateVehicleStatusAsync(vehicles["MOT-004"].Id, new UpdateVehicleStatusRequest("OutOfService"));

        var second = await Service().AnalyzeFleetAsync();
        var third = await Service().AnalyzeFleetAsync();

        Assert.Equal(3, first.Utilization.OperableVehicles);
        Assert.Equal(2, second.Utilization.OperableVehicles);          // refleja el cambio
        Assert.Equal(Json.Of(second), Json.Of(third));                 // y un tercer análisis no arrastra nada del anterior
        Assert.Equal(5, third.Utilization.TotalVehicles);              // (si el visitante se reutilizara, estas cuentas se acumularían)
    }
}
