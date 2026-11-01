namespace FleetManagement.Application.AlertHandling;

/// <summary>
/// Lo común a todos los manejadores: guardar al sucesor y decidir, en un solo lugar, qué pasa con el reporte. Cada manejador
/// concreto solo dice si le corresponde (<see cref="CanHandle"/>), qué hace (<see cref="ProcessAsync"/>) y si con eso lo da
/// por atendido (<see cref="ConsumesTheReport"/>): si no le corresponde o no lo consume, el reporte sigue a su sucesor.
/// </summary>
public abstract class AlertHandler : IAlertHandler
{
    private IAlertHandler? _next;

    public IAlertHandler SetNext(IAlertHandler next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        return next;
    }

    public async Task HandleAsync(AlertReportContext context)
    {
        if (CanHandle(context))
        {
            await ProcessAsync(context);
            if (ConsumesTheReport)
                return; // atendido: la cadena termina aquí
        }

        if (_next is not null)
            await _next.HandleAsync(context);
    }

    /// <summary>Si este manejador interviene en este reporte. Por defecto, siempre.</summary>
    protected virtual bool CanHandle(AlertReportContext context) => true;

    /// <summary>Si, después de intervenir, el reporte se da por atendido y no pasa al siguiente. Por defecto, no.</summary>
    protected virtual bool ConsumesTheReport => false;

    protected abstract Task ProcessAsync(AlertReportContext context);
}
