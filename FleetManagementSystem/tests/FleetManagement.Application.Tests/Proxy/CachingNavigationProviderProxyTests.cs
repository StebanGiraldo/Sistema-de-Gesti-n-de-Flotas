using System.Collections.Concurrent;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Services;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Infrastructure.Navigation;
using FleetManagement.Infrastructure.Persistence;
using Xunit;
using static FleetManagement.Application.Tests.Simulation.SimulationTestData;

namespace FleetManagement.Application.Tests.Proxy;

/// <summary>
/// PRUEBAS DEL PATRÓN PROXY (caché): comprueban que <see cref="CachingNavigationProviderProxy"/> controla el acceso al
/// proveedor real. Un contador en el "sujeto real" de prueba demuestra cuándo se le llama y cuándo no, y un reloj manual
/// hace exactos los vencimientos. Los valores de ejemplo (7.10, -73.10 → 7.20, -73.20) son coordenadas cualquiera.
/// </summary>
public class CachingNavigationProviderProxyTests
{
    private static readonly WaypointDto StopA = new(7.12, -73.12, "Parada A", 0);
    private static readonly WaypointDto StopB = new(7.15, -73.15, "Parada B", 1);

    // ---------------- Llamadas repetidas y llamadas distintas ----------------

    [Fact]
    public async Task SecondIdenticalCall_ReturnsTheStoredUrl_WithoutReachingTheRealSubject()
    {
        var rig = new ProxyRig();

        var first = await rig.GenerateAsync(StopA, StopB);
        var second = await rig.GenerateAsync(StopA, StopB);

        Assert.Equal(1, rig.Subject.Calls);
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(7.11, -73.10, 7.20, -73.20)] // otra latitud de origen
    [InlineData(7.10, -73.11, 7.20, -73.20)] // otra longitud de origen
    [InlineData(7.10, -73.10, 7.21, -73.20)] // otra latitud de destino
    [InlineData(7.10, -73.10, 7.20, -73.21)] // otra longitud de destino
    [InlineData(7.20, -73.20, 7.10, -73.10)] // el mismo trayecto en sentido contrario
    public async Task ADifferentOriginOrDestination_IsADifferentKey_AndReachesTheSubject(double originLat, double originLng, double destinationLat, double destinationLng)
    {
        var rig = new ProxyRig();
        await rig.GenerateAsync();

        var other = await rig.Proxy.GenerateRouteUrlAsync(originLat, originLng, destinationLat, destinationLng, new List<WaypointDto>());

        Assert.Equal(2, rig.Subject.Calls);
        Assert.Contains("llamada=2", other);
    }

    [Fact]
    public async Task DifferentWaypoints_AreDifferentKeys()
    {
        var rig = new ProxyRig();

        await rig.GenerateAsync();                 // sin paradas
        await rig.GenerateAsync(StopA);            // una parada
        await rig.GenerateAsync(StopB);            // otra parada
        await rig.GenerateAsync(StopA, StopB);     // dos paradas

        Assert.Equal(4, rig.Subject.Calls);
    }

    // ---------------- Paradas: el orden de visita importa; la etiqueta no ----------------

    [Fact]
    public async Task WaypointsAreOrderedByOrder_SoTheSameVisitSequenceGivenInAnotherInputOrderSharesTheEntry()
    {
        var rig = new ProxyRig();

        await rig.GenerateAsync(StopA, StopB);
        await rig.GenerateAsync(StopB, StopA); // llegan desordenadas, pero Order dice que A va primero

        Assert.Equal(1, rig.Subject.Calls);
    }

    [Fact]
    public async Task SwappingTheVisitOrderOfTheWaypoints_IsADifferentKey()
    {
        var rig = new ProxyRig();

        await rig.GenerateAsync(StopA with { Order = 0 }, StopB with { Order = 1 });
        await rig.GenerateAsync(StopA with { Order = 1 }, StopB with { Order = 0 }); // ahora B se visita antes que A

        Assert.Equal(2, rig.Subject.Calls);
    }

    [Fact]
    public async Task WaypointLabelsAndAbsoluteOrderValues_DoNotChangeTheKey()
    {
        var rig = new ProxyRig();

        await rig.GenerateAsync(StopA, StopB);
        await rig.GenerateAsync(StopA with { Label = "Otro nombre", Order = 10 }, StopB with { Label = "Y otro", Order = 20 });

        Assert.Equal(1, rig.Subject.Calls);
    }

    // ---------------- Redondeo de coordenadas ----------------

