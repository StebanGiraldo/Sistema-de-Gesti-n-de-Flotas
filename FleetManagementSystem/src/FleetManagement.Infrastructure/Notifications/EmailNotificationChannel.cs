using FleetManagement.Application.Interfaces;

namespace FleetManagement.Infrastructure.Notifications;

/// <summary>
/// IMPLEMENTADOR CONCRETO (patrón BRIDGE): simula el envío por correo
/// electrónico. Como el proyecto trabaja con datos simulados (ver README),
/// el envío se representa con una línea de consola y un registro en el
/// Singleton de auditoría ya existente (<see cref="IFleetAuditLogger"/>), en
/// lugar de integrar un proveedor SMTP real.
/// </summary>
public class EmailNotificationChannel : INotificationChannel
{
    private readonly IFleetAuditLogger _auditLogger;

    public EmailNotificationChannel(IFleetAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }

    public Task SendAsync(string recipient, string subject, string message)
    {
        Console.WriteLine($"[EMAIL] Para: {recipient} | Asunto: {subject}{Environment.NewLine}{message}");
        _auditLogger.LogEvent("Notificación (Email)", $"'{subject}' enviado a {recipient}.");
        return Task.CompletedTask;
    }
}
