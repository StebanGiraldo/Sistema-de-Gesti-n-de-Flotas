using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Domain.States;
using Xunit;

namespace FleetManagement.Application.Tests.State;

/// <summary>
/// Pruebas del patrón STATE en el dominio, sin servicios ni repositorios: qué es cada estado (capacidades), qué hace al
/// empezar o terminar un viaje, a qué estados puede pasar a mano y qué pasa cuando se le pide una transición que no
/// permite. Los valores esperados se escriben a mano en cada fila para que la prueba no repita la lógica que comprueba.
/// </summary>
public class VehicleStateBehaviorTests
{
    private const string Plate = "TST-001";

    private static Vehicle VehicleIn(VehicleStatus status) => new() { LicensePlate = Plate, Status = status };

    private static VehicleStateContext ContextIn(VehicleStatus status) => new(VehicleIn(status));

    // ---------------- Los cuatro estados y su resolución ----------------

    [Theory]
    [InlineData(VehicleStatus.Available, typeof(AvailableState))]
    [InlineData(VehicleStatus.EnRoute, typeof(EnRouteState))]
    [InlineData(VehicleStatus.Maintenance, typeof(MaintenanceState))]
    [InlineData(VehicleStatus.OutOfService, typeof(OutOfServiceState))]
    public void EachStatus_HasItsOwnConcreteState(VehicleStatus status, Type expectedState)
    {
        var state = VehicleStateResolver.Default.For(status);

        Assert.IsType(expectedState, state);
        Assert.Equal(status, state.Status);
    }

    [Fact]
    public void EveryValueOfTheEnum_HasAState_SoNoStatusIsLeftWithoutBehavior()
    {
        foreach (var status in Enum.GetValues<VehicleStatus>())
            Assert.Equal(status, VehicleStateResolver.Default.For(status).Status);
    }

    [Fact]
    public void TheResolver_SharesOneInstancePerStatus_BecauseStatesHoldNoData()
    {
        foreach (var status in Enum.GetValues<VehicleStatus>())
            Assert.Same(VehicleStateResolver.Default.For(status), VehicleStateResolver.Default.For(status));
    }

