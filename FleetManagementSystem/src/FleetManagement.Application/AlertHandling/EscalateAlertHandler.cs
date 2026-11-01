using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Notifications;
using FleetManagement.Application.Notifications.Decorators;

namespace FleetManagement.Application.AlertHandling;

/// <summary>
/// Atiende las alertas de UNA gravedad: avisa al destinatario por el Bridge (con los Decorators de prioridad, reintento y
/// auditoría que ya existen) y lo deja en la bitácora. Una sola clase sirve para todos los niveles: cada nivel es una
/// instancia con su gravedad, su prioridad y su destinatario, armada en <c>Program.cs</c>; agregar un nivel no toca esta
/// clase. Las alertas de otra gravedad pasan al siguiente eslabón.
/// </summary>
public sealed class EscalateAlertHandler : AlertHandler
{
    private const string AuditCategory = "Escalamiento";

    private readonly AlertSeverity _level;
    private readonly NotificationPriority _priority;
    private readonly string _recipient;
    private readonly INotificationChannel _channel;
    private readonly IFleetAuditLogger _audit;

    public EscalateAlertHandler(AlertSeverity level, NotificationPriority priority, string recipient, INotificationChannel channel, IFleetAuditLogger audit)
    {
        _level = level;
        _priority = priority;
        _recipient = recipient;
        _channel = channel;
        _audit = audit;
    }

    protected override bool CanHandle(AlertReportContext context) => context.Severity == _level;

    protected override bool ConsumesTheReport => true;

    protected override async Task ProcessAsync(AlertReportContext context)
    {
        var alert = context.Alert;
        var notification = new TripAlertNotification(_channel, _recipient, alert.RouteName, alert.Type, alert.Description, alert.DelayMinutes);

        await notification
            .WithPriority(_priority)
            .WithRetry()
            .WithAudit(_audit)
            .SendAsync();

        _audit.LogEvent(AuditCategory, $"Alerta {alert.Id} ({alert.Type}, gravedad {_level}) escalada a {_recipient}.");
        context.Note($"Escalada a {_recipient} (gravedad {_level})");
    }
}
