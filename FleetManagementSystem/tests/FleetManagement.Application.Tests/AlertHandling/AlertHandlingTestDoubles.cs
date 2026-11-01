using FleetManagement.Application.AlertHandling;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;

namespace FleetManagement.Application.Tests.AlertHandling;

/// <summary>
/// Manejador de prueba (escrito a mano, sin librerías de mocking) que hereda de la base real <see cref="AlertHandler"/>:
/// anota en un diario compartido que fue llamado y deja que la prueba decida si le corresponde el reporte y si lo da por
/// atendido. Sirve para probar el orden de la cadena y cuándo se corta sin depender de ninguna regla de alertas.
/// </summary>
public sealed class SpyHandler : AlertHandler
{
    private readonly string _name;
    private readonly List<string> _journal;
    private readonly bool _canHandle;
    private readonly bool _consumes;

    public SpyHandler(string name, List<string> journal, bool canHandle = true, bool consumes = false)
    {
        _name = name;
        _journal = journal;
        _canHandle = canHandle;
        _consumes = consumes;
    }

    protected override bool CanHandle(AlertReportContext context) => _canHandle;

    protected override bool ConsumesTheReport => _consumes;

    protected override Task ProcessAsync(AlertReportContext context)
    {
        _journal.Add(_name);
        return Task.CompletedTask;
    }
}

/// <summary>Manejador de prueba que falla: para comprobar que una falla corta la cadena y no se esconde.</summary>
public sealed class FailingHandler : AlertHandler
{
    private readonly Exception _failure;

    public FailingHandler(Exception failure)
    {
        _failure = failure;
    }

    protected override Task ProcessAsync(AlertReportContext context) => throw _failure;
}

/// <summary>
/// Servicio de alertas de prueba: devuelve siempre la misma alerta, guarda lo que le pidieron y cuenta cada llamada.
/// Si se le da una falla, la lanza al registrar.
/// </summary>
public sealed class StubTripAlertService : ITripAlertService
{
    public StubTripAlertService(TripAlertDto? alert = null)
    {
        Alert = alert ?? AlertHandlingTestData.Alert("Delay");
    }

    public TripAlertDto Alert { get; }

    public Exception? Failure { get; set; }

    public List<CreateTripAlertRequest> Requests { get; } = new();
    public int GetAllCalls { get; private set; }
    public List<Guid> RoutesAsked { get; } = new();
    public List<Guid> Resolved { get; } = new();

    public Task<TripAlertDto> CreateAlertAsync(CreateTripAlertRequest request)
    {
        Requests.Add(request);
        if (Failure is not null) throw Failure;
        return Task.FromResult(Alert);
    }

    public Task<IReadOnlyList<TripAlertDto>> GetAllAsync()
    {
        GetAllCalls++;
        return Task.FromResult<IReadOnlyList<TripAlertDto>>(new[] { Alert });
    }

    public Task<IReadOnlyList<TripAlertDto>> GetByRouteAsync(Guid routeId)
    {
        RoutesAsked.Add(routeId);
        return Task.FromResult<IReadOnlyList<TripAlertDto>>(new[] { Alert });
    }

    public Task ResolveAlertAsync(Guid id)
    {
        Resolved.Add(id);
        return Task.CompletedTask;
    }
}

public static class AlertHandlingTestData
{
    public static CreateTripAlertRequest Request(string type = "Delay", string description = "Retraso por obras en la vía", int delayMinutes = 10, Guid? routeId = null)
        => new(routeId ?? Guid.NewGuid(), type, description, delayMinutes);

    public static AlertReportContext Context(string type = "Delay", string description = "Retraso por obras en la vía", int delayMinutes = 10)
        => new(Request(type, description, delayMinutes));

    public static TripAlertDto Alert(string type, int delayMinutes = 10, string routeName = "Ruta Norte")
        => new(Guid.NewGuid(), Guid.NewGuid(), routeName, null, null, type, "Descripción de prueba", delayMinutes, "Open", DateTime.UtcNow, null);

    /// <summary>Un contexto ya registrado y clasificado, como le llega a un manejador de escalamiento.</summary>
    public static AlertReportContext ClassifiedContext(string type, AlertSeverity severity, int delayMinutes = 10, string routeName = "Ruta Norte")
    {
        var context = new AlertReportContext(Request(type, delayMinutes: delayMinutes));
        context.Register(Alert(type, delayMinutes, routeName));
        context.Classify(severity);
        return context;
    }
}
