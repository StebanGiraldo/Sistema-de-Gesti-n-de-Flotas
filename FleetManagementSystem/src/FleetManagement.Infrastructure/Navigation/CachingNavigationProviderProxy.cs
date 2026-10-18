using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces;

namespace FleetManagement.Infrastructure.Navigation;

/// <summary>
/// PROXY DE CACHÉ (patrón estructural GoF "Proxy", variante caching proxy).
///
/// Tiene la misma interfaz que el sujeto real (<see cref="INavigationProvider"/>) y se interpone entre el cliente
/// (<c>NavigationService</c>) y el sujeto (<see cref="GoogleMapsAdapter"/>) para CONTROLAR EL ACCESO a él: si la misma ruta ya
/// se pidió hace poco, devuelve la URL guardada y no llega al proveedor. El cliente no se entera: ni
/// <c>NavigationService</c> ni el Adapter ni <c>GoogleMapsApi</c> se modificaron; solo cambió el registro en Program.cs.
///
/// Proxy frente a Decorator: la estructura es parecida (ambos envuelven un <see cref="INavigationProvider"/>), pero la
/// intención es distinta. Los Decorators de este proyecto (notificaciones y reportes) AÑADEN responsabilidades funcionales al
/// objeto envuelto: reintentar, auditar, agregar estadísticas. Este proxy no añade nada al resultado —devuelve exactamente la
/// misma URL que daría el proveedor— sino que decide SI Y CUÁNDO se le llama. Por eso es un Proxy y no un Decorator.
///
/// Compromisos asumidos:
/// <list type="bullet">
///   <item>Si el proveedor falla, la excepción llega al cliente y NO se guarda nada: el siguiente pedido lo vuelve a intentar.</item>
///   <item>Dos pedidos simultáneos de una ruta que aún no está en el caché pueden llegar los dos al proveedor (una vez cada
///   uno). Es aceptable porque generar la URL no tiene efectos secundarios; evitarlo exigiría coordinar los pedidos en
///   vuelo, más complejidad de la que este caso justifica.</item>
///   <item>El caché es de memoria de este proceso: no se comparte entre instancias de la API ni sobrevive a un reinicio.</item>
/// </list>
/// Debe registrarse como Singleton: con otro ciclo de vida cada petición tendría un caché vacío y no serviría de nada.
/// </summary>
public sealed class CachingNavigationProviderProxy : INavigationProvider
{
    private readonly INavigationProvider _subject;
    private readonly ExpiringUrlCache _cache;

    public CachingNavigationProviderProxy(INavigationProvider subject, NavigationCacheOptions options, TimeProvider timeProvider)
    {
        _subject = subject;
        _cache = new ExpiringUrlCache(options, timeProvider);
    }

    public async Task<string> GenerateRouteUrlAsync(
        double originLat,
        double originLng,
        double destinationLat,
        double destinationLng,
        IReadOnlyList<WaypointDto> waypoints)
    {
        var key = NavigationCacheKey.From(originLat, originLng, destinationLat, destinationLng, waypoints);
        if (_cache.TryGet(key, out var cachedUrl))
            return cachedUrl;

        // Se llama al proveedor FUERA del candado del caché: hoy es una operación local, pero un proveedor real sería de
        // red y no debe bloquear las consultas de otras rutas mientras responde.
        var url = await _subject.GenerateRouteUrlAsync(originLat, originLng, destinationLat, destinationLng, waypoints);
        _cache.Set(key, url);
        return url;
    }
}
