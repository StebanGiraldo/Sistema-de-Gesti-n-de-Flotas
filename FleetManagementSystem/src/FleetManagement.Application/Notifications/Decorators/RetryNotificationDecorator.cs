namespace FleetManagement.Application.Notifications.Decorators;

/// <summary>
/// DECORATOR concreto: reintenta el envío cuando el canal falla (por ejemplo
/// una pasarela SMS o SMTP con un error transitorio). Si se agotan los
/// intentos, relanza la última excepción: nunca oculta un fallo.
///
/// Si no se indica un número de intentos, lo decide la prioridad de la
/// notificación (si algún decorador de prioridad está dentro de este en la
/// cadena; si no, se trata como Normal): Low = 1, Normal = 2, High = 3,
/// Critical = 5. Mantiene estado del último envío (<see cref="AttemptsMade"/>),
/// por eso se crea una instancia por notificación y no se comparte entre hilos.
/// </summary>
public sealed class RetryNotificationDecorator : NotificationDecorator, IRetryingNotification
{
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(200);

    private readonly int? _configuredMaxAttempts;
    private readonly TimeSpan _delayBetweenAttempts;

    public RetryNotificationDecorator(INotification inner, int? maxAttempts = null, TimeSpan? delayBetweenAttempts = null)
        : base(inner)
    {
        if (maxAttempts is < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Debe haber al menos 1 intento.");
        if (delayBetweenAttempts is { } delay && delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delayBetweenAttempts), delayBetweenAttempts, "La espera no puede ser negativa.");

        _configuredMaxAttempts = maxAttempts;
        _delayBetweenAttempts = delayBetweenAttempts ?? DefaultDelay;
    }

    /// <summary>Intentos realizados en el último envío.</summary>
    public int AttemptsMade { get; private set; }

    /// <summary>Intentos permitidos en el último envío (fijos o derivados de la prioridad).</summary>
    public int MaxAttempts { get; private set; }

    public static int DefaultAttemptsFor(NotificationPriority priority) => priority switch
    {
        NotificationPriority.Low => 1,
        NotificationPriority.Normal => 2,
        NotificationPriority.High => 3,
        NotificationPriority.Critical => 5,
        _ => 2
    };

    public override async Task SendAsync()
    {
        var priority = NotificationChain.Find<IPrioritizedNotification>(Inner)?.Priority ?? NotificationPriority.Normal;
        MaxAttempts = _configuredMaxAttempts ?? DefaultAttemptsFor(priority);
        AttemptsMade = 0;

        while (true)
        {
            AttemptsMade++;
            try
            {
                await Inner.SendAsync();
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && AttemptsMade < MaxAttempts)
            {
                if (_delayBetweenAttempts > TimeSpan.Zero)
                    await Task.Delay(_delayBetweenAttempts);
            }
        }
    }
}