    [Theory]
    [InlineData(7.1234561, 7.1234564, 1)] // ambas se redondean a 7.123456: misma clave
    [InlineData(7.1234561, 7.1234569, 2)] // 7.123456 contra 7.123457: claves distintas
    [InlineData(-0.0000001, 0.0, 1)]      // -0 y 0 deben ser la misma clave
    public async Task CoordinatesAreRoundedToSixDecimals_BeforeBuildingTheKey(double firstLatitude, double secondLatitude, int expectedProviderCalls)
    {
        var rig = new ProxyRig();

        await rig.Proxy.GenerateRouteUrlAsync(firstLatitude, -73.10, 7.20, -73.20, new List<WaypointDto>());
        await rig.Proxy.GenerateRouteUrlAsync(secondLatitude, -73.10, 7.20, -73.20, new List<WaypointDto>());

        Assert.Equal(expectedProviderCalls, rig.Subject.Calls);
    }

    [Fact]
    public async Task TheKeyDoesNotDependOnTheCultureOfTheMachine()
    {
        var rig = new ProxyRig();
        var commaCulture = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.InvariantCulture.Clone();
        commaCulture.NumberFormat.NumberDecimalSeparator = ","; // como "es-CO": coma decimal
        var previous = System.Globalization.CultureInfo.CurrentCulture;

        try
        {
            System.Globalization.CultureInfo.CurrentCulture = commaCulture;
            await rig.GenerateAsync(StopA);
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            await rig.GenerateAsync(StopA);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }

        Assert.Equal(1, rig.Subject.Calls);
    }

    // ---------------- Vencimiento (TTL) ----------------

