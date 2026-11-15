using FleetManagement.Application.Mediators;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Domain.States;
using Xunit;

namespace FleetManagement.Application.Tests.State;

/// <summary>
/// STATE + MEDIATOR: el patrón State no reemplaza al Mediator ni cambia sus reglas. El Mediator sigue decidiendo CUÁNDO
/// reaccionar a un cambio de ruta (y qué sabe de las demás rutas); el estado del vehículo decide CÓMO reacciona el vehículo.
/// Estas pruebas arman la cadena real —<see cref="RouteColleague"/> → <see cref="FleetLifecycleMediator"/> →
/// <see cref="VehicleColleague"/>— sobre repositorios que entregan copias (como una base de datos real) y comprueban, para cada
/// estado inicial del vehículo y cada cambio de la ruta, el estado que queda GUARDADO.
/// </summary>
public class VehicleStateMediatorCompatibilityTests
{
    private readonly CopyingVehicleRepository _vehicles = new();
    private readonly CopyingRouteRepository _routes = new();

    private IRouteColleague RouteColleague(IVehicleStateResolver? states = null)
        => new RouteColleague(_routes, new FleetLifecycleMediator(new VehicleColleague(_vehicles, states)));

    private async Task<Vehicle> VehicleAsync(VehicleStatus status, string plate = "VAN-001")
        => await _vehicles.AddAsync(new Vehicle { LicensePlate = plate, Type = VehicleType.Van, Status = status });

    private async Task<DeliveryRoute> RouteAsync(Vehicle? vehicle, DeliveryRouteStatus status = DeliveryRouteStatus.Planned, string name = "Ruta de prueba")
        => await _routes.AddAsync(new DeliveryRoute { Name = name, Status = status, AssignedVehicleId = vehicle?.Id });

    private async Task<VehicleStatus> StoredStatusAsync(Vehicle vehicle) => (await _vehicles.GetByIdAsync(vehicle.Id))!.Status;

    // ---------------- Las reglas del Mediator, ahora ejecutadas por el estado ----------------

