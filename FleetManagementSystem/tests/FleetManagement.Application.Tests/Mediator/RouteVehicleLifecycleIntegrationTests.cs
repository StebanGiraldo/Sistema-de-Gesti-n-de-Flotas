using FleetManagement.Application.Assignment;
using FleetManagement.Application.Builders;
using FleetManagement.Application.Facades;
using FleetManagement.Application.Services;
using FleetManagement.Application.Tests.AlertHandling;
using FleetManagement.Application.Tests.Decorator;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Persistence;
using Xunit;

namespace FleetManagement.Application.Tests.Mediator;

/// <summary>
/// PRUEBAS DE INTEGRACIÓN DEL MEDIATOR a través de los casos de uso reales (<c>DeliveryRouteService</c> y
/// <c>TripAlertService</c>), con repositorios reales en memoria y sin mocks. Comprueban las cuatro reglas de negocio que
/// ya existían, que el mediador conserva tal cual, y cómo convive con la cadena de alertas, el puente de notificaciones,
/// los decoradores, la auditoría y el resumen del dashboard (Facade).
/// </summary>
public class RouteVehicleLifecycleIntegrationTests
{
    private readonly InMemoryVehicleRepository _vehicles = new();
    private readonly InMemoryDriverRepository _drivers = new();
    private readonly InMemoryDeliveryRouteRepository _routes = new();
    private readonly RecordingAuditLogger _audit = new();

    private DeliveryRouteService Service()
        => new(_routes, _vehicles, _drivers, new DeliveryRouteBuilder(), new DeliveryRouteDirector(), _audit, FleetMediators.CreateRouteColleague(_routes, _vehicles));

    private async Task<Vehicle> VehicleAsync(VehicleStatus status, string plate = "VAN-001")
        => await _vehicles.AddAsync(new Vehicle { LicensePlate = plate, Type = VehicleType.Van, Status = status });

    private async Task<DeliveryRoute> RouteAsync(string name, DeliveryRouteStatus status, Vehicle? vehicle)
        => await _routes.AddAsync(new DeliveryRoute { Name = name, Status = status, AssignedVehicleId = vehicle?.Id });

    private async Task<VehicleStatus> StatusOfAsync(Vehicle vehicle) => (await _vehicles.GetByIdAsync(vehicle.Id))!.Status;

    private async Task<DeliveryRouteStatus> StatusOfAsync(DeliveryRoute route) => (await _routes.GetByIdAsync(route.Id))!.Status;

    // ---------- Regla 1: En progreso o Retrasada -> el vehículo asignado queda En ruta ----------

    [Theory]
    [InlineData("InProgress", DeliveryRouteStatus.InProgress)]
    [InlineData("Delayed", DeliveryRouteStatus.Delayed)]
    public async Task StartingOrDelayingARoute_LeavesItsVehicleEnRoute(string status, DeliveryRouteStatus expectedRoute)
    {
        var van = await VehicleAsync(VehicleStatus.Available);
        var route = await RouteAsync("Ruta Norte", DeliveryRouteStatus.Planned, van);

        await Service().UpdateRouteStatusAsync(route.Id, status);

        Assert.Equal(expectedRoute, await StatusOfAsync(route));
        Assert.Equal(VehicleStatus.EnRoute, await StatusOfAsync(van));
    }

    [Theory]
    [InlineData("InProgress")]
    [InlineData("Delayed")]
    public async Task AVehicleThatIsAlreadyEnRoute_StaysEnRoute(string status)
    {
        var van = await VehicleAsync(VehicleStatus.EnRoute);
        var route = await RouteAsync("Ruta Norte", DeliveryRouteStatus.InProgress, van);

        await Service().UpdateRouteStatusAsync(route.Id, status);

        Assert.Equal(VehicleStatus.EnRoute, await StatusOfAsync(van));
    }

    // ---------- Regla 2: Completada o Cancelada -> Disponible sólo si no sigue en otro viaje activo ----------

