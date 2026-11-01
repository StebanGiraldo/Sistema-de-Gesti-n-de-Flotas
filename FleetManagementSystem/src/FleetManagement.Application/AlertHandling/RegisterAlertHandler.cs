using FleetManagement.Application.Interfaces.Services;

namespace FleetManagement.Application.AlertHandling;

/// <summary>
/// Registra la alerta con el servicio de alertas de siempre (el que valida el tipo y la ruta, la guarda, refleja el retraso
/// y envía el aviso a operaciones por el Bridge y los Decorators). Recibe el servicio SIN la cadena: si recibiera el que la
/// contiene, se llamaría a sí misma sin fin.
/// </summary>
public sealed class RegisterAlertHandler : AlertHandler
{
    private readonly ITripAlertService _alerts;

    public RegisterAlertHandler(ITripAlertService alerts)
    {
        _alerts = alerts;
    }

    protected override async Task ProcessAsync(AlertReportContext context)
    {
        context.Register(await _alerts.CreateAlertAsync(context.Request));
        context.Note("Registrada");
    }
}
