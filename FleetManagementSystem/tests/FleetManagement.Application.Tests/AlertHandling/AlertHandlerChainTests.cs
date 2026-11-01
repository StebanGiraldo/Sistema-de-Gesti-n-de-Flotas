using FleetManagement.Application.AlertHandling;
using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Tests.Bridge;
using FleetManagement.Application.Tests.Decorator;
using Xunit;
using static FleetManagement.Application.Tests.AlertHandling.AlertHandlingTestData;

namespace FleetManagement.Application.Tests.AlertHandling;

/// <summary>
/// El mecanismo de la cadena de responsabilidad, sin ninguna regla de alertas: la base común de los manejadores
/// (<see cref="AlertHandler"/>) y el armado en orden (<see cref="AlertHandlerChain"/>). Qué hace cada manejador real se
/// prueba aparte; aquí se prueba cuándo un manejador interviene, cuándo se corta la cadena y que se puede ampliar.
/// </summary>
public class AlertHandlerChainTests
{
    private readonly List<string> _journal = new();

    // ------------------------------------------------------------------ cuándo interviene un manejador y cuándo se corta

    [Fact]
    public async Task AHandlerThatCannotHandleTheReport_PassesItOn_WithoutProcessingIt()
    {
        var chain = AlertHandlerChain.Of(
            new SpyHandler("A", _journal, canHandle: false),
            new SpyHandler("B", _journal));

        await chain.HandleAsync(Context());

        Assert.Equal(new[] { "B" }, _journal);
    }

    [Fact]
    public async Task AHandlerThatHandlesButDoesNotConsume_ProcessesTheReport_AndPassesItOn()
    {
        var chain = AlertHandlerChain.Of(
            new SpyHandler("A", _journal, consumes: false),
            new SpyHandler("B", _journal));

        await chain.HandleAsync(Context());

        Assert.Equal(new[] { "A", "B" }, _journal);
    }

    [Fact]
    public async Task AHandlerThatConsumesTheReport_CutsTheChain()
    {
        var chain = AlertHandlerChain.Of(
            new SpyHandler("A", _journal),
            new SpyHandler("B", _journal, consumes: true),
            new SpyHandler("C", _journal));

        await chain.HandleAsync(Context());

        Assert.Equal(new[] { "A", "B" }, _journal); // C nunca la vio
    }

    [Fact]
    public async Task AHandlerThatCannotHandle_NeverCutsTheChain_EvenIfItWouldConsume()
    {
        var chain = AlertHandlerChain.Of(
            new SpyHandler("A", _journal, canHandle: false, consumes: true),
            new SpyHandler("B", _journal));

        await chain.HandleAsync(Context());

        Assert.Equal(new[] { "B" }, _journal);
    }

    [Fact]
    public async Task TheLastHandler_WithNoSuccessor_JustFinishes()
    {
        var chain = AlertHandlerChain.Of(new SpyHandler("A", _journal));

        await chain.HandleAsync(Context());

        Assert.Equal(new[] { "A" }, _journal);
    }

    [Fact]
    public async Task AReportNobodyCanHandle_GoesThroughTheWholeChain_AndNothingHappens()
    {
        var chain = AlertHandlerChain.Of(
            new SpyHandler("A", _journal, canHandle: false),
            new SpyHandler("B", _journal, canHandle: false));

        await chain.HandleAsync(Context());

        Assert.Empty(_journal);
    }

    [Fact]
    public async Task AFailureInAHandler_CutsTheChain_AndIsNotHidden()
    {
        var chain = AlertHandlerChain.Of(
            new SpyHandler("A", _journal),
            new FailingHandler(new InvalidOperationException("falla de B")),
            new SpyHandler("C", _journal));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => chain.HandleAsync(Context()));

