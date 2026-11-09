using FleetManagement.Application.Mediators;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Mediator;

/// <summary>
/// PRUEBAS DEL MEDIADOR SOLO (patrón Mediator): con un participante de vehículos que solo anota y un remitente de rutas
/// que solo contesta, se ve exactamente QUÉ mensajes se cruzan: a quién le pregunta el mediador, a quién le ordena y qué
/// le pasa de uno a otro. No hay repositorios: lo que se prueba es la coordinación, no el guardado.
/// </summary>
public class FleetLifecycleMediatorTests
{
    private readonly RecordingVehicleColleague _vehicles = new();
    private readonly StubRouteColleague _sender = new();
    private readonly Guid _vehicleId = Guid.NewGuid();

    private FleetLifecycleMediator Mediator() => new(_vehicles);

    private DeliveryRoute Route(DeliveryRouteStatus status, bool withVehicle = true)
        => new() { Name = "Ruta de prueba", Status = status, AssignedVehicleId = withVehicle ? _vehicleId : null };

    // ---------- Regla 1: En progreso / Retrasada -> el vehículo queda En ruta ----------

    [Theory]
    [InlineData(DeliveryRouteStatus.InProgress)]
    [InlineData(DeliveryRouteStatus.Delayed)]
    public async Task ARouteThatIsOnTheRoad_MakesTheMediatorOrderTheVehicleEnRoute(DeliveryRouteStatus status)
    {
        await Mediator().RouteStatusChangedAsync(_sender, Route(status));

        var call = Assert.Single(_vehicles.Calls);
        Assert.Equal("MarkEnRoute", call.Kind);
        Assert.Equal(_vehicleId, call.VehicleId);
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.InProgress)]
    [InlineData(DeliveryRouteStatus.Delayed)]
    public async Task ARouteThatIsOnTheRoad_DoesNotNeedToAskTheRouteSideAnything(DeliveryRouteStatus status)
    {
        await Mediator().RouteStatusChangedAsync(_sender, Route(status));

        Assert.Empty(_sender.Questions);
    }

    // ---------- Reglas 2 y 3: Completada / Cancelada -> la pregunta viaja de un participante al otro ----------

    [Theory]
    [InlineData(DeliveryRouteStatus.Completed, true)]
    [InlineData(DeliveryRouteStatus.Completed, false)]
    [InlineData(DeliveryRouteStatus.Cancelled, true)]
    [InlineData(DeliveryRouteStatus.Cancelled, false)]
    public async Task WhenATripEnds_TheMediatorAsksTheRouteSide_AndHandsTheAnswerToTheVehicleSide(DeliveryRouteStatus status, bool answer)
    {
        _sender.AnswerToHasOtherActiveTrip = answer;
        var route = Route(status);

        await Mediator().RouteStatusChangedAsync(_sender, route);

        var question = Assert.Single(_sender.Questions);
        Assert.Equal(_vehicleId, question.VehicleId);
        Assert.Equal(route.Id, question.ExceptRouteId);   // la ruta que acaba de terminar no cuenta como "otro viaje"

        var call = Assert.Single(_vehicles.Calls);
        Assert.Equal("Release", call.Kind);
        Assert.Equal(_vehicleId, call.VehicleId);
        Assert.Equal(answer, call.HasOtherActiveTrip);   // lo mismo que contestó el de rutas, sin tocarlo
    }

    [Fact]
    public async Task EachQuestionGoesToTheParticipantThatNotifiedThatTime_TheMediatorKeepsNoSenderBetweenMessages()
    {
        var first = new StubRouteColleague();
        var second = new StubRouteColleague();
        var mediator = Mediator();

        await mediator.RouteStatusChangedAsync(first, Route(DeliveryRouteStatus.Completed));
        await mediator.RouteStatusChangedAsync(second, Route(DeliveryRouteStatus.Cancelled));

        Assert.Single(first.Questions);
        Assert.Single(second.Questions);
    }

    // ---------- Planificada: no hay nada que coordinar ----------

    [Fact]
    public async Task APlannedRoute_ChangesNothingForTheVehicle()
    {
        await Mediator().RouteStatusChangedAsync(_sender, Route(DeliveryRouteStatus.Planned));

        Assert.Empty(_vehicles.Calls);
        Assert.Empty(_sender.Questions);
    }

    // ---------- Regla 4: ruta sin vehículo asignado ----------

    [Theory]
    [InlineData(DeliveryRouteStatus.Planned)]
    [InlineData(DeliveryRouteStatus.InProgress)]
    [InlineData(DeliveryRouteStatus.Delayed)]
    [InlineData(DeliveryRouteStatus.Completed)]
    [InlineData(DeliveryRouteStatus.Cancelled)]
    public async Task ARouteWithoutAnAssignedVehicle_IsHandledQuietly_NobodyIsOrderedOrAsked(DeliveryRouteStatus status)
    {
        await Mediator().RouteStatusChangedAsync(_sender, Route(status, withVehicle: false));

        Assert.Empty(_vehicles.Calls);
        Assert.Empty(_sender.Questions);
    }

    // ---------- Retraso reportado por una alerta: decisión conservada ----------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RouteDelayReported_DoesNotTouchTheVehicle_AsTheAlertServiceAlwaysDid(bool withVehicle)
    {
        await Mediator().RouteDelayReportedAsync(_sender, Route(DeliveryRouteStatus.Delayed, withVehicle));

        Assert.Empty(_vehicles.Calls);
        Assert.Empty(_sender.Questions);
    }

    // ---------- Mensajes mal formados ----------

    [Fact]
    public async Task AMessageWithoutSenderOrRoute_IsRejected()
    {
        var mediator = Mediator();

        await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.RouteStatusChangedAsync(null!, Route(DeliveryRouteStatus.Completed)));
        await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.RouteStatusChangedAsync(_sender, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.RouteDelayReportedAsync(null!, Route(DeliveryRouteStatus.Delayed)));
        await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.RouteDelayReportedAsync(_sender, null!));
        Assert.Empty(_vehicles.Calls);
    }

    [Fact]
    public void TheMediator_CannotBeBuiltWithoutItsVehicleParticipant()
    {
        Assert.Throws<ArgumentNullException>(() => new FleetLifecycleMediator(null!));
    }
}
