using FleetManagement.Application.AlertHandling;
using FleetManagement.Application.Interfaces.Services;
using Xunit;
using static FleetManagement.Application.Tests.AlertHandling.AlertHandlingTestData;

namespace FleetManagement.Application.Tests.AlertHandling;

/// <summary>El decorador que pone la cadena delante del servicio de alertas: reportar pasa por la cadena y todo lo demás se delega tal cual.</summary>
public class AlertHandlingTripAlertServiceTests
{
    private readonly List<string> _journal = new();
    private readonly StubTripAlertService _inner = new();

    private AlertHandlingTripAlertService ServiceWith(IAlertHandler chain) => new(_inner, chain);

    /// <summary>Un eslabón que hace de "registrar": deja la alerta en el contexto, como lo hace el real.</summary>
    private sealed class RegisteringHandler : AlertHandler
    {
        private readonly FleetManagement.Application.DTOs.TripAlertDto _alert;

        public RegisteringHandler(FleetManagement.Application.DTOs.TripAlertDto alert)
        {
            _alert = alert;
        }

        protected override Task ProcessAsync(AlertReportContext context)
        {
            context.Register(_alert);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Reporting_GoesThroughTheChain_AndReturnsTheAlertTheChainRegistered()
    {
        var registered = Alert("Breakdown", routeName: "Ruta Sur");
        var chain = AlertHandlerChain.Of(new SpyHandler("antes", _journal), new RegisteringHandler(registered), new SpyHandler("después", _journal));

        var returned = await ServiceWith(chain).CreateAlertAsync(Request("Breakdown"));

        Assert.Same(registered, returned);
        Assert.Equal(new[] { "antes", "después" }, _journal);
        Assert.Empty(_inner.Requests); // el servicio de verdad solo lo llama el eslabón que registra, no el decorador por su cuenta
    }

    [Fact]
    public async Task TheChain_ReceivesTheRequestTheClientSent()
    {
        var request = Request("Delay", "Obras", 20);
        AlertReportContext? seen = null;
        var chain = AlertHandlerChain.Of(new CaptureHandler(context => seen = context), new RegisteringHandler(Alert("Delay")));

        await ServiceWith(chain).CreateAlertAsync(request);

        Assert.Same(request, seen!.Request);
    }

    private sealed class CaptureHandler : AlertHandler
    {
        private readonly Action<AlertReportContext> _capture;

        public CaptureHandler(Action<AlertReportContext> capture)
        {
            _capture = capture;
        }

        protected override Task ProcessAsync(AlertReportContext context)
        {
            _capture(context);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task AFailureInTheChain_ReachesTheCaller()
    {
        var chain = AlertHandlerChain.Of(new FailingHandler(new AlertRejectedException("no cumple")));

        var error = await Assert.ThrowsAsync<AlertRejectedException>(() => ServiceWith(chain).CreateAlertAsync(Request()));

        Assert.Equal("no cumple", error.Message);
    }

    [Fact]
    public async Task AChainThatNeverRegistersTheAlert_IsAnAssemblyError_NotANullResult()
    {
        var chain = AlertHandlerChain.Of(new SpyHandler("solo", _journal));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ServiceWith(chain).CreateAlertAsync(Request()));

        Assert.Contains("todavía no se registró", error.Message);
    }

    [Fact]
    public async Task EverythingElse_IsDelegatedUntouched_WithoutPassingThroughTheChain()
    {
        var chain = AlertHandlerChain.Of(new SpyHandler("no debería llamarse", _journal));
        ITripAlertService service = ServiceWith(chain);
        var routeId = Guid.NewGuid();
        var alertId = Guid.NewGuid();

        var all = await service.GetAllAsync();
        var byRoute = await service.GetByRouteAsync(routeId);
        await service.ResolveAlertAsync(alertId);

        Assert.Same(_inner.Alert, Assert.Single(all));
        Assert.Same(_inner.Alert, Assert.Single(byRoute));
        Assert.Equal(1, _inner.GetAllCalls);
        Assert.Equal(new[] { routeId }, _inner.RoutesAsked);
        Assert.Equal(new[] { alertId }, _inner.Resolved);
        Assert.Empty(_journal);
    }
}
