using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Notifications;

namespace FleetManagement.Application.AlertHandling;

/// <summary>
/// La cadena que usa la aplicación, armada en UN solo lugar para que el orden se lea de arriba abajo (y para que las pruebas
/// ejerciten la misma cadena que producción, no una copia):
/// validar → registrar → clasificar → escalar a la administración (críticas) → escalar al supervisor (graves) → dejar abierta (leves).
/// El orden importa: no se puede clasificar lo que no se registró, ni escalar lo que no se clasificó; y el último eslabón
/// recoge lo que ningún otro atendió. Para agregar un paso, se escribe un manejador nuevo y se pone aquí.
/// </summary>
public static class DefaultAlertHandlerChain
{
    public static IAlertHandler Create(
        AlertHandlingOptions options,
        ITripAlertService alerts,
        INotificationChannel channel,
        IFleetAuditLogger audit)
    {
        return AlertHandlerChain.Of(
            new ValidateAlertReportHandler(options),
            new RegisterAlertHandler(alerts),
            new ClassifySeverityHandler(new AlertSeverityPolicy(options)),
            new EscalateAlertHandler(AlertSeverity.Critical, NotificationPriority.Critical, options.AdministratorRecipient, channel, audit),
            new EscalateAlertHandler(AlertSeverity.Serious, NotificationPriority.High, options.SupervisorRecipient, channel, audit),
            new UnescalatedAlertHandler(audit));
    }
}