    [Theory]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public async Task EndingTheOnlyTripOfAVehicle_ReleasesIt(string status)
    {
        var van = await VehicleAsync(VehicleStatus.EnRoute);
        var route = await RouteAsync("Ruta Norte", DeliveryRouteStatus.InProgress, van);

        await Service().UpdateRouteStatusAsync(route.Id, status);

        Assert.Equal(VehicleStatus.Available, await StatusOfAsync(van));
    }

    [Theory]
    [InlineData("Completed", DeliveryRouteStatus.InProgress)]
    [InlineData("Cancelled", DeliveryRouteStatus.InProgress)]
    [InlineData("Completed", DeliveryRouteStatus.Delayed)]
    [InlineData("Cancelled", DeliveryRouteStatus.Delayed)]
    public async Task EndingATrip_KeepsTheVehicleEnRoute_WhileAnotherActiveTripUsesIt(string status, DeliveryRouteStatus otherTrip)
    {
        var van = await VehicleAsync(VehicleStatus.EnRoute);
        var ending = await RouteAsync("Termina", DeliveryRouteStatus.InProgress, van);
        var other = await RouteAsync("Sigue", otherTrip, van);

        await Service().UpdateRouteStatusAsync(ending.Id, status);

        Assert.Equal(VehicleStatus.EnRoute, await StatusOfAsync(van));
        Assert.Equal(otherTrip, await StatusOfAsync(other));   // la otra ruta ni se toca
    }

    // ---------- Regla 3: Mantenimiento y Fuera de servicio no pasan a Disponible por terminar una ruta ----------

    [Theory]
    [InlineData(VehicleStatus.Maintenance, "Completed")]
    [InlineData(VehicleStatus.Maintenance, "Cancelled")]
    [InlineData(VehicleStatus.OutOfService, "Completed")]
    [InlineData(VehicleStatus.OutOfService, "Cancelled")]
    public async Task EndingARoute_NeverMakesAvailableAVehicleThatIsInMaintenanceOrOutOfService(VehicleStatus vehicleStatus, string status)
    {
        var van = await VehicleAsync(vehicleStatus);
        var route = await RouteAsync("Ruta Norte", DeliveryRouteStatus.InProgress, van);

        await Service().UpdateRouteStatusAsync(route.Id, status);

        Assert.Equal(vehicleStatus, await StatusOfAsync(van));
        Assert.Equal(Enum.Parse<DeliveryRouteStatus>(status), await StatusOfAsync(route));   // la ruta sí cambia
    }

    // ---------- Planificada: el vehículo no se toca ----------

    [Theory]
    [InlineData(VehicleStatus.Available)]
    [InlineData(VehicleStatus.EnRoute)]
    [InlineData(VehicleStatus.Maintenance)]
    public async Task BackToPlanned_LeavesTheVehicleAsItWas(VehicleStatus vehicleStatus)
    {
        var van = await VehicleAsync(vehicleStatus);
        var route = await RouteAsync("Ruta Norte", DeliveryRouteStatus.InProgress, van);

        await Service().UpdateRouteStatusAsync(route.Id, "Planned");

        Assert.Equal(vehicleStatus, await StatusOfAsync(van));
        Assert.Equal(DeliveryRouteStatus.Planned, await StatusOfAsync(route));
    }

    // ---------- Regla 4: ruta sin vehículo asignado (o con un vehículo que ya no existe) ----------

