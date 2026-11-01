using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.AlertHandling;

/// <summary>
/// Cuándo una alerta es grave. Un accidente es crítico y una avería, seria; cualquier otra se vuelve seria si el retraso
/// llega al umbral configurado. Se queda con la mayor de las dos gravedades. Es pura: el manejador solo la consulta.
/// </summary>
public sealed class AlertSeverityPolicy
{
    private readonly AlertHandlingOptions _options;

    public AlertSeverityPolicy(AlertHandlingOptions options)
    {
        _options = options;
    }

    public AlertSeverity Classify(AlertType type, int delayMinutes)
    {
        var byType = type switch
        {
            AlertType.Accident => AlertSeverity.Critical,
            AlertType.Breakdown => AlertSeverity.Serious,
            _ => AlertSeverity.Minor
        };
        var byDelay = delayMinutes >= _options.SeriousDelayMinutes ? AlertSeverity.Serious : AlertSeverity.Minor;

        return byType > byDelay ? byType : byDelay;
    }
}
