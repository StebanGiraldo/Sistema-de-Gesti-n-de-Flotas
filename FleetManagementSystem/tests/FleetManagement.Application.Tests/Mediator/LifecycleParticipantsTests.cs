using FleetManagement.Application.Mediators;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Persistence;
using Xunit;

namespace FleetManagement.Application.Tests.Mediator;

/// <summary>
/// PRUEBAS DE LOS DOS PARTICIPANTES (colegas) del Mediator, cada uno por separado y con repositorios reales en memoria:
/// el de rutas guarda el cambio de estado y avisa al mediador; el de vehículos aplica la regla de disponibilidad.
/// Ninguno de los dos conoce al otro: lo que uno necesita del otro lo recibe en la llamada.
/// </summary>
public class LifecycleParticipantsTests
{
    // =====================================================================
    // Participante de VEHÍCULOS
    // =====================================================================

    private readonly InMemoryVehicleRepository _vehicleRepository = new();

    private VehicleColleague Vehicles() => new(_vehicleRepository);

    private async Task<Vehicle> VehicleAsync(VehicleStatus status)
        => await _vehicleRepository.AddAsync(new Vehicle { LicensePlate = "VAN-001", Type = VehicleType.Van, Status = status });

    private async Task<VehicleStatus> StatusOfAsync(Vehicle vehicle) => (await _vehicleRepository.GetByIdAsync(vehicle.Id))!.Status;

    [Fact]
    public async Task MarkEnRoute_PutsTheVehicleEnRoute()
    {
        var van = await VehicleAsync(VehicleStatus.Available);

        await Vehicles().MarkEnRouteAsync(van.Id);

        Assert.Equal(VehicleStatus.EnRoute, await StatusOfAsync(van));
    }

    [Fact]
    public async Task MarkEnRoute_IsUnconditional_AsItAlwaysWasInUpdateRouteStatus()
    {
        // Se conserva tal cual estaba. Lo que impide arrancar un viaje con un vehículo en mantenimiento es el validador de
        // asignación (patrón Decorator), que corre ANTES; este participante no repite esa regla (ver las pruebas de integración).
        var van = await VehicleAsync(VehicleStatus.Maintenance);

        await Vehicles().MarkEnRouteAsync(van.Id);

        Assert.Equal(VehicleStatus.EnRoute, await StatusOfAsync(van));
    }

    [Fact]
    public async Task MarkEnRoute_ForAVehicleThatNoLongerExists_DoesNothing()
    {
        await Vehicles().MarkEnRouteAsync(Guid.NewGuid());

        Assert.Empty(await _vehicleRepository.GetAllAsync());
    }

    [Theory]
    [InlineData(VehicleStatus.EnRoute, false, VehicleStatus.Available)]      // regla 2: nadie más lo usa -> se libera
    [InlineData(VehicleStatus.EnRoute, true, VehicleStatus.EnRoute)]         // regla 2: sigue en otro viaje -> se queda
    [InlineData(VehicleStatus.Maintenance, false, VehicleStatus.Maintenance)] // regla 3: terminar una ruta no lo repara
    [InlineData(VehicleStatus.Maintenance, true, VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService, false, VehicleStatus.OutOfService)]
    [InlineData(VehicleStatus.OutOfService, true, VehicleStatus.OutOfService)]
    [InlineData(VehicleStatus.Available, false, VehicleStatus.Available)]
    [InlineData(VehicleStatus.Available, true, VehicleStatus.Available)]
    public async Task ReleaseAfterTrip_AppliesTheAvailabilityRule(VehicleStatus current, bool otherActiveTrip, VehicleStatus expected)
    {
        var van = await VehicleAsync(current);

        await Vehicles().ReleaseAfterTripAsync(van.Id, otherActiveTrip);

        Assert.Equal(expected, await StatusOfAsync(van));
    }