    [Theory]
    [InlineData("Planned")]
    [InlineData("InProgress")]
    [InlineData("Delayed")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public async Task ARouteWithoutAVehicle_ChangesItsStatus_AndNoVehicleIsTouched(string status)
    {
        var bystander = await VehicleAsync(VehicleStatus.EnRoute, "VAN-009");
        await RouteAsync("De otro", DeliveryRouteStatus.InProgress, bystander);
        var route = await RouteAsync("Sin vehículo", DeliveryRouteStatus.Planned, null);

        await Service().UpdateRouteStatusAsync(route.Id, status);

        Assert.Equal(Enum.Parse<DeliveryRouteStatus>(status), await StatusOfAsync(route));
        Assert.Equal(VehicleStatus.EnRoute, await StatusOfAsync(bystander));
        Assert.Single(_audit.Entries);
    }

    [Theory]
    [InlineData("InProgress")]
    [InlineData("Completed")]
    public async Task ARouteWhoseVehicleNoLongerExists_StillChangesItsStatus_WithoutFailing(string status)
    {
        var route = await _routes.AddAsync(new DeliveryRoute { Name = "Vehículo borrado", Status = DeliveryRouteStatus.Planned, AssignedVehicleId = Guid.NewGuid() });

        await Service().UpdateRouteStatusAsync(route.Id, status);

        Assert.Equal(Enum.Parse<DeliveryRouteStatus>(status), await StatusOfAsync(route));
        Assert.Empty(await _vehicles.GetAllAsync());   // el mediador no inventa vehículos
    }

    // ---------- Lo que el servicio sigue haciendo por su cuenta: validar y auditar ----------

    [Fact]
    public async Task AnUnknownRoute_StillFailsWithTheSameMessage_AndNothingChanges()
    {
        var van = await VehicleAsync(VehicleStatus.EnRoute);

        var error = await Assert.ThrowsAsync<KeyNotFoundException>(() => Service().UpdateRouteStatusAsync(Guid.NewGuid(), "Completed"));

        Assert.Equal("Ruta no encontrada.", error.Message);
        Assert.Equal(VehicleStatus.EnRoute, await StatusOfAsync(van));
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task AnInvalidStatus_StillFailsBeforeTheMediatorActs_AndNothingChanges()
    {
        var van = await VehicleAsync(VehicleStatus.EnRoute);
        var route = await RouteAsync("Ruta Norte", DeliveryRouteStatus.InProgress, van);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service().UpdateRouteStatusAsync(route.Id, "Volando"));

        Assert.Equal("Estado de ruta inválido: 'Volando'.", error.Message);
        Assert.Equal(DeliveryRouteStatus.InProgress, await StatusOfAsync(route));
        Assert.Equal(VehicleStatus.EnRoute, await StatusOfAsync(van));
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task EachStatusChange_WritesExactlyTheSameAuditLineAsBefore_AndTheMediatorAddsNone()
    {
        var van = await VehicleAsync(VehicleStatus.EnRoute);
        var route = await RouteAsync("Ruta Norte", DeliveryRouteStatus.InProgress, van);

        await Service().UpdateRouteStatusAsync(route.Id, "Completed");

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("Ruta", entry.Category);
        Assert.Equal("Ruta 'Ruta Norte' cambió su estado a Completed.", entry.Message);
    }

    // ---------- Convivencia con el Decorator de asignación ----------

    [Fact]
    public async Task TheAssignmentRules_StillRunBeforeTheMediator_AndAnAllowedStartGoesThroughIt()
    {
        var validator = new RouteAssignmentValidator(_vehicles, _drivers, _routes, new FakeMaintenanceService());
        var guarded = new AssignmentValidatingRouteService(Service(), validator);

        var broken = await VehicleAsync(VehicleStatus.Maintenance, "CAR-003");
        var blocked = await RouteAsync("Bloqueada", DeliveryRouteStatus.Planned, broken);
        await Assert.ThrowsAsync<AssignmentRuleException>(() => guarded.UpdateRouteStatusAsync(blocked.Id, "InProgress"));

        Assert.Equal(VehicleStatus.Maintenance, await StatusOfAsync(broken));   // el mediador ni llegó a actuar
        Assert.Equal(DeliveryRouteStatus.Planned, await StatusOfAsync(blocked));

        var healthy = await VehicleAsync(VehicleStatus.Available, "VAN-002");
        var allowed = await RouteAsync("Permitida", DeliveryRouteStatus.Planned, healthy);
        await guarded.UpdateRouteStatusAsync(allowed.Id, "InProgress");

        Assert.Equal(VehicleStatus.EnRoute, await StatusOfAsync(healthy));
    }

    // ---------- Convivencia con la cadena de alertas, el puente de notificaciones y sus decoradores ----------

    private static async Task<(Vehicle Van, DeliveryRoute Route)> OnTheRoadAsync(AlertHandlingKit kit, VehicleStatus vehicleStatus = VehicleStatus.EnRoute, DeliveryRouteStatus routeStatus = DeliveryRouteStatus.InProgress)
    {
        var van = await kit.Vehicles.AddAsync(new Vehicle { LicensePlate = "VAN-001", Type = VehicleType.Van, Status = vehicleStatus });
        var route = await kit.Routes.AddAsync(new DeliveryRoute { Name = "Ruta Norte", Status = routeStatus, AssignedVehicleId = van.Id });
        return (van, route);
    }

    [Fact]
    public async Task ADelayAlert_GoesThroughTheChain_AndTheRouteSideLeavesTheRouteDelayed_WhileTheVehicleStaysEnRoute()
    {
        var kit = new AlertHandlingKit();
        var (van, route) = await OnTheRoadAsync(kit);

        await kit.ReportAsync(route, "Delay", delayMinutes: 15);

        var stored = (await kit.Routes.GetByIdAsync(route.Id))!;
        Assert.Equal(DeliveryRouteStatus.Delayed, stored.Status);
        Assert.Equal(15, stored.DelayMinutes);
        Assert.Equal(VehicleStatus.EnRoute, (await kit.Vehicles.GetByIdAsync(van.Id))!.Status);
    }

    [Fact]
    public async Task ADelayAlert_DoesNotTouchTheVehicle_AndWritesNoRouteStatusAuditLine_ThatDecisionIsKeptOnPurpose()
    {
        // Una ruta Planificada con su vehículo Disponible: pedir el estado "Retrasada" por la API deja al vehículo En ruta
        // (regla 1), pero el retraso que reporta una alerta NUNCA tocó al vehículo ni auditó un cambio de estado. Se conserva.
        var kit = new AlertHandlingKit();
        var (van, route) = await OnTheRoadAsync(kit, VehicleStatus.Available, DeliveryRouteStatus.Planned);

        await kit.ReportAsync(route, "Delay", delayMinutes: 15);

        Assert.Equal(DeliveryRouteStatus.Delayed, (await kit.Routes.GetByIdAsync(route.Id))!.Status);
        Assert.Equal(VehicleStatus.Available, (await kit.Vehicles.GetByIdAsync(van.Id))!.Status);
        Assert.DoesNotContain(kit.Audit.Entries, entry => entry.Category == "Ruta");
    }

    [Fact]
    public async Task TheSameStatus_RequestedThroughTheService_DoesLeaveTheVehicleEnRoute_TheContrastOfThePreviousTest()
    {
        var van = await VehicleAsync(VehicleStatus.Available);
        var route = await RouteAsync("Ruta Norte", DeliveryRouteStatus.Planned, van);

        await Service().UpdateRouteStatusAsync(route.Id, "Delayed");

        Assert.Equal(VehicleStatus.EnRoute, await StatusOfAsync(van));
    }

    [Fact]
    public async Task TheMediatorAddsNoNotificationToTheAlertFlow_ItSendsTheSameWithOrWithoutAVehicle()
    {
        var withVehicle = new AlertHandlingKit();
        var (_, routeWith) = await OnTheRoadAsync(withVehicle);
        await withVehicle.ReportAsync(routeWith, "Delay", delayMinutes: 15);

        var withoutVehicle = new AlertHandlingKit();
        var routeWithout = await withoutVehicle.AddRouteAsync();
        await withoutVehicle.ReportAsync(routeWithout, "Delay", delayMinutes: 15);

        Assert.NotEmpty(withoutVehicle.Channel.SentMessages);   // el flujo de alertas sí notifica (Bridge + Decorators)...
        Assert.Equal(
            withoutVehicle.Channel.SentMessages.Select(message => message.Recipient).ToArray(),
            withVehicle.Channel.SentMessages.Select(message => message.Recipient).ToArray());   // ...y el vehículo no cambia nada
    }

    [Fact]
    public async Task AStatusChange_SendsNoNotificationAtAll_AndWritesOnlyItsOwnAuditLine()
    {
        var fleet = new RealFleet();
        var seeded = await fleet.SeedAsync();
        var giron = (await fleet.Routes.GetAllRoutesAsync()).First(route => route.Name == "Ruta Girón - Bucaramanga Centro");

        await fleet.Routes.UpdateRouteStatusAsync(giron.Id, "Completed");

        Assert.Equal(VehicleStatus.Available, (await fleet.VehicleRepository.GetByIdAsync(seeded["VAN-002"].Id))!.Status);
        Assert.Empty(fleet.Channel.SentMessages);
        var entry = Assert.Single(fleet.Audit.Entries);
        Assert.Equal("Ruta 'Ruta Girón - Bucaramanga Centro' cambió su estado a Completed.", entry.Message);
    }

    [Fact]
    public async Task ARouteDelayedByAnAlert_CountsAsAnotherActiveTrip_WhenTheOtherRouteOfTheVehicleIsCancelled()
    {
        // Las dos entradas al mediador (cambio de estado por la API y retraso por alerta) comparten la misma definición de
        // "viaje activo": una ruta que una alerta dejó Retrasada sigue reteniendo al vehículo.
        var kit = new AlertHandlingKit();
        var (van, travelling) = await OnTheRoadAsync(kit);
        var waiting = await kit.Routes.AddAsync(new DeliveryRoute { Name = "Espera", Status = DeliveryRouteStatus.Planned, AssignedVehicleId = van.Id });
        await kit.ReportAsync(travelling, "Delay", delayMinutes: 20);   // alerta: la ruta que viaja queda Retrasada

        var routeService = new DeliveryRouteService(kit.Routes, kit.Vehicles, new InMemoryDriverRepository(), new DeliveryRouteBuilder(), new DeliveryRouteDirector(), kit.Audit, FleetMediators.CreateRouteColleague(kit.Routes, kit.Vehicles));
        await routeService.UpdateRouteStatusAsync(waiting.Id, "Cancelled");
        Assert.Equal(VehicleStatus.EnRoute, (await kit.Vehicles.GetByIdAsync(van.Id))!.Status);   // la retrasada lo retiene

        await routeService.UpdateRouteStatusAsync(travelling.Id, "Completed");
        Assert.Equal(VehicleStatus.Available, (await kit.Vehicles.GetByIdAsync(van.Id))!.Status);   // ya nadie lo usa
    }

    // ---------- Convivencia con el Facade del dashboard ----------

    [Fact]
    public async Task TheDashboardSummary_ReflectsWhatTheMediatorLeftInTheFleet()
    {
        var fleet = new RealFleet();
        await fleet.SeedAsync();
        var facade = new FleetDashboardFacade(fleet.Vehicles, fleet.Routes, fleet.Maintenance, fleet.Alerts);

        var before = (await facade.GetSummaryAsync()).Statistics;
        Assert.Equal(new[] { 2, 1, 1, 1 }, before.VehiclesByStatus.Values.ToArray());   // Available, EnRoute, Maintenance, OutOfService

        var giron = (await fleet.Routes.GetAllRoutesAsync()).First(route => route.Name == "Ruta Girón - Bucaramanga Centro");
        await fleet.Routes.UpdateRouteStatusAsync(giron.Id, "Completed");   // VAN-002 deja de estar En ruta

        var after = (await facade.GetSummaryAsync()).Statistics;
        Assert.Equal(new[] { 3, 0, 1, 1 }, after.VehiclesByStatus.Values.ToArray());
        Assert.Equal(new[] { 1, 0, 1, 1, 0 }, after.RoutesByStatus.Values.ToArray());   // Planned, InProgress, Delayed, Completed, Cancelled
        Assert.Equal(60.0, after.AvailabilityPercent);
    }
}
