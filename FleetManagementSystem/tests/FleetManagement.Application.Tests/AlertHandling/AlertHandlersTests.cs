using FleetManagement.Application.AlertHandling;
using FleetManagement.Application.Notifications;
using FleetManagement.Application.Tests.Bridge;
using FleetManagement.Application.Tests.Decorator;
using Xunit;
using static FleetManagement.Application.Tests.AlertHandling.AlertHandlingTestData;

namespace FleetManagement.Application.Tests.AlertHandling;

/// <summary>Los eslabones que registran, clasifican y escalan, cada uno solo, con su siguiente eslabón de mentira para ver si el reporte pasa o se corta.</summary>
public class AlertHandlersTests
{
    private readonly List<string> _journal = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly RecordingNotificationChannel _channel = new();

    private SpyHandler Follower() => new("siguiente", _journal);

    // ------------------------------------------------------------------ registrar

    [Fact]
    public async Task Register_AsksTheAlertServiceOnce_WithTheOriginalRequest_AndKeepsTheAlertItReturns()
    {
        var alerts = new StubTripAlertService(Alert("Breakdown"));
        var context = Context("Breakdown");

        await AlertHandlerChain.Of(new RegisterAlertHandler(alerts), Follower()).HandleAsync(context);

        Assert.Same(context.Request, Assert.Single(alerts.Requests));
        Assert.Same(alerts.Alert, context.Alert);
        Assert.Equal(new[] { "Registrada" }, context.Steps);
        Assert.Equal(new[] { "siguiente" }, _journal);
    }

    [Fact]
    public async Task Register_WhenTheAlertServiceFails_CutsTheChain_AndLetsTheErrorThrough()
    {
        var alerts = new StubTripAlertService { Failure = new KeyNotFoundException("Ruta no encontrada.") };

        var error = await Assert.ThrowsAsync<KeyNotFoundException>(() => AlertHandlerChain.Of(new RegisterAlertHandler(alerts), Follower()).HandleAsync(Context()));

        Assert.Equal("Ruta no encontrada.", error.Message);
        Assert.Empty(_journal);
    }

    // ------------------------------------------------------------------ clasificar

    [Theory]
    [InlineData("Accident", 0, AlertSeverity.Critical)]
    [InlineData("Breakdown", 0, AlertSeverity.Serious)]
    [InlineData("Delay", 10, AlertSeverity.Minor)]
    [InlineData("Delay", 90, AlertSeverity.Serious)]
    [InlineData("accident", 0, AlertSeverity.Critical)] // el tipo viaja como texto: no importan las mayúsculas
    public async Task Classify_SetsTheSeverityOfTheRegisteredAlert_AndPassesOn(string type, int delay, AlertSeverity expected)
    {
        var context = new AlertReportContext(Request(type, delayMinutes: delay));
        context.Register(Alert(type, delay));

        await AlertHandlerChain.Of(new ClassifySeverityHandler(new AlertSeverityPolicy(new AlertHandlingOptions())), Follower()).HandleAsync(context);

        Assert.Equal(expected, context.Severity);
        Assert.Equal(new[] { $"Clasificada como {expected}" }, context.Steps);
        Assert.Equal(new[] { "siguiente" }, _journal);
    }

