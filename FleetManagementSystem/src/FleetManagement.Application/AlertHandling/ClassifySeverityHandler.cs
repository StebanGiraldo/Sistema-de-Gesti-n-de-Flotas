using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.AlertHandling;

/// <summary>Le pone gravedad a la alerta ya registrada según <see cref="AlertSeverityPolicy"/>. No decide qué se hace con ella: eso es de los manejadores que siguen.</summary>
public sealed class ClassifySeverityHandler : AlertHandler
{
    private readonly AlertSeverityPolicy _policy;

    public ClassifySeverityHandler(AlertSeverityPolicy policy)
    {
        _policy = policy;
    }

    protected override Task ProcessAsync(AlertReportContext context)
    {
        // El servicio que la registró ya validó el tipo, así que aquí siempre se puede leer.
        var type = Enum.Parse<AlertType>(context.Alert.Type, ignoreCase: true);
        var severity = _policy.Classify(type, context.Alert.DelayMinutes);

        context.Classify(severity);
        context.Note($"Clasificada como {severity}");
        return Task.CompletedTask;
    }
}
