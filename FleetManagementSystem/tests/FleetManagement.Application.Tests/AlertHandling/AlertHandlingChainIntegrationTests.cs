using FleetManagement.Application.AlertHandling;
using FleetManagement.Application.DTOs;
using FleetManagement.Domain.Enums;
using Xunit;
using static FleetManagement.Application.Tests.AlertHandling.AlertHandlingKit;

namespace FleetManagement.Application.Tests.AlertHandling;

/// <summary>
/// La cadena completa de producción con el servicio real de alertas. Cada gravedad llega hasta donde le toca: una alerta
/// leve se registra y queda abierta; una seria sube al supervisor; una crítica sube a la administración. El aviso a
/// operaciones que ya enviaba el servicio de siempre sigue enviándose en todos los casos.
/// </summary>
public class AlertHandlingChainIntegrationTests
{
    private readonly AlertHandlingKit _kit = new();

    private async Task<IReadOnlyList<TripAlertDto>> StoredAlertsAsync() => await _kit.Inner.GetAllAsync();

    // ------------------------------------------------------------------ leve: se registra y queda abierta

    [Fact]
    public async Task AMinorAlert_IsRegisteredAndStaysOpen_WithoutEscalation()
    {
        var route = await _kit.AddRouteAsync();

        var alert = await _kit.ReportAsync(route, "Delay", delayMinutes: 15);

        Assert.Equal("Open", alert.Status);
        Assert.Equal("Delay", alert.Type);
        Assert.Equal(new[] { OperationsDesk }, _kit.Recipients); // solo el aviso de siempre
        Assert.Contains(_kit.Audit.Entries, entry => entry.Category == "Alerta" && entry.Message == $"Alerta {alert.Id} (Delay, gravedad Minor) no requiere escalamiento: queda abierta.");
        Assert.DoesNotContain(_kit.Audit.Entries, entry => entry.Category == "Escalamiento");
    }

    [Fact]
    public async Task EscalatingAnAlert_NeverResolvesIt_NorChangesItsStatus()
    {
        var route = await _kit.AddRouteAsync();

        await _kit.ReportAsync(route, "Delay", delayMinutes: 5);
        await _kit.ReportAsync(route, "Breakdown");
        await _kit.ReportAsync(route, "Accident");

        var stored = await StoredAlertsAsync();
        Assert.Equal(3, stored.Count);
        Assert.All(stored, alert => Assert.Equal("Open", alert.Status));
        Assert.All(stored, alert => Assert.Null(alert.ResolvedAt));
    }

    [Fact]
    public async Task TheExistingBehaviourOfTheAlertService_IsKept_TheRouteDelayIsReflected()
    {
        var route = await _kit.AddRouteAsync();

        await _kit.ReportAsync(route, "Delay", delayMinutes: 25);

        var stored = (await _kit.Routes.GetByIdAsync(route.Id))!;
        Assert.Equal(25, stored.DelayMinutes);
        Assert.Equal(DeliveryRouteStatus.Delayed, stored.Status);
    }

    // ------------------------------------------------------------------ seria: sube al supervisor

    [Fact]
    public async Task ABreakdown_IsSerious_AndGoesUpToTheSupervisor()
    {
        var route = await _kit.AddRouteAsync();

        var alert = await _kit.ReportAsync(route, "Breakdown");

        Assert.Equal(new[] { OperationsDesk, _kit.Options.SupervisorRecipient }, _kit.Recipients);
        Assert.Contains(_kit.Audit.Entries, entry => entry.Category == "Escalamiento" && entry.Message == $"Alerta {alert.Id} (Breakdown, gravedad Serious) escalada a {_kit.Options.SupervisorRecipient}.");
    }

    [Fact]
    public async Task ALongDelay_IsSerious_EvenIfTheTypeIsMild_AndTheEscalationGoesWithHighPriority()
    {
        var route = await _kit.AddRouteAsync();

        await _kit.ReportAsync(route, "Delay", delayMinutes: 90);

        Assert.Equal(new[] { OperationsDesk, _kit.Options.SupervisorRecipient }, _kit.Recipients);
        var audits = _kit.NotificationAudits.ToList();
        Assert.Contains("prioridad Normal", audits[0]); // el aviso de siempre, por el tipo "retraso"
        Assert.Contains("prioridad High", audits[1]);   // el escalamiento, por la gravedad
    }

    [Fact]
    public async Task TheDelayThatMakesAnAlertSerious_ComesFromTheConfiguration()
    {
        _kit.Options.SeriousDelayMinutes = 10;
        _kit.Rebuild();
        var route = await _kit.AddRouteAsync();

        await _kit.ReportAsync(route, "Delay", delayMinutes: 15);

        Assert.Equal(new[] { OperationsDesk, _kit.Options.SupervisorRecipient }, _kit.Recipients);
    }

    // ------------------------------------------------------------------ crítica: sube a la administración

    [Fact]
    public async Task AnAccident_IsCritical_AndGoesUpToTheAdministrator_NotToTheSupervisor()
    {
        var route = await _kit.AddRouteAsync();

        var alert = await _kit.ReportAsync(route, "Accident");

        Assert.Equal(new[] { OperationsDesk, _kit.Options.AdministratorRecipient }, _kit.Recipients);
        Assert.DoesNotContain(_kit.Options.SupervisorRecipient, _kit.Recipients);
        Assert.Contains(_kit.Audit.Entries, entry => entry.Category == "Escalamiento" && entry.Message == $"Alerta {alert.Id} (Accident, gravedad Critical) escalada a {_kit.Options.AdministratorRecipient}.");
        Assert.Contains("prioridad Critical", _kit.NotificationAudits.Last());
    }

