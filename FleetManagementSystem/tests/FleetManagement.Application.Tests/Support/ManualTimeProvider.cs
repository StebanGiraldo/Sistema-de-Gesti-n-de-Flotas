namespace FleetManagement.Application.Tests.Support;

/// <summary>
/// Reloj de prueba: la hora solo cambia cuando la prueba la avanza. Así los vencimientos (p. ej., del caché de navegación)
/// se comprueban en el segundo exacto, sin esperar tiempo real ni depender de la velocidad del equipo.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private DateTimeOffset _utcNow;

    public ManualTimeProvider(DateTimeOffset? start = null)
    {
        _utcNow = start ?? new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate) return _utcNow;
    }

    public void Advance(TimeSpan by)
    {
        lock (_gate) _utcNow += by;
    }
}
