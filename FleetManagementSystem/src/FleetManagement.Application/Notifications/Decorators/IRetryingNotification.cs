namespace FleetManagement.Application.Notifications.Decorators;

/// <summary>
/// Capacidad opcional: informa cuántos intentos de envío se hicieron en el
/// último envío y cuántos se permitían. Lo consume el decorador de auditoría
/// para registrar un único evento completo.
/// </summary>
public interface IRetryingNotification
{
    int AttemptsMade { get; }
    int MaxAttempts { get; }
}