    [Fact]
    public async Task TheStoredUrl_IsValidUntilTheTimeToLive_AndExpiresExactlyAtIt()
    {
        var rig = new ProxyRig(timeToLiveMinutes: 10);
        var first = await rig.GenerateAsync();

        rig.Time.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));
        var justBefore = await rig.GenerateAsync();
        Assert.Equal(1, rig.Subject.Calls);
        Assert.Equal(first, justBefore);

        rig.Time.Advance(TimeSpan.FromSeconds(1)); // ahora se cumple el tiempo de vida completo
        var afterExpiry = await rig.GenerateAsync();

        Assert.Equal(2, rig.Subject.Calls);
        Assert.NotEqual(first, afterExpiry);
        Assert.Contains("llamada=2", afterExpiry);
    }

    [Fact]
    public async Task AnExpiredUrl_IsRenewedWithAFullNewLifetime()
    {
        var rig = new ProxyRig(timeToLiveMinutes: 10);
        await rig.GenerateAsync();
        rig.Time.Advance(TimeSpan.FromMinutes(11));
        var renewed = await rig.GenerateAsync(); // vuelve al proveedor y se guarda de nuevo

        rig.Time.Advance(TimeSpan.FromMinutes(9));
        var stillValid = await rig.GenerateAsync();

        Assert.Equal(2, rig.Subject.Calls);
        Assert.Equal(renewed, stillValid);
    }

    // ---------------- Tamaño máximo ----------------

    [Fact]
    public async Task WhenTheCacheIsFull_TheOldestEntryIsDiscarded_AndTheRestKeepBeingHits()
    {
        var rig = new ProxyRig(maxEntries: 3);
        for (var index = 1; index <= 4; index++)
        {
            await rig.GenerateFromOriginAsync(index);
            rig.Time.Advance(TimeSpan.FromSeconds(1)); // así "la más antigua" está bien definida
        }
        Assert.Equal(4, rig.Subject.Calls); // al guardar la cuarta se descartó la primera

        await rig.GenerateFromOriginAsync(2);
        await rig.GenerateFromOriginAsync(3);
        await rig.GenerateFromOriginAsync(4);
        Assert.Equal(4, rig.Subject.Calls); // las tres más recientes siguen guardadas

        await rig.GenerateFromOriginAsync(1);
        Assert.Equal(5, rig.Subject.Calls); // la primera ya no estaba: se pidió de nuevo
    }

    [Fact]
    public async Task WhenTheCacheIsFull_AnExpiredEntryIsRemovedInsteadOfALiveOne()
    {
        var rig = new ProxyRig(timeToLiveMinutes: 10, maxEntries: 2);
        await rig.GenerateFromOriginAsync(1);          // vence en el minuto 10
        rig.Time.Advance(TimeSpan.FromMinutes(6));
        await rig.GenerateFromOriginAsync(2);          // vence en el minuto 16
        rig.Time.Advance(TimeSpan.FromMinutes(5));     // minuto 11: la 1 venció y la 2 sigue viva

        await rig.GenerateFromOriginAsync(3);          // el caché está lleno: debe hacer lugar retirando la vencida

        await rig.GenerateFromOriginAsync(2);
        Assert.Equal(3, rig.Subject.Calls); // la 2 siguió siendo un acierto
    }

    // ---------------- Fallos del proveedor ----------------

    [Fact]
    public async Task AProviderFailure_IsPropagatedToTheCaller_AndIsNotCached()
    {
        var rig = new ProxyRig();
        rig.Subject.FailureOnNextCall = new InvalidOperationException("Proveedor no disponible.");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.GenerateAsync());
        var retried = await rig.GenerateAsync();   // el fallo no quedó guardado: se vuelve a intentar
        var cached = await rig.GenerateAsync();    // y ahora sí hay una URL guardada

        Assert.Equal("Proveedor no disponible.", failure.Message);
        Assert.Equal(2, rig.Subject.Calls);
        Assert.Equal(retried, cached);
    }

    // ---------------- Transparencia: mismo contrato que el sujeto real ----------------

    [Fact]
    public async Task TheProxyReturnsExactlyTheUrlTheRealAdapterWouldReturn()
    {
        var adapter = new GoogleMapsAdapter(new GoogleMapsApi());
        var proxy = new CachingNavigationProviderProxy(adapter, new NavigationCacheOptions(), new ManualTimeProvider());
        var waypoints = new List<WaypointDto> { StopB, StopA }; // desordenadas a propósito: el Adapter las ordena

        var direct = await adapter.GenerateRouteUrlAsync(7.10, -73.10, 7.20, -73.20, waypoints);
        var throughProxy = await proxy.GenerateRouteUrlAsync(7.10, -73.10, 7.20, -73.20, waypoints);
        var throughProxyAgain = await proxy.GenerateRouteUrlAsync(7.10, -73.10, 7.20, -73.20, waypoints);

        Assert.Equal(direct, throughProxy);
        Assert.Equal(direct, throughProxyAgain);
    }

    [Fact]
    public async Task NavigationService_WorksUnchangedBehindTheProxy_AndStopsReachingTheProviderOnRepeatedRequests()
    {
        var subject = new CountingNavigationProvider();
        var proxy = new CachingNavigationProviderProxy(subject, new NavigationCacheOptions(), new ManualTimeProvider());
        var routes = new InMemoryDeliveryRouteRepository();
        var route = EquatorRoute();
        await routes.AddAsync(route);
        var service = new NavigationService(routes, proxy);

        var first = await service.GenerateExternalNavigationLinkAsync(route.Id);
        var second = await service.GenerateExternalNavigationLinkAsync(route.Id);
        Assert.Equal(1, subject.Calls);
        Assert.Equal(first.ExternalMapsUrl, second.ExternalMapsUrl);

        route.Destination = PointAt(0.0, 0.70); // la ruta cambió: es otro trayecto y no debe servirse la URL vieja
        var third = await service.GenerateExternalNavigationLinkAsync(route.Id);

        Assert.Equal(2, subject.Calls);
        Assert.NotEqual(first.ExternalMapsUrl, third.ExternalMapsUrl);
    }

    // ---------------- Varios hilos ----------------

    [Fact]
    public void ConcurrentCalls_ReturnTheUrlOfTheirOwnRoute_AndLaterCallsAreAllHits()
    {
        var rig = new ProxyRig(maxEntries: 1000);
        const int distinctRoutes = 20;
        var wrongUrls = new ConcurrentBag<string>();

        Parallel.For(0, 2000, index =>
        {
            var origin = 1 + index % distinctRoutes;
            var url = rig.GenerateFromOriginAsync(origin).GetAwaiter().GetResult();
            if (!url.Contains($"o={origin},")) wrongUrls.Add(url);
        });

        Assert.Empty(wrongUrls);
        Assert.InRange(rig.Subject.Calls, distinctRoutes, 2000);

        var callsBefore = rig.Subject.Calls;
        for (var origin = 1; origin <= distinctRoutes; origin++)
            rig.GenerateFromOriginAsync(origin).GetAwaiter().GetResult();
        Assert.Equal(callsBefore, rig.Subject.Calls); // las 20 rutas ya estaban guardadas
    }

    /// <summary>Proxy armado con el sujeto de prueba y un reloj manual, más atajos para pedir URLs.</summary>
    private sealed class ProxyRig
    {
        public ProxyRig(double timeToLiveMinutes = 10, int maxEntries = 500)
        {
            Proxy = new CachingNavigationProviderProxy(
                Subject, new NavigationCacheOptions { TimeToLiveMinutes = timeToLiveMinutes, MaxEntries = maxEntries }, Time);
        }

        public CountingNavigationProvider Subject { get; } = new();
        public ManualTimeProvider Time { get; } = new();
        public CachingNavigationProviderProxy Proxy { get; }

        public Task<string> GenerateAsync(params WaypointDto[] waypoints)
            => Proxy.GenerateRouteUrlAsync(7.10, -73.10, 7.20, -73.20, waypoints);

        /// <summary>Cada valor de <paramref name="origin"/> es una ruta distinta (cambia la latitud de origen en grados enteros).</summary>
        public Task<string> GenerateFromOriginAsync(int origin)
            => Proxy.GenerateRouteUrlAsync(origin, -73.10, 7.20, -73.20, new List<WaypointDto>());
    }
}
