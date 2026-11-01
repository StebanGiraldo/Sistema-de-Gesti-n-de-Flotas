using FleetManagement.Application.Geo;
using FleetManagement.Application.Optimization;
using Xunit;
using static FleetManagement.Application.Tests.Optimization.OptimizationTestData;

namespace FleetManagement.Application.Tests.Optimization;

public class TourLengthTests
{
    [Fact]
    public void WithoutStopsOrDestination_IsZero()
        => Assert.Equal(0, TourLength.Km(OnEquator(0), new List<GeoCoordinate>()));

    [Fact]
    public void WithoutStops_IsTheStraightLineToTheDestination()
        => Assert.Equal(0.6 * KmPerDegree, TourLength.Km(OnEquator(0), new List<GeoCoordinate>(), OnEquator(0.6)), 9);

    [Fact]
    public void AddsEveryLegInOrder()
    {
        var stops = new List<GeoCoordinate> { OnEquator(0.1), OnEquator(0.3) };

        Assert.Equal(0.6 * KmPerDegree, TourLength.Km(OnEquator(0), stops, OnEquator(0.6)), 9);
        Assert.Equal(0.3 * KmPerDegree, TourLength.Km(OnEquator(0), stops), 9);   // recorrido abierto: termina en la última parada
    }

    [Fact]
    public void DependsOnTheOrderOfTheStops()
    {
        var stops = new List<GeoCoordinate> { OnEquator(0.3), OnEquator(0.1) };

        Assert.Equal((0.3 + 0.2) * KmPerDegree, TourLength.Km(OnEquator(0), stops), 9);
        Assert.Equal(0.3 * KmPerDegree, TourLength.Km(OnEquator(0), stops, new List<int> { 1, 0 }), 9);
    }
}

public class NearestNeighborRouteOptimizerTests
{
    private readonly NearestNeighborRouteOptimizer _optimizer = new();

    [Fact]
    public void WithoutStops_ReturnsAnEmptyOrder()
        => Assert.Empty(_optimizer.Order(OnEquator(0), new List<GeoCoordinate>()));

    [Fact]
    public void WithOneStop_ReturnsThatStop()
        => Assert.Equal(new[] { 0 }, _optimizer.Order(OnEquator(0), new List<GeoCoordinate> { OnEquator(0.4) }));

    [Fact]
    public void AlwaysGoesToTheNearestPendingStop()
    {
        var stops = new List<GeoCoordinate> { OnEquator(0.5), OnEquator(0.1), OnEquator(0.3) };

        Assert.Equal(new[] { 1, 2, 0 }, _optimizer.Order(OnEquator(0), stops));
    }

    [Fact]
    public void WhenTwoStopsAreEquallyNear_TheOneThatCameFirstWins()
    {
        var stops = new List<GeoCoordinate> { At(0, 0.1), At(0, -0.1) };

        Assert.Equal(new[] { 0, 1 }, _optimizer.Order(OnEquator(0), stops));
    }

    [Fact]
    public void IgnoresTheFixedDestination()
    {
        var stops = new List<GeoCoordinate> { OnEquator(0.5), OnEquator(0.1), OnEquator(0.3) };

        Assert.Equal(_optimizer.Order(OnEquator(0), stops), _optimizer.Order(OnEquator(0), stops, OnEquator(-5)));
    }

    [Fact]
    public void AlwaysReturnsEveryStopExactlyOnce_AndTheSameResultForTheSameData()
    {
        var random = new Random(7);
        for (var run = 0; run < 50; run++)
        {
            var stops = RandomPoints(random, random.Next(0, 12));
            var origin = RandomPoints(random, 1)[0];

            var order = _optimizer.Order(origin, stops);

            Assert.Equal(Enumerable.Range(0, stops.Count), order.OrderBy(index => index));
            Assert.Equal(order, _optimizer.Order(origin, stops));
        }
    }
}

public class TwoOptRouteOptimizerTests
{
    private readonly TwoOptRouteOptimizer _twoOpt = new(new NearestNeighborRouteOptimizer());
    private readonly NearestNeighborRouteOptimizer _nearestNeighbor = new();

