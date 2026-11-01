using FleetManagement.Application.AlertHandling;
using Xunit;
using static FleetManagement.Application.Tests.AlertHandling.AlertHandlingTestData;

namespace FleetManagement.Application.Tests.AlertHandling;

/// <summary>Lo que va de mano en mano por la cadena: cada manejador solo agrega su parte, y pedir lo que aún no existe es un error de armado.</summary>
public class AlertReportContextTests
{
    [Fact]
    public void ItKeepsTheRequestItWasBuiltWith()
    {
        var request = Request("Breakdown");

        Assert.Same(request, new AlertReportContext(request).Request);
    }

    [Fact]
    public void ItRejectsAMissingRequest()
        => Assert.Throws<ArgumentNullException>(() => new AlertReportContext(null!));

    [Fact]
    public void TheAlert_BeforeItIsRegistered_ExplainsWhichHandlerIsMissing()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Context().Alert);

        Assert.Contains("todavía no se registró", error.Message);
    }

    [Fact]
    public void TheSeverity_BeforeItIsClassified_ExplainsWhichHandlerIsMissing()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Context().Severity);

        Assert.Contains("todavía no se clasificó", error.Message);
    }

    [Fact]
    public void AfterRegisteringAndClassifying_BothCanBeRead()
    {
        var context = Context();
        var alert = Alert("Delay");

        context.Register(alert);
        context.Classify(AlertSeverity.Serious);

        Assert.Same(alert, context.Alert);
        Assert.Equal(AlertSeverity.Serious, context.Severity);
    }

    [Fact]
    public void TheSteps_KeepTheOrderTheyWereNoted()
    {
        var context = Context();

        context.Note("primero");
        context.Note("segundo");

        Assert.Equal(new[] { "primero", "segundo" }, context.Steps);
    }

    [Fact]
    public void ANewContext_HasNoSteps()
        => Assert.Empty(Context().Steps);
}
