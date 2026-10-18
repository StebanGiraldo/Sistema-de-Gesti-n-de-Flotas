namespace FleetManagement.Infrastructure.Navigation;

/// <summary>
/// Almacén de URLs de navegación con vencimiento y tamaño máximo, seguro para varios hilos. Es la pieza que usa
/// <see cref="CachingNavigationProviderProxy"/> para recordar lo que ya pidió al proveedor real.
///
/// Decisiones de diseño:
/// <list type="bullet">
///   <item>Un solo candado (<c>lock</c>) alrededor de un diccionario: las operaciones son de microsegundos y, con a lo sumo
///   unos cientos de entradas, un candado simple es más fácil de razonar que una estructura sin candados.</item>
///   <item>El reloj se inyecta (<see cref="TimeProvider"/>): las pruebas avanzan el tiempo a mano en vez de esperarlo.</item>
///   <item>Una entrada vence cuando <c>ahora &gt;= creación + tiempo de vida</c>. Las vencidas se retiran al consultarlas y
///   cuando hace falta espacio, así que nunca se devuelve una URL vencida.</item>
///   <item>Con el tiempo de vida constante, la entrada que vence primero es la más antigua: al llenarse se descarta esa,
///   que es la que se iba a perder de todos modos.</item>
/// </list>
/// Este tipo no llama al proveedor real: eso lo hace el proxy fuera del candado, para no bloquear a los demás hilos
/// mientras el proveedor responde.
/// </summary>
public sealed class ExpiringUrlCache
{
    private readonly object _gate = new();
    private readonly Dictionary<NavigationCacheKey, Entry> _entries = new();
    private readonly TimeSpan _timeToLive;
    private readonly int _maxEntries;
    private readonly TimeProvider _timeProvider;

    public ExpiringUrlCache(NavigationCacheOptions options, TimeProvider timeProvider)
    {
        _timeToLive = options.TimeToLive;
        _maxEntries = options.MaxEntries;
        _timeProvider = timeProvider;
    }

    /// <summary>Cantidad de entradas guardadas. Puede incluir alguna vencida que todavía no se retiró; nunca supera el máximo.</summary>
    public int Count
    {
        get
        {
            lock (_gate) return _entries.Count;
        }
    }

    public bool TryGet(NavigationCacheKey key, out string url)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                if (!entry.IsExpiredAt(_timeProvider.GetUtcNow()))
                {
                    url = entry.Url;
                    return true;
                }

                _entries.Remove(key);
            }
        }

        url = string.Empty;
        return false;
    }

    /// <summary>Guarda (o renueva) la URL de la clave, con un tiempo de vida completo desde este momento.</summary>
    public void Set(NavigationCacheKey key, string url)
    {
        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            if (!_entries.ContainsKey(key) && _entries.Count >= _maxEntries)
                MakeRoom(now);

            _entries[key] = new Entry(url, now + _timeToLive);
        }
    }

    // Primero se retiran las vencidas (no cuestan nada perderlas); solo si sigue lleno se sacrifica la más antigua.
    private void MakeRoom(DateTimeOffset now)
    {
        foreach (var expiredKey in _entries.Where(pair => pair.Value.IsExpiredAt(now)).Select(pair => pair.Key).ToList())
            _entries.Remove(expiredKey);

        if (_entries.Count >= _maxEntries)
            _entries.Remove(_entries.MinBy(pair => pair.Value.ExpiresAt).Key);
    }

    private readonly record struct Entry(string Url, DateTimeOffset ExpiresAt)
    {
        public bool IsExpiredAt(DateTimeOffset now) => now >= ExpiresAt;
    }
}