    [Fact]
    public async Task ReleaseAfterTrip_ForAVehicleThatNoLongerExists_DoesNothing()
    {
        await Vehicles().ReleaseAfterTripAsync(Guid.NewGuid(), hasOtherActiveTrip: false);

        Assert.Empty(await _vehicleRepository.GetAllAsync());
    }

    [Fact]
    public void TheVehicleParticipant_CannotBeBuiltWithoutItsRepository()
    {
        Assert.Throws<ArgumentNullException>(() => new VehicleColleague(null!));
    }

    // =====================================================================
    // Participante de RUTAS
    // =====================================================================

    private static (RouteColleague Colleague, RecordingMediator Mediator, LoggingRouteRepository Routes, List<string> Log) RouteSide()
    {
        var log = new List<string>();
        var routes = new LoggingRouteRepository(log);
        var mediator = new RecordingMediator(log);
        return (new RouteColleague(routes, mediator), mediator, routes, log);
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.Planned)]
    [InlineData(DeliveryRouteStatus.InProgress)]
    [InlineData(DeliveryRouteStatus.Delayed)]
    [InlineData(DeliveryRouteStatus.Completed)]
    [InlineData(DeliveryRouteStatus.Cancelled)]
    public async Task ChangeStatus_SavesTheNewStatus_AndTellsTheMediatorWhoAndWhat(DeliveryRouteStatus newStatus)
    {
        var (colleague, mediator, routes, _) = RouteSide();
        var route = await routes.AddAsync(new DeliveryRoute { Name = "Ruta Norte", Status = DeliveryRouteStatus.Planned });

        await colleague.ChangeStatusAsync(route, newStatus);

        Assert.Equal(newStatus, (await routes.GetByIdAsync(route.Id))!.Status);
        var notice = Assert.Single(mediator.Notices);
        Assert.Equal("StatusChanged", notice.Kind);
        Assert.Same(colleague, notice.Sender);   // se presenta a sí mismo: el mediador le contesta a quien avisó
        Assert.Same(route, notice.Route);
        Assert.Equal(newStatus, notice.StatusAtTheTime);
    }

    [Fact]
    public async Task ChangeStatus_SavesFirst_AndOnlyThenTellsTheMediator()
    {
        var (colleague, _, routes, log) = RouteSide();
        var route = await routes.AddAsync(new DeliveryRoute { Name = "Ruta Norte" });

        await colleague.ChangeStatusAsync(route, DeliveryRouteStatus.Completed);

        // Si el aviso fuera antes que el guardado, al preguntar por "otros viajes activos" el mediador vería datos viejos.
        Assert.Equal(new[] { "saved:Completed", "notice:StatusChanged" }, log.ToArray());
    }

    [Fact]
    public async Task ReportDelay_AddsTheMinutes_LeavesTheRouteDelayed_SavesIt_AndTellsTheMediator()
    {
        var (colleague, mediator, routes, log) = RouteSide();
        var route = await routes.AddAsync(new DeliveryRoute { Name = "Ruta Norte", Status = DeliveryRouteStatus.InProgress, DelayMinutes = 5 });

        await colleague.ReportDelayAsync(route, 15);

        var stored = (await routes.GetByIdAsync(route.Id))!;
        Assert.Equal(20, stored.DelayMinutes);
        Assert.Equal(DeliveryRouteStatus.Delayed, stored.Status);

        var notice = Assert.Single(mediator.Notices);
        Assert.Equal("DelayReported", notice.Kind);   // no es "StatusChanged": el retraso por alerta no mueve al vehículo
        Assert.Same(colleague, notice.Sender);
        Assert.Equal(new[] { "saved:Delayed", "notice:DelayReported" }, log.ToArray());
    }

    [Fact]
    public async Task ReportDelay_TwoDelays_Accumulate()
    {
        var (colleague, _, routes, _) = RouteSide();
        var route = await routes.AddAsync(new DeliveryRoute { Name = "Ruta Norte", Status = DeliveryRouteStatus.InProgress });

        await colleague.ReportDelayAsync(route, 10);
        await colleague.ReportDelayAsync(route, 25);

        Assert.Equal(35, (await routes.GetByIdAsync(route.Id))!.DelayMinutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task ReportDelay_WithoutPositiveMinutes_IsRejected_AndNothingIsSavedOrNotified(int minutes)
    {
        var (colleague, mediator, routes, log) = RouteSide();
        var route = await routes.AddAsync(new DeliveryRoute { Name = "Ruta Norte", Status = DeliveryRouteStatus.InProgress, DelayMinutes = 5 });

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => colleague.ReportDelayAsync(route, minutes));

        Assert.Equal(5, route.DelayMinutes);
        Assert.Equal(DeliveryRouteStatus.InProgress, route.Status);
        Assert.Empty(log);
        Assert.Empty(mediator.Notices);
    }

    [Fact]
    public async Task ARouteThatIsMissing_IsRejected()
    {
        var (colleague, _, _, _) = RouteSide();

        await Assert.ThrowsAsync<ArgumentNullException>(() => colleague.ChangeStatusAsync(null!, DeliveryRouteStatus.Completed));
        await Assert.ThrowsAsync<ArgumentNullException>(() => colleague.ReportDelayAsync(null!, 10));
    }

    // ---------- Qué es "otro viaje activo" (lo sabe el participante de rutas) ----------

    private readonly InMemoryDeliveryRouteRepository _routeRepository = new();
    private readonly Guid _vehicleId = Guid.NewGuid();

    private RouteColleague RealRouteSide() => new(_routeRepository, new RecordingMediator());

    private Task<DeliveryRoute> OtherRouteAsync(DeliveryRouteStatus status, Guid? vehicleId)
        => _routeRepository.AddAsync(new DeliveryRoute { Name = $"Otra {status}", Status = status, AssignedVehicleId = vehicleId });

    [Theory]
    [InlineData(DeliveryRouteStatus.InProgress, true)]
    [InlineData(DeliveryRouteStatus.Delayed, true)]     // una ruta retrasada sigue en la carretera
    [InlineData(DeliveryRouteStatus.Planned, false)]
    [InlineData(DeliveryRouteStatus.Completed, false)]
    [InlineData(DeliveryRouteStatus.Cancelled, false)]
    public async Task HasOtherActiveTrip_OnlyCountsRoutesThatAreOnTheRoad(DeliveryRouteStatus otherStatus, bool expected)
    {
        await OtherRouteAsync(otherStatus, _vehicleId);

        Assert.Equal(expected, await RealRouteSide().HasOtherActiveTripAsync(_vehicleId, Guid.NewGuid()));
    }

    [Fact]
    public async Task HasOtherActiveTrip_DoesNotCountTheRouteThatIsEnding()
    {
        var ending = await OtherRouteAsync(DeliveryRouteStatus.InProgress, _vehicleId);

        Assert.False(await RealRouteSide().HasOtherActiveTripAsync(_vehicleId, ending.Id));
    }

    [Fact]
    public async Task HasOtherActiveTrip_DoesNotCountTheActiveRoutesOfAnotherVehicle()
    {
        await OtherRouteAsync(DeliveryRouteStatus.InProgress, Guid.NewGuid());
        await OtherRouteAsync(DeliveryRouteStatus.Delayed, null);

        Assert.False(await RealRouteSide().HasOtherActiveTripAsync(_vehicleId, Guid.NewGuid()));
    }

    [Fact]
    public async Task HasOtherActiveTrip_WithNoRoutesAtAll_IsFalse()
    {
        Assert.False(await RealRouteSide().HasOtherActiveTripAsync(_vehicleId, Guid.NewGuid()));
    }

    [Fact]
    public void TheRouteParticipant_CannotBeBuiltWithoutItsRepositoryAndMediator()
    {
        Assert.Throws<ArgumentNullException>(() => new RouteColleague(null!, new RecordingMediator()));
        Assert.Throws<ArgumentNullException>(() => new RouteColleague(new InMemoryDeliveryRouteRepository(), null!));
    }
}
