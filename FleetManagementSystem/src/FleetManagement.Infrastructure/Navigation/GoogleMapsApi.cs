using System.Globalization;

namespace FleetManagement.Infrastructure.Navigation;

/// <summary>
/// ADAPTEE (patrón estructural ADAPTER).
///
/// Representación mínima y simulada del SDK/API externo de Google Maps que
/// el equipo de FleetControl NO controla ni puede modificar. Su interfaz es
/// deliberadamente distinta de la que usa nuestro sistema
/// (<c>INavigationProvider</c>): tiene su propio tipo de coordenada
/// (<see cref="GeoPoint"/>, con <c>Lat</c>/<c>Lng</c> en vez de
/// <c>WaypointDto</c> con <c>Latitude</c>/<c>Longitude</c>/<c>Label</c>/<c>Order</c>),
/// expone un método SÍNCRONO (como suele ocurrir con SDKs que sólo arman una
/// URL localmente, sin llamadas de red) y recibe arreglos en vez de listas ya
/// ordenadas. Traducir esa diferencia de forma es exactamente el trabajo de
/// <c>GoogleMapsAdapter</c>.
///
/// No realiza ninguna llamada de red real: el proyecto funciona con datos
/// simulados (ver README), así que esta clase arma la URL de "Directions" de
/// Google Maps con el mismo formato que usaría la API real
/// (https://developers.google.com/maps/documentation/urls/get-started),
/// reutilizando EXACTAMENTE la misma lógica de construcción de URL que antes
/// vivía embebida dentro de NavigationService.
/// </summary>
public class GoogleMapsApi
{
    /// <summary>Coordenada en el formato propio de esta API externa (simulada).</summary>
    public readonly record struct GeoPoint(double Lat, double Lng);

    /// <summary>
    /// Construye la URL de navegación de Google Maps para el origen, destino
    /// y paradas intermedias dados. <paramref name="stopovers"/> debe venir
    /// ya en el orden en que deben visitarse: esta API, como una API externa
    /// real, no conoce el concepto de "Order" de nuestro dominio.
    /// </summary>
    public string BuildDirectionsUrl(GeoPoint origin, GeoPoint destination, GeoPoint[] stopovers, string travelMode = "driving")
    {
        var originParam = FormatPoint(origin);
        var destinationParam = FormatPoint(destination);

        var url = $"https://www.google.com/maps/dir/?api=1&origin={Uri.EscapeDataString(originParam)}&destination={Uri.EscapeDataString(destinationParam)}&travelmode={travelMode}";

        if (stopovers.Length > 0)
        {
            var waypointsParam = string.Join("|", stopovers.Select(FormatPoint));
            url += $"&waypoints={Uri.EscapeDataString(waypointsParam)}";
        }

        return url;
    }

    private static string FormatPoint(GeoPoint point) =>
        $"{point.Lat.ToString(CultureInfo.InvariantCulture)},{point.Lng.ToString(CultureInfo.InvariantCulture)}";
}