    [Fact]
    public async Task TheRecipients_OfEachLevel_ComeFromTheConfiguration()
    {
        _kit.Options.SupervisorRecipient = "turno-noche@fleetcontrol.demo";
        _kit.Options.AdministratorRecipient = "gerencia@fleetcontrol.demo";
        _kit.Rebuild();
        var route = await _kit.AddRouteAsync();

        await _kit.ReportAsync(route, "Breakdown");
        await _kit.ReportAsync(route, "Accident");

        Assert.Equal(new[] { OperationsDesk, "turno-noche@fleetcontrol.demo", OperationsDesk, "gerencia@fleetcontrol.demo" }, _kit.Recipients);
    }

    [Fact]
    public async Task EachAlert_IsEscalatedToOneLevelOnly()
    {
        var route = await _kit.AddRouteAsync();

        await _kit.ReportAsync(route, "Accident");

        Assert.Single(_kit.Audit.Entries, entry => entry.Category == "Escalamiento");
        Assert.Equal(2, _kit.Channel.SentMessages.Count); // operaciones + administración
    }

    // ------------------------------------------------------------------ reportes que se rechazan

    [Fact]
    public async Task ARejectedReport_RegistersNothing_NotifiesNobody_AndChangesNoRoute()
    {
        var route = await _kit.AddRouteAsync();

        await Assert.ThrowsAsync<AlertRejectedException>(() => _kit.ReportAsync(route, "Delay", delayMinutes: 30, description: "   "));

        Assert.Empty(await StoredAlertsAsync());
        Assert.Empty(_kit.Channel.SentMessages);
        Assert.Equal(0, (await _kit.Routes.GetByIdAsync(route.Id))!.DelayMinutes);
    }

    [Fact]
    public async Task AnAbsurdDelay_IsRejected_BeforeItReachesTheRoute()
    {
        var route = await _kit.AddRouteAsync();

        await Assert.ThrowsAsync<AlertRejectedException>(() => _kit.ReportAsync(route, "Delay", delayMinutes: _kit.Options.MaxDelayMinutes + 1));

        Assert.Equal(0, (await _kit.Routes.GetByIdAsync(route.Id))!.DelayMinutes);
        Assert.Empty(await StoredAlertsAsync());
    }

    [Fact]
    public async Task AnUnknownRoute_IsStillNotFound_AndNothingIsEscalated()
    {
        var missing = new CreateTripAlertRequest(Guid.NewGuid(), "Accident", "No existe la ruta", 0);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _kit.Service.CreateAlertAsync(missing));

        Assert.Empty(_kit.Channel.SentMessages);
        Assert.DoesNotContain(_kit.Audit.Entries, entry => entry.Category == "Escalamiento");
    }

    [Fact]
    public async Task AnUnknownType_IsStillAnInvalidArgument_AndNothingIsEscalated()
    {
        var route = await _kit.AddRouteAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _kit.ReportAsync(route, "Meteorito"));

        Assert.Empty(await StoredAlertsAsync());
        Assert.Empty(_kit.Channel.SentMessages);
    }

    // ------------------------------------------------------------------ los pasos, en orden

    [Fact]
    public async Task TheStepsOfAMinorAlert_AreValidateRegisterClassifyAndLeaveOpen()
    {
        var route = await _kit.AddRouteAsync();
        var context = new AlertReportContext(new CreateTripAlertRequest(route.Id, "Delay", "Lluvia", 5));

        await _kit.Chain.HandleAsync(context);

        Assert.Equal(new[] { "Validada", "Registrada", "Clasificada como Minor", "Sin escalamiento" }, context.Steps);
    }

    [Fact]
    public async Task TheStepsOfASeriousAlert_EndInTheSupervisorEscalation()
    {
        var route = await _kit.AddRouteAsync();
        var context = new AlertReportContext(new CreateTripAlertRequest(route.Id, "Breakdown", "Se recalentó el motor", 0));

        await _kit.Chain.HandleAsync(context);

        Assert.Equal(new[] { "Validada", "Registrada", "Clasificada como Serious", $"Escalada a {_kit.Options.SupervisorRecipient} (gravedad Serious)" }, context.Steps);
    }

    [Fact]
    public async Task TheStepsOfACriticalAlert_EndInTheAdministratorEscalation()
    {
        var route = await _kit.AddRouteAsync();
        var context = new AlertReportContext(new CreateTripAlertRequest(route.Id, "Accident", "Choque en la vía", 0));

        await _kit.Chain.HandleAsync(context);

        Assert.Equal(new[] { "Validada", "Registrada", "Clasificada como Critical", $"Escalada a {_kit.Options.AdministratorRecipient} (gravedad Critical)" }, context.Steps);
    }

    // ------------------------------------------------------------------ una falla al avisar no se esconde

    [Fact]
    public async Task WhenTheEscalationCannotBeSent_TheErrorIsNotHidden_AndTheAlertStaysRegistered()
    {
        var failing = new FleetManagement.Application.Tests.Decorator.FlakyNotificationChannel(failuresBeforeSuccess: int.MaxValue);
        var inner = new FleetManagement.Application.Services.TripAlertService(_kit.Alerts, _kit.Routes, _kit.Audit, _kit.Channel);
        var chain = DefaultAlertHandlerChain.Create(_kit.Options, inner, failing, _kit.Audit);
        var service = new AlertHandlingTripAlertService(inner, chain);
        var route = await _kit.AddRouteAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAlertAsync(new CreateTripAlertRequest(route.Id, "Breakdown", "Falla", 0)));

        Assert.Single(await StoredAlertsAsync());   // la alerta ya estaba guardada: no se pierde
        Assert.DoesNotContain(_kit.Audit.Entries, entry => entry.Category == "Escalamiento"); // y no se registra como escalada
    }
}
