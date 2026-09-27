using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces;

namespace FleetManagement.Infrastructure.Navigation;

/// <summary>
/// ADAPTER (patrón estructural GoF).
///
/// Traduce la interfaz que espera nuestro sistema (<see cref="INavigationProvider"/>,
/// el TARGET: coordenadas sueltas en grados decimales + <c>WaypointDto</c>) a
/// la interfaz que expone <see cref="GoogleMapsApi"/> (el ADAPTEE: su propio
/// tipo <c>GeoPoint</c> y un método síncrono <c>BuildDirectionsUrl</c>). Es la
/// única clase del sistema que conoce simultáneamente ambas formas.
///
/// Responsabilidad única: SÓLO traduce. No valida reglas de negocio (eso ya
/// lo resolvió <c>NavigationService</c> al obtener la ruta), no persiste
/// nada y no envía notificaciones. Si mañana se necesita integrar otro
/// proveedor (Waze, HERE Maps), basta con crear <c>WazeAdapter</c> /
/// <c>HereMapsAdapter</c> implementando este mismo <see cref="INavigationProvider"/>
/// y cambiar una línea de registro en Dependency Injection; <c>NavigationService</c>
/// no se toca (Open/Closed Principle).
/// </summary>
public class GoogleMapsAdapter : INavigationProvider
{
    private readonly GoogleMapsApi _googleMapsApi;

    public GoogleMapsAdapter(GoogleMapsApi googleMapsApi)
    {
        _googleMapsApi = googleMapsApi;
    }

    public Task<string> GenerateRouteUrlAsync(
        double originLat,
        double originLng,
        double destinationLat,
        double destinationLng,
        IReadOnlyList<WaypointDto> waypoints)
    {
        var origin = new GoogleMapsApi.GeoPoint(originLat, originLng);
        var destination = new GoogleMapsApi.GeoPoint(destinationLat, destinationLng);

        // Traducción de forma: WaypointDto (con Label/Order) -> GeoPoint (sólo
        // coordenadas), y se resuelve aquí el orden que GoogleMapsApi espera
        // recibir ya aplicado, porque el Adaptee no conoce "Order".
        var stopovers = waypoints
            .OrderBy(w => w.Order)
            .Select(w => new GoogleMapsApi.GeoPoint(w.Latitude, w.Longitude))
            .ToArray();

        // Traducción síncrono/asíncrono: GoogleMapsApi.BuildDirectionsUrl es
        // síncrona (arma una URL local, no llama a ninguna red); se envuelve
        // en Task.FromResult para cumplir la firma asíncrona que
        // INavigationProvider expone al resto del sistema.
        var url = _googleMapsApi.BuildDirectionsUrl(origin, destination, stopovers);
        return Task.FromResult(url);
    }
}
