using FleetManagement.Application.Interfaces;

namespace FleetManagement.Infrastructure.Notifications;

/// <summary>
/// IMPLEMENTADOR CONCRETO (patrón BRIDGE): simula el envío por SMS. Aplica
/// una restricción propia de este canal (longitud máxima del mensaje) que
/// ni <see cref="INotificationChannel"/> ni la Abstracción necesitan
/// conocer: eso es exactamente lo que Bridge permite, cada implementador
/// puede tener su propia lógica de entrega sin afectar a las notificaciones
/// que lo usan.
/// </summary>
public class SmsNotificationChannel : INotificationChannel
{
    private const int MaxSmsLength = 160;

    private readonly IFleetAuditLogger _auditLogger;

    public SmsNotificationChannel(IFleetAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }

    public Task SendAsync(string recipient, string subject, string message)
    {
        var text = $"{subject}: {message}";
        if (text.Length > MaxSmsLength)
        {
            text = text[..(MaxSmsLength - 1)] + "…";
        }

        Console.WriteLine($"[SMS] Para: {recipient} | {text}");
        _auditLogger.LogEvent("Notificación (SMS)", $"'{subject}' enviado a {recipient}.");
        return Task.CompletedTask;
    }
}