    [Fact]
    public async Task Classify_BeforeTheAlertIsRegistered_IsAnAssemblyError_NotASilentDefault()
    {
        var handler = new ClassifySeverityHandler(new AlertSeverityPolicy(new AlertHandlingOptions()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(Context()));
    }

    // ------------------------------------------------------------------ escalar

    private EscalateAlertHandler Escalation(AlertSeverity level, NotificationPriority priority = NotificationPriority.High, string recipient = "jefe@fleetcontrol.demo")
        => new(level, priority, recipient, _channel, _audit);

    [Fact]
    public async Task Escalate_ToItsRecipient_WhenTheSeverityIsItsLevel()
    {
        var context = ClassifiedContext("Breakdown", AlertSeverity.Serious, delayMinutes: 30, routeName: "Ruta Sur");

        await AlertHandlerChain.Of(Escalation(AlertSeverity.Serious, recipient: "supervisor@fleetcontrol.demo"), Follower()).HandleAsync(context);

        var sent = Assert.Single(_channel.SentMessages);
        Assert.Equal("supervisor@fleetcontrol.demo", sent.Recipient);
        Assert.Contains("Ruta Sur", sent.Subject);
        Assert.Contains("'Breakdown'", sent.Message);
        Assert.Contains("Retraso estimado: 30 min", sent.Message);
    }

    [Fact]
    public async Task Escalate_ConsumesTheReport_SoTheChainStopsThere()
    {
        var context = ClassifiedContext("Breakdown", AlertSeverity.Serious);

        await AlertHandlerChain.Of(Escalation(AlertSeverity.Serious), Follower()).HandleAsync(context);

        Assert.Empty(_journal);
    }

    [Theory]
    [InlineData(AlertSeverity.Minor)]
    [InlineData(AlertSeverity.Critical)]
    public async Task Escalate_IgnoresAReportOfAnotherLevel_AndPassesItOn(AlertSeverity other)
    {
        var context = ClassifiedContext("Delay", other);

        await AlertHandlerChain.Of(Escalation(AlertSeverity.Serious), Follower()).HandleAsync(context);

        Assert.Empty(_channel.SentMessages);
        Assert.Equal(new[] { "siguiente" }, _journal);
        Assert.Empty(context.Steps);
    }

    [Theory]
    [InlineData(NotificationPriority.Low)]
    [InlineData(NotificationPriority.High)]
    [InlineData(NotificationPriority.Critical)]
    public async Task Escalate_SendsWithThePriorityItWasGiven_ThroughTheNotificationDecorators(NotificationPriority priority)
    {
        var context = ClassifiedContext("Breakdown", AlertSeverity.Serious);

        await Escalation(AlertSeverity.Serious, priority).HandleAsync(context);

        var audited = Assert.Single(_audit.Entries, entry => entry.Category == "Notificación");
        Assert.Contains($"prioridad {priority}", audited.Message);
        Assert.Contains("TripAlertNotification enviada correctamente", audited.Message);
    }

    [Fact]
    public async Task Escalate_LeavesAnAuditEntry_WithTheAlertTheLevelAndTheRecipient()
    {
        var context = ClassifiedContext("Accident", AlertSeverity.Critical);

        await Escalation(AlertSeverity.Critical, NotificationPriority.Critical, "admin@fleetcontrol.demo").HandleAsync(context);

        var entry = Assert.Single(_audit.Entries, e => e.Category == "Escalamiento");
        Assert.Equal($"Alerta {context.Alert.Id} (Accident, gravedad Critical) escalada a admin@fleetcontrol.demo.", entry.Message);
        Assert.Equal(new[] { "Escalada a admin@fleetcontrol.demo (gravedad Critical)" }, context.Steps);
    }

    [Fact]
    public async Task Escalate_WhenTheChannelKeepsFailing_TheErrorIsNotHidden_AndNothingIsLoggedAsEscalated()
    {
        var failing = new FlakyNotificationChannel(failuresBeforeSuccess: int.MaxValue);
        var handler = new EscalateAlertHandler(AlertSeverity.Serious, NotificationPriority.High, "jefe@fleetcontrol.demo", failing, _audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(ClassifiedContext("Breakdown", AlertSeverity.Serious)));

        Assert.Equal(3, failing.Calls); // prioridad alta = 3 intentos
        Assert.DoesNotContain(_audit.Entries, entry => entry.Category == "Escalamiento");
    }

    [Fact]
    public async Task Escalate_BeforeTheAlertIsClassified_IsAnAssemblyError()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => Escalation(AlertSeverity.Serious).HandleAsync(Context()));

    // ------------------------------------------------------------------ lo que no se escala

    [Fact]
    public async Task Unescalated_LogsThatTheAlertStaysOpen_WithoutNotifyingAnyone()
    {
        var context = ClassifiedContext("Delay", AlertSeverity.Minor);

        await new UnescalatedAlertHandler(_audit).HandleAsync(context);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("Alerta", entry.Category);
        Assert.Equal($"Alerta {context.Alert.Id} (Delay, gravedad Minor) no requiere escalamiento: queda abierta.", entry.Message);
        Assert.Empty(_channel.SentMessages);
        Assert.Equal(new[] { "Sin escalamiento" }, context.Steps);
    }

    [Fact]
    public async Task Unescalated_ConsumesTheReport()
    {
        await AlertHandlerChain.Of(new UnescalatedAlertHandler(_audit), Follower()).HandleAsync(ClassifiedContext("Delay", AlertSeverity.Minor));

        Assert.Empty(_journal);
    }
}