        Assert.Equal("falla de B", error.Message);
        Assert.Equal(new[] { "A" }, _journal);
    }

    // ------------------------------------------------------------------ armado de la cadena

    [Fact]
    public async Task Of_LinksTheHandlersInTheGivenOrder()
    {
        var chain = AlertHandlerChain.Of(
            new SpyHandler("1", _journal),
            new SpyHandler("2", _journal),
            new SpyHandler("3", _journal),
            new SpyHandler("4", _journal));

        await chain.HandleAsync(Context());

        Assert.Equal(new[] { "1", "2", "3", "4" }, _journal);
    }

    [Fact]
    public void Of_ReturnsTheFirstHandler()
    {
        var first = new SpyHandler("A", _journal);

        Assert.Same(first, AlertHandlerChain.Of(first, new SpyHandler("B", _journal)));
    }

    [Fact]
    public void Of_RejectsAMissingFirstHandler()
        => Assert.Throws<ArgumentNullException>(() => AlertHandlerChain.Of(null!));

    [Fact]
    public void SetNext_ReturnsTheSuccessor_SoCallsCanBeChained()
    {
        var first = new SpyHandler("A", _journal);
        var second = new SpyHandler("B", _journal);

        Assert.Same(second, first.SetNext(second));
    }

    [Fact]
    public void SetNext_RejectsAMissingSuccessor()
        => Assert.Throws<ArgumentNullException>(() => new SpyHandler("A", _journal).SetNext(null!));

    [Fact]
    public async Task ReplacingTheSuccessor_ChangesWhereTheReportGoes()
    {
        var first = new SpyHandler("A", _journal);
        first.SetNext(new SpyHandler("viejo", _journal));
        first.SetNext(new SpyHandler("nuevo", _journal));

        await first.HandleAsync(Context());

        Assert.Equal(new[] { "A", "nuevo" }, _journal);
    }

    // ------------------------------------------------------------------ ampliar la cadena sin tocar nada de lo existente

    /// <summary>Un paso nuevo que se le ocurre a alguien mañana: se escribe una clase y se pone en la cadena. Nada más cambia.</summary>
    private sealed class TagHandler : AlertHandler
    {
        protected override Task ProcessAsync(AlertReportContext context)
        {
            context.Note("Etiquetada");
            return Task.CompletedTask;
        }
    }

    private static IAlertHandler[] RealHandlers(AlertHandlingOptions options, RecordingNotificationChannel channel, IFleetAuditLogger audit, StubTripAlertService alerts)
        => new IAlertHandler[]
        {
            new ValidateAlertReportHandler(options),
            new RegisterAlertHandler(alerts),
            new ClassifySeverityHandler(new AlertSeverityPolicy(options)),
            new EscalateAlertHandler(AlertSeverity.Critical, FleetManagement.Application.Notifications.NotificationPriority.Critical, options.AdministratorRecipient, channel, audit),
            new UnescalatedAlertHandler(audit)
        };

    [Fact]
    public async Task ANewHandler_CanBeInsertedBetweenTheExistingOnes_WithoutChangingThem()
    {
        var options = new AlertHandlingOptions();
        var alerts = new StubTripAlertService(Alert("Accident"));
        var handlers = RealHandlers(options, new RecordingNotificationChannel(), new RecordingAuditLogger(), alerts).ToList();
        handlers.Insert(3, new TagHandler()); // entre clasificar y escalar
        var chain = AlertHandlerChain.Of(handlers[0], handlers.Skip(1).ToArray());
        var context = Context("Accident");

        await chain.HandleAsync(context);

        Assert.Equal(
            new[] { "Validada", "Registrada", "Clasificada como Critical", "Etiquetada", $"Escalada a {options.AdministratorRecipient} (gravedad Critical)" },
            context.Steps);
    }

    [Fact]
    public async Task TheSameHandlers_WithoutTheNewOne_BehaveExactlyAsBefore()
    {
        var options = new AlertHandlingOptions();
        var alerts = new StubTripAlertService(Alert("Accident"));
        var handlers = RealHandlers(options, new RecordingNotificationChannel(), new RecordingAuditLogger(), alerts);
        var chain = AlertHandlerChain.Of(handlers[0], handlers.Skip(1).ToArray());
        var context = Context("Accident");

        await chain.HandleAsync(context);

        Assert.Equal(
            new[] { "Validada", "Registrada", "Clasificada como Critical", $"Escalada a {options.AdministratorRecipient} (gravedad Critical)" },
            context.Steps);
    }
}