    [Fact]
    public void WithFewerThanTwoStops_ReturnsWhatTheSeedGave()
    {
        Assert.Empty(_twoOpt.Order(OnEquator(0), new List<GeoCoordinate>()));
        Assert.Equal(new[] { 0 }, _twoOpt.Order(OnEquator(0), new List<GeoCoordinate> { OnEquator(0.2) }, OnEquator(0.9)));
    }

    [Fact]
    public void FixesACrossingThatTheSeedLeft()
    {
        // Origen (0,0) y esquinas de un cuadrado de 0,1°. Visitadas en este orden se cruza la diagonal: 1 + √2 + 1.
        var stops = new List<GeoCoordinate> { At(0.1, 0), At(0, 0.1), At(0.1, 0.1) };
        var twoOpt = new TwoOptRouteOptimizer(new InOrderOptimizer());

        var order = twoOpt.Order(At(0, 0), stops);

        Assert.Equal(3 * 0.1 * KmPerDegree, TourLength.Km(At(0, 0), stops, order), 3);   // el mejor: 3 lados del cuadrado (±0,5 m: un paralelo a 0,1° de latitud mide un poco menos)
        Assert.True(TourLength.Km(At(0, 0), stops, new List<int> { 0, 1, 2 }) > TourLength.Km(At(0, 0), stops, order));
    }

    [Fact]
    public void NeverReturnsALongerTourThanItsSeed_WithOrWithoutAFixedDestination()
    {
        var random = new Random(11);
        for (var run = 0; run < 200; run++)
        {
            var stops = RandomPoints(random, random.Next(2, 14));
            var points = RandomPoints(random, 2);
            GeoCoordinate? destination = run % 2 == 0 ? points[1] : null;

            var seedKm = TourLength.Km(points[0], stops, _nearestNeighbor.Order(points[0], stops, destination), destination);
            var order = _twoOpt.Order(points[0], stops, destination);

            Assert.Equal(Enumerable.Range(0, stops.Count), order.OrderBy(index => index));
            Assert.True(TourLength.Km(points[0], stops, order, destination) <= seedKm + 1e-9);
        }
    }

    [Fact]
    public void ImprovesTheNearestNeighborTourInSomeInstances()
    {
        var random = new Random(11);
        var improved = 0;
        for (var run = 0; run < 200; run++)
        {
            var stops = RandomPoints(random, random.Next(4, 12));
            var origin = RandomPoints(random, 1)[0];

            var seedKm = TourLength.Km(origin, stops, _nearestNeighbor.Order(origin, stops));
            var km = TourLength.Km(origin, stops, _twoOpt.Order(origin, stops));
            if (km < seedKm - 1e-6) improved++;
        }

        Assert.True(improved > 0, "el 2-opt debería acortar algún recorrido que dejó el vecino más cercano");
    }

    [Fact]
    public void StaysCloseToTheBestPossibleTour_OnSmallInstances()
    {
        var random = new Random(23);
        var worstRatio = 0.0;
        for (var run = 0; run < 100; run++)
        {
            var stops = RandomPoints(random, random.Next(3, 7));
            var origin = RandomPoints(random, 1)[0];

            var best = BestPossibleKm(origin, stops);
            var km = TourLength.Km(origin, stops, _twoOpt.Order(origin, stops));

            Assert.True(km >= best - 1e-9, "ningún orden puede ser más corto que el mejor posible");
            worstRatio = Math.Max(worstRatio, km / best);
        }

        Assert.True(worstRatio <= 1.35, $"el peor caso quedó {worstRatio:0.000} veces más largo que el óptimo");
    }

    [Fact]
    public void KeepsTheDestinationAtTheEnd_SoItIsNeverPartOfTheOrder()
    {
        var stops = new List<GeoCoordinate> { OnEquator(0.5), OnEquator(0.1), OnEquator(0.3) };

        var order = _twoOpt.Order(OnEquator(0), stops, OnEquator(0.6));

        Assert.Equal(3, order.Count);
        Assert.Equal(0.6 * KmPerDegree, TourLength.Km(OnEquator(0), stops, order, OnEquator(0.6)), 9);   // sobre una recta, ir en línea hasta el destino es lo mejor
    }

    [Fact]
    public void GivesTheSameResultForTheSameData()
    {
        var random = new Random(5);
        var stops = RandomPoints(random, 10);
        var origin = RandomPoints(random, 1)[0];

        Assert.Equal(_twoOpt.Order(origin, stops), _twoOpt.Order(origin, stops));
    }
}
