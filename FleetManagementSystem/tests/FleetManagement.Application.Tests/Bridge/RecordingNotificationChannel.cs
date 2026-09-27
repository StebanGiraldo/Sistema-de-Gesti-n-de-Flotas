using FleetManagement.Application.Interfaces;

namespace FleetManagement.Application.Tests.Bridge;

/// <summary>
/// Implementador de prueba (test double escrito a mano, sin librerías de
/// mocking, igual que el resto del proyecto): en vez de escribir en consola,
/// guarda cada envío en memoria para poder hacer aserciones precisas sobre
/// destinatario/asunto/mensaje en las pruebas de <c>FleetNotification</c> y
/// de la integración con los servicios existentes.
/// </summary>
public class RecordingNotificationChannel : INotificationChannel
{
    public record SentMessage(string Recipient, string Subject, string Message);

    private readonly List<SentMessage> _sentMessages = new();

    public IReadOnlyList<SentMessage> SentMessages => _sentMessages;

    public Task SendAsync(string recipient, string subject, string message)
    {
        _sentMessages.Add(new SentMessage(recipient, subject, message));
        return Task.CompletedTask;
    }
}
