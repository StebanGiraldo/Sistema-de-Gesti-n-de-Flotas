namespace FleetManagement.Application.AlertHandling;

/// <summary>Qué tan grave es una alerta. Decide hasta dónde sube dentro de la cadena de manejadores.</summary>
public enum AlertSeverity
{
    Minor,
    Serious,
    Critical
}
