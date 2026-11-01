namespace FleetManagement.Application.AlertHandling;

/// <summary>
/// Primer eslabón: rechaza el reporte que no cumple las reglas, antes de registrar nada. Los límites salen de
/// <see cref="AlertHandlingOptions"/>. El tipo de alerta y la ruta los valida después el servicio que registra la alerta.
/// </summary>
public sealed class ValidateAlertReportHandler : AlertHandler
{
    private readonly AlertHandlingOptions _options;

    public ValidateAlertReportHandler(AlertHandlingOptions options)
    {
        _options = options;
    }

    protected override Task ProcessAsync(AlertReportContext context)
    {
        var request = context.Request;
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new AlertRejectedException("La descripción de la alerta es obligatoria.");
        if (request.Description.Length > _options.MaxDescriptionLength)
            throw new AlertRejectedException($"La descripción no puede superar {_options.MaxDescriptionLength} caracteres (tiene {request.Description.Length}).");
        if (request.DelayMinutes > _options.MaxDelayMinutes)
            throw new AlertRejectedException($"El retraso no puede superar {_options.MaxDelayMinutes} minutos (se indicaron {request.DelayMinutes}).");

        context.Note("Validada");
        return Task.CompletedTask;
    }
}
