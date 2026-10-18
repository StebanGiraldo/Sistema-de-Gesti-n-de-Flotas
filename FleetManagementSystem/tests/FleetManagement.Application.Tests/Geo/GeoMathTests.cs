using FleetManagement.Application.DTOs;
using FleetManagement.Application.Geo;
using FleetManagement.Application.Tests.Support;
using Xunit;

namespace FleetManagement.Application.Tests.Geo;

/// <summary>
/// Pruebas de los cálculos geográficos puros (Haversine e interpolación). Los valores de referencia de distancia se
/// calcularon aparte, con una implementación independiente, para no comparar la fórmula consigo misma.
/// </summary>
public class GeoMathTests
{
    private static readonly GeoCoordinate Bucaramanga = new(7.1193, -73.1227);
    private static readonly GeoCoordinate Bogota = new(4.7110, -74.0721);
    private static readonly GeoCoordinate Giron = new(7.0806, -73.1716);

    [Fact]
    public void DistanceKm_BetweenTheSameCoordinate_IsZero()
    {
        Assert.Equal(0.0, GeoMath.DistanceKm(Bucaramanga, Bucaramanga));
    }

    [Fact]
    public void DistanceKm_MatchesTheIndependentReferenceValues()
    {
        Assert.Equal(287.6396, GeoMath.DistanceKm(Bucaramanga, Bogota), 3);
        Assert.Equal(6.9016, GeoMath.DistanceKm(Giron, Bucaramanga), 3);
    }

    [Fact]
    public void DistanceKm_IsTheSameInBothDirections()
    {
        Assert.Equal(GeoMath.DistanceKm(Bucaramanga, Bogota), GeoMath.DistanceKm(Bogota, Bucaramanga), 9);
    }

    [Fact]
    public void DistanceKm_OneDegreeAlongAMeridianAndAlongTheEquator_IsTheSame111Km()
    {
        var alongMeridian = GeoMath.DistanceKm(new GeoCoordinate(0, 0), new GeoCoordinate(1, 0));
        var alongEquator = GeoMath.DistanceKm(new GeoCoordinate(0, 0), new GeoCoordinate(0, 1));

        Assert.Equal(111.1949, alongMeridian, 3);
        Assert.Equal(alongMeridian, alongEquator, 9);
    }

    [Fact]
    public async Task DeliveryRouteService_KeepsEstimatingTheDistanceExactlyAsBefore_NowThroughGeoMath()
    {
        // El cálculo de la estimación pasó a GeoMath; el resultado debe seguir siendo la suma de los tramos con
        // Haversine redondeada a 2 decimales (igual que antes de la extracción).
        var fleet = new RealFleet();
        var request = new CreateDeliveryRouteRequest(
            "Ruta de comparación", Giron.Latitude, Giron.Longitude, Bucaramanga.Latitude, Bucaramanga.Longitude,
            new List<WaypointDto> { new(7.0850, -73.1690, "Parada", 0) },
            new List<CargoItemDto>(), null, null, null);

        var route = await fleet.Routes.CreateRouteAsync(request);

        var expected = GeoMath.DistanceKm(Giron, new GeoCoordinate(7.0850, -73.1690))
                     + GeoMath.DistanceKm(new GeoCoordinate(7.0850, -73.1690), Bucaramanga);
        Assert.Equal(Math.Round(expected, 2), route.EstimatedDistanceKm);
        Assert.Equal(6.94, route.EstimatedDistanceKm);   // valor de referencia calculado aparte (6,9426 km)
    }

    [Fact]
    public void Interpolate_ReturnsTheExactEndpointsAtFractionZeroAndOne()
    {
        Assert.Equal(Giron, GeoMath.Interpolate(Giron, Bucaramanga, 0.0));
        Assert.Equal(Bucaramanga, GeoMath.Interpolate(Giron, Bucaramanga, 1.0));
    }

    [Fact]
    public void Interpolate_AtHalf_IsTheMidpointOfTheSegment()
    {
        var middle = GeoMath.Interpolate(new GeoCoordinate(0, 0), new GeoCoordinate(2, 4), 0.5);

        Assert.Equal(1.0, middle.Latitude, 12);
        Assert.Equal(2.0, middle.Longitude, 12);
    }

    [Fact]
    public void Interpolate_OutsideTheSegment_IsClampedToTheEndpoints()
    {
        Assert.Equal(Giron, GeoMath.Interpolate(Giron, Bucaramanga, -0.3));
        Assert.Equal(Bucaramanga, GeoMath.Interpolate(Giron, Bucaramanga, 1.7));
    }

    [Fact]
    public void Interpolate_TheCoveredDistanceGrowsInProportionToTheFraction()
    {
        var total = GeoMath.DistanceKm(Giron, Bucaramanga);

        var quarter = GeoMath.DistanceKm(Giron, GeoMath.Interpolate(Giron, Bucaramanga, 0.25));
        var threeQuarters = GeoMath.DistanceKm(Giron, GeoMath.Interpolate(Giron, Bucaramanga, 0.75));

        Assert.Equal(total * 0.25, quarter, 3);
        Assert.Equal(total * 0.75, threeQuarters, 3);
    }
}
