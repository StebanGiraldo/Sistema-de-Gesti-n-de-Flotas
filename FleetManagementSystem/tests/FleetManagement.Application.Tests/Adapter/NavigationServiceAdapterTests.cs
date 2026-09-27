using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Services;
using FleetManagement.Domain.Entities;
using FleetManagement.Infrastructure.Navigation;
using FleetManagement.Infrastructure.Persistence;
using NetTopologySuite.Geometries;
using Xunit;

namespace FleetManagement.Application.Tests.Adapter;

/// <summary>
/// PRUEBAS DEL PATRÓN ADAPTER a través del CLIENTE (<see cref="NavigationService"/>).
///
/// Demuestran, con dos proveedores distintos, que NavigationService depende
/// únicamente de <see cref="INavigationProvider"/> (el TARGET) y no de
/// Google Maps directamente:
///  1) Con un proveedor de prueba (<see cref="FakeNavigationProvider"/>, que
///     no sabe nada de Google Maps) para probar que el Cliente funciona
///     igual sin importar qué implementación concreta reciba.
///  2) Con el Adapter real (<see cref="GoogleMapsAdapter"/> +
///     <see cref="GoogleMapsApi"/>) para comprobar que la funcionalidad
///     original (enlace de Google Maps) se preserva exactamente.
/// </summary>
public class NavigationServiceAdapterTests
{
    /// <summary>
    /// Test double escrito a mano (sin librerías de mocking, igual que el
    /// resto del proyecto): implementa INavigationProvider sin tocar Google
    /// Maps en absoluto, para demostrar que NavigationService no depende de
    /// una implementación concreta.
    /// </summary>
    private class FakeNavigationProvider : INavigationProvider
    {
        public int CallCount { get; private set; }
        public IReadOnlyList<WaypointDto>? LastWaypoints { get; private set; }

        public Task<string> GenerateRouteUrlAsync(double originLat, double originLng, double destinationLat, double destinationLng, IReadOnlyList<WaypointDto> waypoints)
        {
            CallCount++;
            LastWaypoints = waypoints;
            return Task.FromResult("https://fake-provider.test/route");
        }
    }

    private static async Task<Guid> SeedRouteAsync(InMemoryDeliveryRouteRepository repository)
    {
        var route = new DeliveryRoute
        {
            Name = "Ruta de prueba",
            Origin = new Point(-73.1227, 7.1193) { SRID = 4326 },
            Destination = new Point(-73.1000, 7.1500) { SRID = 4326 }
        };
        route.Waypoints.Add(new Waypoint { Order = 1, Label = "Segunda parada", Location = new Point(-73.11, 7.13) { SRID = 4326 } });
        route.Waypoints.Add(new Waypoint { Order = 0, Label = "Primera parada", Location = new Point(-73.12, 7.12) { SRID = 4326 } });

        await repository.AddAsync(route);
        return route.Id;
    }

    [Fact]
    public async Task GenerateExternalNavigationLinkAsync_DelegatesToInjectedProvider_NotToAnyConcreteImplementation()
    {
        var routeRepository = new InMemoryDeliveryRouteRepository();
        var routeId = await SeedRouteAsync(routeRepository);
        var fakeProvider = new FakeNavigationProvider();
        var service = new NavigationService(routeRepository, fakeProvider);

        var link = await service.GenerateExternalNavigationLinkAsync(routeId);

        // NavigationService compiló y funcionó sin ninguna referencia a
        // GoogleMapsAdapter/GoogleMapsApi: sólo conoce INavigationProvider.
        Assert.Equal(1, fakeProvider.CallCount);
        Assert.Equal("https://fake-provider.test/route", link.ExternalMapsUrl);
    }

    [Fact]
    public async Task GenerateExternalNavigationLinkAsync_OrdersWaypointsBeforeDelegatingToProvider()
    {
        var routeRepository = new InMemoryDeliveryRouteRepository();
        var routeId = await SeedRouteAsync(routeRepository);
        var fakeProvider = new FakeNavigationProvider();
        var service = new NavigationService(routeRepository, fakeProvider);

        var link = await service.GenerateExternalNavigationLinkAsync(routeId);

        Assert.NotNull(fakeProvider.LastWaypoints);
        Assert.Equal(2, fakeProvider.LastWaypoints!.Count);
        Assert.Equal("Primera parada", fakeProvider.LastWaypoints![0].Label);
        Assert.Equal("Segunda parada", fakeProvider.LastWaypoints![1].Label);
        Assert.Equal(2, link.OrderedStops.Count);
        Assert.Equal("Primera parada", link.OrderedStops[0].Label);
    }

    [Fact]
    public async Task GenerateExternalNavigationLinkAsync_WithNonexistentRoute_ThrowsKeyNotFoundException()
    {
        var routeRepository = new InMemoryDeliveryRouteRepository();
        var service = new NavigationService(routeRepository, new FakeNavigationProvider());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GenerateExternalNavigationLinkAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task NavigationService_WithRealGoogleMapsAdapter_PreservesOriginalGoogleMapsUrlBehavior()
    {
        // Prueba de integración de la cadena completa del patrón:
        // Cliente (NavigationService) -> Target (INavigationProvider) ->
        // Adapter (GoogleMapsAdapter) -> Adaptee (GoogleMapsApi), usando el
        // Adapter real (no el fake) para comprobar que la funcionalidad
        // original (enlace de Google Maps) se preserva.
        var routeRepository = new InMemoryDeliveryRouteRepository();
        var routeId = await SeedRouteAsync(routeRepository);
        var adapter = new GoogleMapsAdapter(new GoogleMapsApi());
        var service = new NavigationService(routeRepository, adapter);

        var link = await service.GenerateExternalNavigationLinkAsync(routeId);

        Assert.StartsWith("https://www.google.com/maps/dir/?api=1&origin=", link.ExternalMapsUrl);
        Assert.Contains("destination=", link.ExternalMapsUrl);
        Assert.Contains("travelmode=driving", link.ExternalMapsUrl);
        Assert.Contains("waypoints=", link.ExternalMapsUrl);
        Assert.Equal(2, link.OrderedStops.Count);
    }
}