    [Fact]
    public void AnUnknownStatus_IsRejectedByTheResolver()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => VehicleStateResolver.Default.For((VehicleStatus)99));

        Assert.Equal("status", error.ParamName);
    }

    [Fact]
    public void TheStates_AreImmutable_SoSharingThemBetweenThreadsIsSafe()
    {
        var stateTypes = new[] { typeof(VehicleState), typeof(AvailableState), typeof(EnRouteState), typeof(MaintenanceState), typeof(OutOfServiceState) };
        const System.Reflection.BindingFlags everything =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;

        var mutableFields = stateTypes
            .SelectMany(type => type.GetFields(everything))
            .Where(field => !field.IsInitOnly)
            .Select(field => $"{field.DeclaringType!.Name}.{field.Name}")
            .ToList();

        Assert.Empty(mutableFields);
    }

    [Fact]
    public void TheConcreteStates_AreSealed_AndOnlyTheDomainCanCreateThem()
    {
        var concrete = new[] { typeof(AvailableState), typeof(EnRouteState), typeof(MaintenanceState), typeof(OutOfServiceState) };

        Assert.All(concrete, type => Assert.True(type.IsSealed, $"{type.Name} no es sealed"));
        Assert.All(concrete, type => Assert.Empty(type.GetConstructors())); // constructores internos: se obtienen por el resolver
    }

    // ---------------- Qué permite cada estado ----------------

    [Theory]
    //        estado                      puede operar  sigue un viaje
    [InlineData(VehicleStatus.Available,    true,         false)]
    [InlineData(VehicleStatus.EnRoute,      true,         true)]
    [InlineData(VehicleStatus.Maintenance,  false,        false)]
    [InlineData(VehicleStatus.OutOfService, false,        false)]
    public void EachState_DeclaresWhatItAllows(VehicleStatus status, bool canOperate, bool canTrackTrip)
    {
        var context = ContextIn(status);

        Assert.Equal(canOperate, context.CanOperate);
        Assert.Equal(canTrackTrip, context.CanTrackTrip);
        Assert.Equal(canOperate, context.State.CanOperate);
    }

    [Theory]
    [InlineData(VehicleStatus.Available)]
    [InlineData(VehicleStatus.EnRoute)]
    public void AnOperableVehicle_HasNoReasonToBeBlocked(VehicleStatus status)
    {
        Assert.Null(ContextIn(status).OperationBlockReason());
    }

    [Fact]
    public void AVehicleInMaintenance_IsBlockedWithTheOriginalMessage()
    {
        Assert.Equal("El vehículo TST-001 está en mantenimiento; no se puede usar en una ruta.", ContextIn(VehicleStatus.Maintenance).OperationBlockReason());
    }

    [Fact]
    public void AVehicleOutOfService_IsBlockedWithTheOriginalMessage()
    {
        Assert.Equal("El vehículo TST-001 está fuera de servicio; no se puede usar en una ruta.", ContextIn(VehicleStatus.OutOfService).OperationBlockReason());
    }

    [Fact]
    public void TheBlockReasonUsesThePlateOfThatVehicle()
    {
        var context = new VehicleStateContext(new Vehicle { LicensePlate = "XYZ-987", Status = VehicleStatus.Maintenance });

        Assert.Contains("XYZ-987", context.OperationBlockReason());
    }

    [Fact]
    public void OnlyTheStatesThatCannotOperate_HaveABlockReason()
    {
        foreach (var status in Enum.GetValues<VehicleStatus>())
        {
            var state = VehicleStateResolver.Default.For(status);
            Assert.Equal(!state.CanOperate, state.OperationBlockReason(Plate) is not null);
        }
    }

    // ---------------- Empezar un viaje (regla 1 del Mediator) ----------------

    [Theory]
    [InlineData(VehicleStatus.Available)]
    [InlineData(VehicleStatus.EnRoute)]
    [InlineData(VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService)]
    public void StartingATrip_LeavesTheVehicleEnRoute_FromAnyState(VehicleStatus from)
    {
        var context = ContextIn(from);

        context.StartTrip();

        Assert.Equal(VehicleStatus.EnRoute, context.Status);
        Assert.Equal(VehicleStatus.EnRoute, context.Vehicle.Status);
    }

    // ---------------- Terminar o cancelar un viaje (reglas 2 y 3 del Mediator) ----------------

    [Theory]
    //        estado                      sigue en otro viaje  estado esperado
    [InlineData(VehicleStatus.EnRoute,      false,             VehicleStatus.Available)]
    [InlineData(VehicleStatus.EnRoute,      true,              VehicleStatus.EnRoute)]
    [InlineData(VehicleStatus.Available,    false,             VehicleStatus.Available)]
    [InlineData(VehicleStatus.Available,    true,              VehicleStatus.Available)]
    [InlineData(VehicleStatus.Maintenance,  false,             VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.Maintenance,  true,              VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService, false,             VehicleStatus.OutOfService)]
    [InlineData(VehicleStatus.OutOfService, true,              VehicleStatus.OutOfService)]
    public void FinishingATrip_OnlyReleasesAnEnRouteVehicleThatHasNoOtherActiveTrip(VehicleStatus from, bool hasOtherActiveTrip, VehicleStatus expected)
    {
        var context = ContextIn(from);

        context.FinishTrip(hasOtherActiveTrip);

        Assert.Equal(expected, context.Vehicle.Status);
    }

    // ---------------- Cambio manual de estado: la matriz completa ----------------

    [Theory]
    //        desde                       hacia                       permitido
    [InlineData(VehicleStatus.Available,    VehicleStatus.Available,    true)]
    [InlineData(VehicleStatus.Available,    VehicleStatus.EnRoute,      true)]
    [InlineData(VehicleStatus.Available,    VehicleStatus.Maintenance,  true)]
    [InlineData(VehicleStatus.Available,    VehicleStatus.OutOfService, true)]
    [InlineData(VehicleStatus.EnRoute,      VehicleStatus.Available,    true)]
    [InlineData(VehicleStatus.EnRoute,      VehicleStatus.EnRoute,      true)]
    [InlineData(VehicleStatus.EnRoute,      VehicleStatus.Maintenance,  true)]
    [InlineData(VehicleStatus.EnRoute,      VehicleStatus.OutOfService, true)]
    [InlineData(VehicleStatus.Maintenance,  VehicleStatus.Available,    false)]   // del taller solo se sale registrando el mantenimiento
    [InlineData(VehicleStatus.Maintenance,  VehicleStatus.EnRoute,      false)]
    [InlineData(VehicleStatus.Maintenance,  VehicleStatus.Maintenance,  true)]
    [InlineData(VehicleStatus.Maintenance,  VehicleStatus.OutOfService, true)]
    [InlineData(VehicleStatus.OutOfService, VehicleStatus.Available,    true)]
    [InlineData(VehicleStatus.OutOfService, VehicleStatus.EnRoute,      false)]
    [InlineData(VehicleStatus.OutOfService, VehicleStatus.Maintenance,  true)]
    [InlineData(VehicleStatus.OutOfService, VehicleStatus.OutOfService, true)]
    public void ManualChange_FollowsTheTransitionTable(VehicleStatus from, VehicleStatus to, bool allowed)
    {
        var context = ContextIn(from);

        if (allowed)
        {
            context.ChangeTo(to);
            Assert.Equal(to, context.Vehicle.Status);
        }
        else
        {
            Assert.Throws<InvalidVehicleTransitionException>(() => context.ChangeTo(to));
            Assert.Equal(from, context.Vehicle.Status); // un cambio rechazado no toca al vehículo
        }
    }

    [Fact]
    public void AVehicleInTheWorkshop_MustHaveItsMaintenanceRegisteredBeforeItCanGoEnRoute()
    {
        var context = ContextIn(VehicleStatus.Maintenance);

        Assert.Throws<InvalidVehicleTransitionException>(() => context.ChangeTo(VehicleStatus.EnRoute));
        Assert.Throws<InvalidVehicleTransitionException>(() => context.ChangeTo(VehicleStatus.Available));   // ni siquiera a Disponible a mano

        context.CompleteMaintenance();                                                                      // registrar el mantenimiento lo deja Disponible
        Assert.Equal(VehicleStatus.Available, context.Status);

        context.ChangeTo(VehicleStatus.EnRoute);
        Assert.Equal(VehicleStatus.EnRoute, context.Status);
    }

    [Fact]
    public void AVehicleOutOfService_MustBeMadeAvailableBeforeItCanGoEnRoute()
    {
        var context = ContextIn(VehicleStatus.OutOfService);

        Assert.Throws<InvalidVehicleTransitionException>(() => context.ChangeTo(VehicleStatus.EnRoute));

        context.ChangeTo(VehicleStatus.Available);
        context.ChangeTo(VehicleStatus.EnRoute);
        Assert.Equal(VehicleStatus.EnRoute, context.Status);
    }

    [Fact]
    public void AManualMaintenanceToAvailable_IsRejected_AndSaysThatTheMaintenanceMustBeRegistered()
    {
        var context = ContextIn(VehicleStatus.Maintenance);

        var error = Assert.Throws<InvalidVehicleTransitionException>(() => context.ChangeTo(VehicleStatus.Available));

        Assert.Equal(VehicleStatus.Maintenance, context.Vehicle.Status);   // el rechazo no toca al vehículo
        Assert.Equal(new[] { VehicleStatus.OutOfService }, error.AllowedTargets.ToArray());
        Assert.Equal("Para dejarlo Disponible, registre el mantenimiento del vehículo.", error.Hint);
        Assert.Equal(
            "Transición de estado no permitida: el vehículo TST-001 está en Maintenance y no puede pasar a Available. " +
            "Desde Maintenance solo puede pasar a: OutOfService. Para dejarlo Disponible, registre el mantenimiento del vehículo.",
            error.Message);
    }

    [Fact]
    public void RegisteringTheMaintenance_IsTheOnlyWayOutOfTheWorkshopToAvailable()
    {
        var byHand = ContextIn(VehicleStatus.Maintenance);
        var byRecord = ContextIn(VehicleStatus.Maintenance);

        Assert.Throws<InvalidVehicleTransitionException>(() => byHand.ChangeTo(VehicleStatus.Available));
        byRecord.CompleteMaintenance();

        Assert.Equal(VehicleStatus.Maintenance, byHand.Status);
        Assert.Equal(VehicleStatus.Available, byRecord.Status);
    }

    [Fact]
    public void ARejectedTransition_ExplainsWhatHappenedAndWhatIsAllowed()
    {
        var context = ContextIn(VehicleStatus.Maintenance);

        var error = Assert.Throws<InvalidVehicleTransitionException>(() => context.ChangeTo(VehicleStatus.EnRoute));

        Assert.Equal(Plate, error.LicensePlate);
        Assert.Equal(VehicleStatus.Maintenance, error.From);
        Assert.Equal(VehicleStatus.EnRoute, error.To);
        Assert.Equal(new[] { VehicleStatus.OutOfService }, error.AllowedTargets.ToArray());
        Assert.Equal(
            "Transición de estado no permitida: el vehículo TST-001 está en Maintenance y no puede pasar a EnRoute. " +
            "Desde Maintenance solo puede pasar a: OutOfService. Para dejarlo Disponible, registre el mantenimiento del vehículo.",
            error.Message);
    }

    [Fact]
    public void ARejectedTransitionFromAStateWithoutAnotherWay_AddsNoHint()
    {
        var context = ContextIn(VehicleStatus.OutOfService);

        var error = Assert.Throws<InvalidVehicleTransitionException>(() => context.ChangeTo(VehicleStatus.EnRoute));

        Assert.Null(error.Hint);
        Assert.Equal(
            "Transición de estado no permitida: el vehículo TST-001 está en OutOfService y no puede pasar a EnRoute. " +
            "Desde OutOfService solo puede pasar a: Available, Maintenance.",
            error.Message);
    }

    [Fact]
    public void ATransitionToAnUndefinedStatus_IsRejected_NotStored()
    {
        var context = ContextIn(VehicleStatus.Available);

        Assert.Throws<InvalidVehicleTransitionException>(() => context.ChangeTo((VehicleStatus)99));

        Assert.Equal(VehicleStatus.Available, context.Vehicle.Status);
    }

    [Fact]
    public void TheTransitionError_IsAnInvalidOperationException_LikeTheOtherBusinessRuleViolations()
    {
        var error = new InvalidVehicleTransitionException(Plate, VehicleStatus.Maintenance, VehicleStatus.EnRoute, new[] { VehicleStatus.Available });

        Assert.IsAssignableFrom<InvalidOperationException>(error);
    }

    // ---------------- El contexto ----------------

    [Fact]
    public void TheContext_ReadsTheStatusFromTheVehicle_SoThereIsNoSecondCopyToDesynchronize()
    {
        var vehicle = VehicleIn(VehicleStatus.Available);
        var context = new VehicleStateContext(vehicle);

        vehicle.Status = VehicleStatus.Maintenance;   // alguien cambia la entidad por otro camino (por ejemplo, un repositorio)

        Assert.Equal(VehicleStatus.Maintenance, context.Status);
        Assert.IsType<MaintenanceState>(context.State);
        Assert.False(context.CanOperate);
    }

    [Fact]
    public void TheContext_ChangesOnlyThroughTheStates_NobodyCanBypassTheRules()
    {
        var transition = typeof(VehicleStateContext).GetMethod("TransitionTo", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        Assert.Null(transition); // TransitionTo es internal: solo los estados del dominio deciden una transición
    }

    [Fact]
    public void TheContext_UsesTheResolverItIsGiven()
    {
        var spy = new SpyResolver();
        var context = new VehicleStateContext(VehicleIn(VehicleStatus.Available), spy);

        _ = context.CanOperate;
        context.ChangeTo(VehicleStatus.Maintenance);

        Assert.Contains(VehicleStatus.Available, spy.Requested);
        Assert.Contains(VehicleStatus.Maintenance, spy.Requested);
    }

    [Fact]
    public void TheContext_RequiresAVehicle()
    {
        Assert.Throws<ArgumentNullException>(() => new VehicleStateContext(null!));
    }

    [Fact]
    public void TheEntityIsNotModified_ItStillHasTheSameStatusProperty()
    {
        var property = typeof(Vehicle).GetProperty(nameof(Vehicle.Status));

        Assert.NotNull(property);
        Assert.Equal(typeof(VehicleStatus), property!.PropertyType);
        Assert.Equal(new[] { "Available", "EnRoute", "Maintenance", "OutOfService" }, Enum.GetNames<VehicleStatus>());
    }

    [Fact]
    public void TheQueriesAndTheBehavior_AreSeparateSmallContracts_ThatTheStateInterfaceJoins()
    {
        var queries = typeof(IVehicleStateRules).GetProperties().Select(property => property.Name)
            .Concat(typeof(IVehicleStateRules).GetMethods().Where(method => !method.IsSpecialName).Select(method => method.Name))
            .Order()
            .ToArray();
        var behavior = typeof(IVehicleStateBehavior).GetMethods().Select(method => method.Name).Order().ToArray();

        Assert.Equal(new[] { "CanOperate", "CanTrackTrip", "OperationBlockReason", "Status" }, queries);
        Assert.Equal(new[] { "ChangeTo", "CompleteMaintenance", "FinishTrip", "StartTrip" }, behavior);
        Assert.True(typeof(IVehicleStateRules).IsAssignableFrom(typeof(IVehicleState)));
        Assert.True(typeof(IVehicleStateBehavior).IsAssignableFrom(typeof(IVehicleState)));
        Assert.False(typeof(IVehicleStateRules).IsAssignableFrom(typeof(IVehicleStateBehavior))); // un consumidor de consultas no arrastra el comportamiento
        Assert.True(typeof(IVehicleState).IsAssignableFrom(typeof(VehicleState)));
    }

    private sealed class SpyResolver : IVehicleStateResolver
    {
        public List<VehicleStatus> Requested { get; } = new();

        public IVehicleState For(VehicleStatus status)
        {
            Requested.Add(status);
            return VehicleStateResolver.Default.For(status);
        }
    }
}
