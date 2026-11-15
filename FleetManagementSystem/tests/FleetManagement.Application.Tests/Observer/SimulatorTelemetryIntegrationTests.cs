using FleetManagement.Application.DTOs;
using FleetManagement.Application.Factories;
using FleetManagement.Application.Flyweights;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Monitoring;
using FleetManagement.Application.Services;
using FleetManagement.Application.Simulation;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Simulation;
using Xunit;
using static FleetManagement.Application.Tests.Simulation.SimulationTestData;

namespace FleetManagement.Application.Tests.Observer;

/// <summary>
/// OBSERVER integrado con el flujo REAL de la simulación: el simulador de movimiento publica, y quienes escuchan son la bitácora
/// de auditoría y el tablero en vivo. Los servicios son los de la aplicación (rutas con su Mediator, vehículos con su State, la
/// bitácora real en memoria) sobre repositorios en memoria; solo el reloj es de prueba. Una furgoneta (100 km/h) en la ruta del
/// ecuador (66,717 km, paradas "Primera" a 11,1 km y "Segunda" a 33,4 km) avanza en pasos de 6 minutos = 10 km.
/// </summary>
public class SimulatorTelemetryIntegrationTests
{
    private const double Tolerance = 1e-9;
    private const double RouteKm = 0.6 * KmPerDegree;
    private static readonly TimeSpan SixMinutes = TimeSpan.FromMinutes(6);
    private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);

    private static string[] Kinds(IEnumerable<TelemetryEvent> events)
        => events.Select(telemetry => telemetry switch
        {
            VehicleMovedEvent => "moved",
            TripStopReachedEvent stop => $"stop:{stop.StopLabel}",
            TripArrivedEvent => "arrived",
            _ => "other"
        }).ToArray();

    // ---------------- El flujo completo ----------------

    [Fact]
    public async Task AFullTrip_PublishesEachMovement_EachStopOnce_AndOneArrival_InOrder()
    {
        var world = new TelemetryWorld();
        var (vehicle, _) = await world.StartTripAsync("VAN-INT", EquatorRoute());

        await world.AdvanceAsync(steps: 7, SixMinutes);

        Assert.Equal(
            new[] { "moved", "moved", "stop:Primera", "moved", "moved", "stop:Segunda", "moved", "moved", "moved", "arrived" },
            Kinds(world.Recorder.Received));
        var moves = world.Recorder.Received.OfType<VehicleMovedEvent>().ToList();
        Assert.Equal(RouteKm, moves.Sum(move => move.StepKm), Tolerance);                    // los pasos suman exactamente la ruta
        Assert.Equal(1000 + RouteKm, moves[^1].MileageKm, 1e-6);                             // y el kilometraje del vehículo también
        Assert.Equal(moves.Select(move => move.ProgressPercent).Order(), moves.Select(move => move.ProgressPercent)); // nunca retrocede
        Assert.All(world.Recorder.Received, telemetry => Assert.Equal(vehicle.Id, telemetry.VehicleId));
    }

    [Fact]
    public async Task TheTelemetry_IsPublishedAfterTheStepWasSaved_SoAnObserverNeverSeesTheFuture()
    {
        var world = new TelemetryWorld();
        var (vehicle, route) = await world.StartTripAsync("VAN-INT", EquatorRoute());
        var snapshots = new List<(TelemetryEvent Event, double MileageKm, VehicleStatus VehicleStatus, DeliveryRouteStatus RouteStatus)>();
        world.Publisher.Subscribe(new DelegateObserver(telemetry =>
        {
            // Lo que el observador encuentra en los repositorios EN EL MOMENTO de recibir el evento.
            var storedVehicle = world.Fleet.VehicleRepository.GetByIdAsync(vehicle.Id).GetAwaiter().GetResult()!;
            var storedRoute = world.Fleet.RouteRepository.GetByIdAsync(route.Id).GetAwaiter().GetResult()!;
            snapshots.Add((telemetry, storedVehicle.MileageKm, storedVehicle.Status, storedRoute.Status));
        }));

        await world.AdvanceAsync(steps: 7, SixMinutes);

        foreach (var (telemetry, mileageKm, _, _) in snapshots.Where(s => s.Event is VehicleMovedEvent))
            Assert.Equal(((VehicleMovedEvent)telemetry).MileageKm, mileageKm, Tolerance);   // el movimiento ya estaba guardado
        var arrival = snapshots.Single(s => s.Event is TripArrivedEvent);
        Assert.Equal(DeliveryRouteStatus.Completed, arrival.RouteStatus);                  // la ruta ya estaba completada
        Assert.Equal(VehicleStatus.Available, arrival.VehicleStatus);                      // y el vehículo ya había sido liberado (Mediator + State)
    }

    [Fact]
    public async Task TheAudit_HasOneEntryPerStop_AndTheArrivalIsAuditedOnlyByTheRouteService()
    {
        var world = new TelemetryWorld();
        await world.StartTripAsync("VAN-INT", EquatorRoute());

        await world.AdvanceAsync(steps: 7, SixMinutes);

        var telemetryEntries = world.Fleet.Audit.Entries.Where(entry => entry.Category == TripAuditObserver.AuditCategory).ToList();
        Assert.Equal(2, telemetryEntries.Count);
        Assert.Equal("El vehículo VAN-INT alcanzó la parada 'Primera' (16.7 % del recorrido).", telemetryEntries[0].Message);
        Assert.Equal("El vehículo VAN-INT alcanzó la parada 'Segunda' (50 % del recorrido).", telemetryEntries[1].Message);

        var completions = world.Fleet.Audit.Entries.Where(entry => entry.Category == "Ruta" && entry.Message.EndsWith("cambió su estado a Completed.")).ToList();
        Assert.Single(completions);                                                          // la llegada no se audita dos veces
        Assert.DoesNotContain(world.Fleet.Audit.Entries, entry => entry.Category == VehicleTelemetryPublisher.FailureAuditCategory);
    }

    [Fact]
    public async Task AfterTheArrival_TheVehicleIsAvailable_NothingMoreIsPublished_AndTheBoardKeepsTheLastKnownData()
    {
        var world = new TelemetryWorld();
        var (vehicle, route) = await world.StartTripAsync("VAN-INT", EquatorRoute());
        await world.AdvanceAsync(steps: 7, SixMinutes);
        var publishedAtArrival = world.Recorder.Received.Count;

        await world.AdvanceAsync(steps: 3, SixMinutes);   // la ruta terminó: ya no hay viaje que mover

        Assert.Equal(VehicleStatus.Available, vehicle.Status);
        Assert.Equal(DeliveryRouteStatus.Completed, route.Status);
        Assert.Equal(publishedAtArrival, world.Recorder.Received.Count);
        var status = world.Board.Get(vehicle.Id)!;
        Assert.True(status.Arrived);
        Assert.Equal(100, status.Percent);
        Assert.Equal(0, status.RemainingKm);
        Assert.Equal("Segunda", status.LastStopReached);
        Assert.Equal(RoutePath.DestinationLabel, status.NextStopLabel);
    }

    [Fact]
    public async Task TheBoard_AgreesWithTheProgressQuery_AtEveryStepOfTheTrip()
    {
        var world = new TelemetryWorld();
        var (vehicle, _) = await world.StartTripAsync("VAN-INT", EquatorRoute());
        var progress = new TripProgressService(world.Fleet.RouteRepository, world.Fleet.VehicleRepository, world.Tracker, world.Profiles, world.Options);

        for (var step = 1; step <= 6; step++)
        {
            await world.AdvanceAsync(steps: 1, SixMinutes);

            var live = world.Board.Get(vehicle.Id)!;
            var onDemand = Assert.Single(await progress.GetActiveTripsAsync());
            Assert.Equal(onDemand.Percent, live.Percent);
            Assert.Equal(onDemand.NextStopLabel, live.NextStopLabel);
            Assert.Equal(onDemand.RemainingKm, live.RemainingKm);
            Assert.Equal(onDemand.EtaMinutes, live.EtaMinutes);
            Assert.Equal(onDemand.Latitude, live.Latitude);
            Assert.Equal(onDemand.Longitude, live.Longitude);
            Assert.Equal(onDemand.LicensePlate, live.LicensePlate);
        }
    }

    [Fact]
    public async Task TheTimestamps_ComeFromTheInjectedClock_OnePerStep()
    {
        var world = new TelemetryWorld();
        await world.StartTripAsync("VAN-INT", EquatorRoute());
        var start = world.Clock.GetUtcNow();

        await world.AdvanceAsync(steps: 1, SixMinutes);
        world.Clock.Advance(SixMinutes);
        await world.AdvanceAsync(steps: 1, SixMinutes);   // en este paso se cruza "Primera"

        var firstStep = world.Recorder.Received.Where(telemetry => telemetry.OccurredAt == start).ToList();
        var secondStep = world.Recorder.Received.Where(telemetry => telemetry.OccurredAt == start + SixMinutes).ToList();
        Assert.Equal(new[] { "moved" }, Kinds(firstStep));
        Assert.Equal(new[] { "moved", "stop:Primera" }, Kinds(secondStep));
    }

    // ---------------- Quién publica y quién no ----------------

    [Theory]
    [InlineData(VehicleStatus.Available)]
    [InlineData(VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService)]
    public async Task AVehicleThatIsNotEnRoute_PublishesNothing_EvenIfItsRouteIsActive(VehicleStatus status)
    {
        // STATE × OBSERVER: solo un vehículo En ruta sigue un viaje (CanTrackTrip), así que los demás estados no se mueven ni emiten telemetría.
        var world = new TelemetryWorld();
        var vehicle = await world.Fleet.VehicleRepository.AddAsync(new Vehicle { LicensePlate = "VAN-OFF", Type = VehicleType.Van, Status = status, MileageKm = 1000 });
        var route = EquatorRoute(DeliveryRouteStatus.InProgress, vehicle.Id);
        await world.Fleet.RouteRepository.AddAsync(route);

        await world.AdvanceAsync(steps: 3, SixMinutes);

        Assert.Empty(world.Recorder.Received);
        Assert.Empty(world.Board.GetAll());
        Assert.Equal(1000, vehicle.MileageKm);
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.Planned)]
    [InlineData(DeliveryRouteStatus.Completed)]
    [InlineData(DeliveryRouteStatus.Cancelled)]
    public async Task ARouteThatIsNotActive_PublishesNothing(DeliveryRouteStatus routeStatus)
    {
        var world = new TelemetryWorld();
        var vehicle = await world.Fleet.VehicleRepository.AddAsync(new Vehicle { LicensePlate = "VAN-IDLE", Type = VehicleType.Van, Status = VehicleStatus.EnRoute });
        await world.Fleet.RouteRepository.AddAsync(EquatorRoute(routeStatus, vehicle.Id));

        await world.AdvanceAsync(steps: 2, SixMinutes);

        Assert.Empty(world.Recorder.Received);
    }

    [Fact]
    public async Task TwoTripsAtOnce_ProduceTheirOwnEvents_AndTheBoardListsBothByPlate()
    {
        var world = new TelemetryWorld();
        var (van, _) = await world.StartTripAsync("VAN-INT", EquatorRoute());
        var (car, _) = await world.StartTripAsync("CAR-INT", BentRoute(), VehicleType.Car, latitude: 7.0806, longitude: -73.1716);

        await world.AdvanceAsync(steps: 2, OneMinute);   // la ruta con curvas mide unos 7 km: a 120 km/h, dos minutos son 4 km y aún no llega

        Assert.Equal(2, world.Recorder.Received.OfType<VehicleMovedEvent>().Count(move => move.VehicleId == van.Id));
        Assert.Equal(2, world.Recorder.Received.OfType<VehicleMovedEvent>().Count(move => move.VehicleId == car.Id));
        Assert.Equal(new[] { "CAR-INT", "VAN-INT" }, world.Board.GetAll().Select(status => status.LicensePlate));
        Assert.All(world.Recorder.Received, telemetry => Assert.Contains(telemetry.VehicleId, new[] { van.Id, car.Id }));
    }

    // ---------------- Fallos ----------------

    [Fact]
    public async Task AFailingObserver_DoesNotStopTheSimulation_NorTheOtherObservers()
    {
        var failing = new ThrowingObserver();
        var world = new TelemetryWorld(first: failing);
        var (vehicle, route) = await world.StartTripAsync("VAN-INT", EquatorRoute());

        await world.AdvanceAsync(steps: 7, SixMinutes);   // no debe lanzar

        Assert.Equal(DeliveryRouteStatus.Completed, route.Status);
        Assert.Equal(VehicleStatus.Available, vehicle.Status);
        Assert.Equal(10, failing.Calls);                                             // recibió todos los eventos y falló en cada uno
        Assert.Equal(10, world.Recorder.Received.Count);                              // los demás recibieron lo mismo
        Assert.True(world.Board.Get(vehicle.Id)!.Arrived);
        Assert.Equal(10, world.Fleet.Audit.Entries.Count(entry => entry.Category == VehicleTelemetryPublisher.FailureAuditCategory));
    }

    [Fact]
    public async Task IfTheRouteCannotBeCompleted_NoArrivalIsPublished()
    {
        // El simulador publica al FINAL del paso: si completar la ruta falla, no se anuncia una llegada que no ocurrió.
        var recorder = new RecordingObserver();
        var publisher = new VehicleTelemetryPublisher(new ITelemetryObserver[] { recorder });
        var vehicles = new FleetManagement.Infrastructure.Persistence.InMemoryVehicleRepository();
        var routes = new FleetManagement.Infrastructure.Persistence.InMemoryDeliveryRouteRepository();
        var vehicle = EnRouteVehicle(VehicleType.Van);
        await vehicles.AddAsync(vehicle);
        await routes.AddAsync(EquatorRoute(DeliveryRouteStatus.InProgress, vehicle.Id));
        var simulator = new VehicleMovementSimulator(
            routes, vehicles, new FailingRouteService(), new InMemoryTripProgressTracker(),
            new VehicleTypeProfileFactory(new FleetOnboardingFactoryProvider()), new SimulationOptions(), publisher, new ManualTimeProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(() => simulator.AdvanceAsync(TimeSpan.FromHours(2)));   // de sobra para llegar

        Assert.DoesNotContain(recorder.Received, telemetry => telemetry is TripArrivedEvent);
    }

    // ---------------- Sin publicador, todo sigue igual ----------------

    [Fact]
    public async Task WithoutAPublisher_TheSimulationDoesExactlyWhatItDidBefore()
    {
        var withObservers = new TelemetryWorld();
        var without = new TelemetryWorld(publishing: false);
        var (vehicleWith, routeWith) = await withObservers.StartTripAsync("VAN-INT", EquatorRoute());
        var (vehicleWithout, routeWithout) = await without.StartTripAsync("VAN-INT", EquatorRoute());

        for (var step = 1; step <= 8; step++)
        {
            await withObservers.AdvanceAsync(steps: 1, SixMinutes);
            await without.AdvanceAsync(steps: 1, SixMinutes);

            Assert.Equal(vehicleWithout.CurrentLocation.X, vehicleWith.CurrentLocation.X);
            Assert.Equal(vehicleWithout.CurrentLocation.Y, vehicleWith.CurrentLocation.Y);
            Assert.Equal(vehicleWithout.MileageKm, vehicleWith.MileageKm);
            Assert.Equal(vehicleWithout.Status, vehicleWith.Status);
            Assert.Equal(routeWithout.Status, routeWith.Status);
        }

        Assert.Empty(without.Recorder.Received);
        Assert.Empty(without.Board.GetAll());
        Assert.DoesNotContain(without.Fleet.Audit.Entries, entry => entry.Category == TripAuditObserver.AuditCategory);
        // Salvo las constancias de las paradas, la bitácora de la aplicación es la misma con o sin observadores.
        Assert.Equal(
            without.Fleet.Audit.Entries.Select(entry => (entry.Category, entry.Message)),
            withObservers.Fleet.Audit.Entries.Where(entry => entry.Category != TripAuditObserver.AuditCategory).Select(entry => (entry.Category, entry.Message)));
    }

    /// <summary>Servicio de rutas que no puede completar ninguna ruta.</summary>
    private sealed class FailingRouteService : IDeliveryRouteService
    {
        public Task UpdateRouteStatusAsync(Guid id, string status) => throw new InvalidOperationException("no se pudo completar la ruta");
        public Task<IReadOnlyList<DeliveryRouteDto>> GetAllRoutesAsync() => throw new NotSupportedException();
        public Task<DeliveryRouteDto?> GetRouteByIdAsync(Guid id) => throw new NotSupportedException();
        public Task<IReadOnlyList<DeliveryRouteDto>> GetRoutesByDriverAsync(Guid driverId) => throw new NotSupportedException();
        public Task<DeliveryRouteDto> CreateRouteAsync(CreateDeliveryRouteRequest request) => throw new NotSupportedException();
        public Task<DeliveryRouteDto> CreateExpressRouteAsync(CreateExpressRouteRequest request) => throw new NotSupportedException();
        public Task<DeliveryRouteDto> DuplicateRouteAsync(Guid routeId, DuplicateRouteRequest request) => throw new NotSupportedException();
    }

    /// <summary>
    /// El "mundo" de estas pruebas: la flota real de pruebas (servicios, Mediator, State, bitácora, repositorios en memoria), un
    /// simulador conectado a un publicador con tres observadores (un registro de lo recibido, el tablero y la auditoría) y un reloj manual.
    /// </summary>
    private sealed class TelemetryWorld
    {
        public TelemetryWorld(ITelemetryObserver? first = null, bool publishing = true)
        {
            Profiles = new VehicleTypeProfileFactory(new FleetOnboardingFactoryProvider());
            var observers = new List<ITelemetryObserver>();
            if (first is not null)
                observers.Add(first);
            observers.Add(Recorder);
            observers.Add(Board);
            observers.Add(new TripAuditObserver(Fleet.Audit));
            Publisher = new VehicleTelemetryPublisher(observers, Fleet.Audit);
            Simulator = new VehicleMovementSimulator(
                Fleet.RouteRepository, Fleet.VehicleRepository, Fleet.Routes, Tracker, Profiles, Options,
                publishing ? Publisher : null, Clock);
        }

        public RealFleet Fleet { get; } = new();
        public ManualTimeProvider Clock { get; } = new();
        public RecordingObserver Recorder { get; } = new();
        public LiveTelemetryBoard Board { get; } = new();
        public InMemoryTripProgressTracker Tracker { get; } = new();
        public SimulationOptions Options { get; } = new();
        public IVehicleTypeProfileFactory Profiles { get; }
        public VehicleTelemetryPublisher Publisher { get; }
        public VehicleMovementSimulator Simulator { get; }

        /// <summary>Deja un vehículo disponible con una ruta planificada y la pone en marcha por el servicio real: el Mediator lo deja En ruta.</summary>
        public async Task<(Vehicle Vehicle, DeliveryRoute Route)> StartTripAsync(
            string plate, DeliveryRoute route, VehicleType type = VehicleType.Van, double latitude = 0, double longitude = 0)
        {
            var vehicle = new Vehicle { LicensePlate = plate, Type = type, Status = VehicleStatus.Available, MileageKm = 1000 };
            vehicle.UpdateLocation(latitude, longitude);
            await Fleet.VehicleRepository.AddAsync(vehicle);
            route.Status = DeliveryRouteStatus.Planned;
            route.AssignedVehicleId = vehicle.Id;
            await Fleet.RouteRepository.AddAsync(route);

            await Fleet.Routes.UpdateRouteStatusAsync(route.Id, nameof(DeliveryRouteStatus.InProgress));

            Assert.Equal(VehicleStatus.EnRoute, vehicle.Status);
            return (vehicle, route);
        }

        public async Task AdvanceAsync(int steps, TimeSpan elapsedPerStep)
        {
            for (var step = 0; step < steps; step++)
                await Simulator.AdvanceAsync(elapsedPerStep);
        }
    }
}
