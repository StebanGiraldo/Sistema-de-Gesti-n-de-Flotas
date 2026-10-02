using System.Diagnostics;
using FleetManagement.Application.Interfaces;

namespace FleetManagement.Application.Notifications.Decorators;

/// <summary>
/// DECORATOR concreto: registra en la bitácora de auditoría (el Singleton
/// IFleetAuditLogger ya existente) el resultado de enviar la notificación:
/// tipo de notificación, duración, prioridad e intentos si algún decorador de
/// la cadena los aporta, y, si falla, el error (que se relanza). Los canales
/// sólo auditan los envíos exitosos; este decorador deja constancia también
/// de los fallos. Para ver los intentos y la prioridad debe quedar FUERA de
/// los decoradores que los aportan.
/// </summary>
public sealed class AuditNotificationDecorator : NotificationDecorator
{
    private const string AuditCategory = "Notificación";

    private readonly IFleetAuditLogger _auditLogger;

    public AuditNotificationDecorator(INotification inner, IFleetAuditLogger auditLogger) : base(inner)
    {
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public override async Task SendAsync()
    {
        var kind = NotificationChain.Unwrap(Inner).GetType().Name;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await Inner.SendAsync();
            stopwatch.Stop();
            _auditLogger.LogEvent(AuditCategory,
                $"{kind} enviada correctamente en {stopwatch.ElapsedMilliseconds} ms{DescribeChain()}.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _auditLogger.LogEvent(AuditCategory,
                $"{kind} falló tras {stopwatch.ElapsedMilliseconds} ms{DescribeChain()}: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    private string DescribeChain()
    {
        var details = new List<string>();

        var prioritized = NotificationChain.Find<IPrioritizedNotification>(Inner);
        if (prioritized is not null)
            details.Add($"prioridad {prioritized.Priority}");

        var retrying = NotificationChain.Find<IRetryingNotification>(Inner);
        if (retrying is not null)
            details.Add($"{retrying.AttemptsMade} de {retrying.MaxAttempts} intento(s)");

        return details.Count == 0 ? string.Empty : $" ({string.Join(", ", details)})";
    }
}
