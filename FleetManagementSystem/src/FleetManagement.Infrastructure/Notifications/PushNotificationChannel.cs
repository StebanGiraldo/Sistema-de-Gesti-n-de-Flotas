using FleetManagement.Application.Interfaces;

namespace FleetManagement.Infrastructure.Notifications;

/// <summary>
/// IMPLEMENTADOR CONCRETO (patrón BRIDGE): simula el envío de una
/// notificación push a la app móvil/portal del operador.
/// </summary>
public class PushNotificationChannel : INotificationChannel
{
    private readonly IFleetAuditLogger _auditLogger;

    public PushNotificationChannel(IFleetAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }

    public Task SendAsync(string recipient, string subject, string message)
    {
        Console.WriteLine($"[PUSH] Dispositivo/usuario: {recipient} | {subject} - {message}");
        _auditLogger.LogEvent("Notificación (Push)", $"'{subject}' enviado a {recipient}.");
        return Task.CompletedTask;
    }
}