    [Theory]
    //        estado inicial                 la ruta pasa a                     el vehículo queda
    [InlineData(VehicleStatus.Available,    DeliveryRouteStatus.InProgress,    VehicleStatus.EnRoute)]
    [InlineData(VehicleStatus.EnRoute,      DeliveryRouteStatus.InProgress,    VehicleStatus.EnRoute)]
    [InlineData(VehicleStatus.Maintenance,  DeliveryRouteStatus.InProgress,    VehicleStatus.EnRoute)]   // regla 1 incondicional, tal como era
    [InlineData(VehicleStatus.OutOfService, DeliveryRouteStatus.InProgress,    VehicleStatus.EnRoute)]
    [InlineData(VehicleStatus.Available,    DeliveryRouteStatus.Delayed,       VehicleStatus.EnRoute)]
    [InlineData(VehicleStatus.Maintenance,  DeliveryRouteStatus.Delayed,       VehicleStatus.EnRoute)]
    [InlineData(VehicleStatus.EnRoute,      DeliveryRouteStatus.Completed,     VehicleStatus.Available)] // regla 2: se libera al terminar
    [InlineData(VehicleStatus.EnRoute,      DeliveryRouteStatus.Cancelled,     VehicleStatus.Available)] // regla 2: también al cancelar
    [InlineData(VehicleStatus.Available,    DeliveryRouteStatus.Completed,     VehicleStatus.Available)]
    [InlineData(VehicleStatus.Maintenance,  DeliveryRouteStatus.Completed,     VehicleStatus.Maintenance)]  // regla 3: se conserva
    [InlineData(VehicleStatus.Maintenance,  DeliveryRouteStatus.Cancelled,     VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService, DeliveryRouteStatus.Completed,     VehicleStatus.OutOfService)]
    [InlineData(VehicleStatus.OutOfService, DeliveryRouteStatus.Cancelled,     VehicleStatus.OutOfService)]
    [InlineData(VehicleStatus.Available,    DeliveryRouteStatus.Planned,       VehicleStatus.Available)]    // una ruta planificada no toca al vehículo
    [InlineData(VehicleStatus.EnRoute,      DeliveryRouteStatus.Planned,       VehicleStatus.EnRoute)]
    [InlineData(VehicleStatus.Maintenance,  DeliveryRouteStatus.Planned,       VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService, DeliveryRouteStatus.Planned,       VehicleStatus.OutOfService)]
    public async Task TheStoredVehicleStatus_FollowsTheMediatorRules_ForEveryStartingState(VehicleStatus initial, DeliveryRouteStatus routeStatus, VehicleStatus expected)
    {
        var vehicle = await VehicleAsync(initial);
        var route = await RouteAsync(vehicle);

        await RouteColleague().ChangeStatusAsync(route, routeStatus);

        Assert.Equal(expected, await StoredStatusAsync(vehicle));
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.Completed)]
    [InlineData(DeliveryRouteStatus.Cancelled)]
    public async Task AVehicleStillInAnotherActiveTrip_IsNotReleased_WhenOneOfItsTripsEnds(DeliveryRouteStatus ending)
    {
        var vehicle = await VehicleAsync(VehicleStatus.EnRoute);
        var ending1 = await RouteAsync(vehicle, DeliveryRouteStatus.InProgress, "Ruta que termina");
        await RouteAsync(vehicle, DeliveryRouteStatus.Delayed, "Otra ruta activa");

        await RouteColleague().ChangeStatusAsync(ending1, ending);

        Assert.Equal(VehicleStatus.EnRoute, await StoredStatusAsync(vehicle));   // el Mediator sabe de las demás rutas y el estado lo respeta
    }

    [Fact]
    public async Task AVehicleIsReleased_WhenItsLastActiveTripEnds()
    {
        var vehicle = await VehicleAsync(VehicleStatus.EnRoute);
        var first = await RouteAsync(vehicle, DeliveryRouteStatus.InProgress, "Primera");
        var second = await RouteAsync(vehicle, DeliveryRouteStatus.InProgress, "Segunda");
        var colleague = RouteColleague();

        await colleague.ChangeStatusAsync(first, DeliveryRouteStatus.Completed);
        Assert.Equal(VehicleStatus.EnRoute, await StoredStatusAsync(vehicle));

        await colleague.ChangeStatusAsync(second, DeliveryRouteStatus.Completed);
        Assert.Equal(VehicleStatus.Available, await StoredStatusAsync(vehicle));
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.InProgress)]
    [InlineData(DeliveryRouteStatus.Delayed)]
    [InlineData(DeliveryRouteStatus.Completed)]
    [InlineData(DeliveryRouteStatus.Cancelled)]
    public async Task ARouteWithoutVehicle_StillCoordinatesNothing(DeliveryRouteStatus routeStatus)
    {
        var bystander = await VehicleAsync(VehicleStatus.Available, "VAN-009");
        var route = await RouteAsync(vehicle: null);

        await RouteColleague().ChangeStatusAsync(route, routeStatus);

        Assert.Equal(VehicleStatus.Available, await StoredStatusAsync(bystander));
    }

    [Fact]
    public async Task ARouteThatPointsToAMissingVehicle_IsIgnored_LikeBefore()
    {
        var route = await _routes.AddAsync(new DeliveryRoute { Name = "Huérfana", AssignedVehicleId = Guid.NewGuid() });

        await RouteColleague().ChangeStatusAsync(route, DeliveryRouteStatus.InProgress);   // no debe lanzar

        Assert.Empty(await _vehicles.GetAllAsync());
    }

    [Fact]
    public async Task ADelayReportedByAnAlert_StillDoesNotTouchTheVehicle()
    {
        var vehicle = await VehicleAsync(VehicleStatus.Available);
        var route = await RouteAsync(vehicle, DeliveryRouteStatus.Planned);

        await RouteColleague().ReportDelayAsync(route, 15);

        Assert.Equal(VehicleStatus.Available, await StoredStatusAsync(vehicle));   // decisión del Mediator que el estado no cambia
    }

    // ---------------- El estado es quien decide ----------------

    [Fact]
    public async Task TheVehicleColleague_AsksTheStateOfTheVehicle_ToStartAndToFinishATrip()
    {
        var states = new RecordingResolver();
        var vehicle = await VehicleAsync(VehicleStatus.Available);
        var route = await RouteAsync(vehicle);
        var colleague = RouteColleague(states);

        await colleague.ChangeStatusAsync(route, DeliveryRouteStatus.InProgress);
        await colleague.ChangeStatusAsync(route, DeliveryRouteStatus.Completed);

        // Available (empezar) → EnRoute (terminar): cada decisión la tomó el estado en que estaba el vehículo en ese momento.
        Assert.Equal(new[] { VehicleStatus.Available, VehicleStatus.EnRoute }, states.Requested.Distinct().ToArray());
        Assert.Equal(VehicleStatus.Available, await StoredStatusAsync(vehicle));
    }

    [Fact]
    public async Task ADifferentStateImplementation_ChangesTheOutcome_WithoutTouchingTheMediator()
    {
        // Un resolutor que trata a TODOS los vehículos como si estuvieran fuera de servicio: terminar un viaje ya no los libera.
        var vehicle = await VehicleAsync(VehicleStatus.EnRoute);
        var route = await RouteAsync(vehicle, DeliveryRouteStatus.InProgress);

        await RouteColleague(new AlwaysResolver(VehicleStateResolver.Default.For(VehicleStatus.OutOfService)))
            .ChangeStatusAsync(route, DeliveryRouteStatus.Completed);

        Assert.Equal(VehicleStatus.EnRoute, await StoredStatusAsync(vehicle));
    }

    [Fact]
    public void TheVehicleColleague_StillWorksWithoutAResolver_UsingTheDefaultOne()
    {
        var colleague = new VehicleColleague(_vehicles);

        Assert.NotNull(colleague);
    }

    [Fact]
    public void TheMediatorAndTheColleagues_DoNotDependOnTheConcreteStates()
    {
        // La dependencia es hacia el resolutor (una abstracción); ningún participante del Mediator nombra un estado concreto.
        var concrete = new[] { typeof(AvailableState), typeof(EnRouteState), typeof(MaintenanceState), typeof(OutOfServiceState) };
        var mediatorTypes = new[] { typeof(FleetLifecycleMediator), typeof(RouteColleague), typeof(VehicleColleague) };

        foreach (var mediatorType in mediatorTypes)
        {
            var dependencies = mediatorType.GetConstructors().SelectMany(ctor => ctor.GetParameters()).Select(parameter => parameter.ParameterType);
            Assert.DoesNotContain(dependencies, type => concrete.Contains(type));
        }
    }

    private sealed class RecordingResolver : IVehicleStateResolver
    {
        public List<VehicleStatus> Requested { get; } = new();

        public IVehicleState For(VehicleStatus status)
        {
            Requested.Add(status);
            return VehicleStateResolver.Default.For(status);
        }
    }

    private sealed class AlwaysResolver : IVehicleStateResolver
    {
        private readonly IVehicleState _state;

        public AlwaysResolver(IVehicleState state)
        {
            _state = state;
        }

        public IVehicleState For(VehicleStatus status) => _state;
    }
}
