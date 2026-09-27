using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces;
using FleetManagement.Infrastructure.Navigation;
using Xunit;

namespace FleetManagement.Application.Tests.Adapter;

/// <summary>
/// PRUEBAS DEL PATRÓN ADAPTER en aislamiento: comprueban que
/// <see cref="GoogleMapsAdapter"/> traduce correctamente la interfaz de
/// nuestro sistema (coordenadas sueltas + <see cref="WaypointDto"/>) a la
/// interfaz del Adaptee (<see cref="GoogleMapsApi"/>: su tipo propio
/// <c>GeoPoint</c> y el método síncrono <c>BuildDirectionsUrl</c>), sin
/// pasar por <c>NavigationService</c> ni por ningún repositorio.
///
/// Las URLs esperadas se calculan a mano (ver comentarios) para verificar
/// carácter por carácter que la traducción preserva EXACTAMENTE el mismo
/// formato que generaba el código original embebido en NavigationService.
/// </summary>
public class GoogleMapsAdapterTests
{
    [Fact]
    public async Task GenerateRouteUrlAsync_WithoutWaypoints_BuildsUrlWithOriginDestinationAndTravelMode()
    {
        var adapter = new GoogleMapsAdapter(new GoogleMapsApi());

        // origin "7.1193,-73.1227" -> escapado: "7.1193%2C-73.1227"
        // destination "7.15,-73.1" -> escapado: "7.15%2C-73.1"
        var url = await adapter.GenerateRouteUrlAsync(7.1193, -73.1227, 7.15, -73.1, new List<WaypointDto>());

        Assert.Equal(
            "https://www.google.com/maps/dir/?api=1&origin=7.1193%2C-73.1227&destination=7.15%2C-73.1&travelmode=driving",
            url);
    }

    [Fact]
    public async Task GenerateRouteUrlAsync_WithWaypoints_OrdersThemByOrder_AndAppendsWaypointsParam()
    {
        var adapter = new GoogleMapsAdapter(new GoogleMapsApi());

        // A propósito, las paradas llegan desordenadas (Order = 1 antes que Order = 0):
        // el Adapter debe reordenarlas antes de traducirlas a GeoPoint.
        var waypoints = new List<WaypointDto>
        {
            new(7.14, -73.14, "Segunda parada", 1),
            new(7.12, -73.12, "Primera parada", 0)
        };

        var url = await adapter.GenerateRouteUrlAsync(7.10, -73.10, 7.20, -73.20, waypoints);

        Assert.Equal(
            "https://www.google.com/maps/dir/?api=1&origin=7.1%2C-73.1&destination=7.2%2C-73.2&travelmode=driving&waypoints=7.12%2C-73.12%7C7.14%2C-73.14",
            url);
    }

    [Fact]
    public async Task GenerateRouteUrlAsync_WithEmptyWaypointList_DoesNotAppendWaypointsParam()
    {
        var adapter = new GoogleMapsAdapter(new GoogleMapsApi());

        var url = await adapter.GenerateRouteUrlAsync(0, 0, 1, 1, new List<WaypointDto>());

        Assert.DoesNotContain("waypoints=", url);
    }

    [Fact]
    public void GoogleMapsAdapter_ImplementsINavigationProvider()
    {
        // Prueba de contrato: el Adapter debe exponer el TARGET del patrón.
        INavigationProvider provider = new GoogleMapsAdapter(new GoogleMapsApi());

        Assert.IsType<GoogleMapsAdapter>(provider);
    }
}
