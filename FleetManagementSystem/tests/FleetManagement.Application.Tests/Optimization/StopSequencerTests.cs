using FleetManagement.Application.Geo;
using FleetManagement.Application.Optimization;
using FleetManagement.Domain.Enums;
using Xunit;
using static FleetManagement.Application.Tests.Optimization.OptimizationTestData;

namespace FleetManagement.Application.Tests.Optimization;

public class StopSequencerTests
{
    private static readonly GeoCoordinate Depot = OnEquator(0);
    private readonly PriorityStopSequencer _sequencer = new(new TwoOptRouteOptimizer(new NearestNeighborRouteOptimizer()));

    private static IEnumerable<string> Names(IEnumerable<PendingDelivery> stops) => stops.Select(stop => stop.Description);

    [Fact]
    public void WithoutUrgentDeliveries_VisitsEverythingByTheShortestPath()
    {
        var deliveries = new[] { Delivery("lejos", 1, longitude: 0.5), Delivery("cerca", 1, longitude: 0.1), Delivery("medio", 1, longitude: 0.3) };

        Assert.Equal(new[] { "cerca", "medio", "lejos" }, Names(_sequencer.Sequence(Depot, deliveries)));
    }

    [Fact]
    public void UrgentDeliveriesGoFirst_EvenWhenTheyAreTheFarthest()
    {
        var deliveries = new[]
        {
            Delivery("cerca", 1, longitude: 0.1),
            Delivery("urgente lejos", 1, CargoPriority.Urgent, longitude: 0.9),
            Delivery("medio", 1, longitude: 0.3)
        };

        var order = Names(_sequencer.Sequence(Depot, deliveries)).ToList();

        Assert.Equal("urgente lejos", order[0]);
        Assert.Equal(new[] { "medio", "cerca" }, order.Skip(1));   // desde 0,9° lo más próximo es 0,3°, y luego 0,1°
    }

    [Fact]
    public void SeveralUrgentDeliveries_AreOrderedAmongThemselvesByTheShortestPath()
    {
        var deliveries = new[]
        {
            Delivery("urgente B", 1, CargoPriority.Urgent, longitude: 0.6),
            Delivery("urgente A", 1, CargoPriority.Urgent, longitude: 0.2),
            Delivery("normal", 1, longitude: 0.4)
        };

        Assert.Equal(new[] { "urgente A", "urgente B", "normal" }, Names(_sequencer.Sequence(Depot, deliveries)));
    }

    [Fact]
    public void ReturnsTheSameDeliveries_NothingAddedNothingLost()
    {
        var random = new Random(3);
        var deliveries = Enumerable.Range(0, 12)
            .Select(i => Delivery($"d{i}", 1, (CargoPriority)random.Next(0, 4), 0, random.NextDouble(), random.NextDouble())).ToList();

        var sequenced = _sequencer.Sequence(Depot, deliveries);

        Assert.Equal(deliveries.Select(d => d.Description).OrderBy(x => x), Names(sequenced).OrderBy(x => x));
    }

    [Fact]
    public void WithoutDeliveries_ReturnsNothing() => Assert.Empty(_sequencer.Sequence(Depot, new List<PendingDelivery>()));

    [Fact]
    public void GivenOrderSequencer_LeavesTheDeliveriesExactlyAsTheyCame()
    {
        var deliveries = new[] { Delivery("lejos", 1, longitude: 0.5), Delivery("urgente", 1, CargoPriority.Urgent, longitude: 0.9), Delivery("cerca", 1, longitude: 0.1) };

        Assert.Equal(new[] { "lejos", "urgente", "cerca" }, Names(new GivenOrderStopSequencer().Sequence(Depot, deliveries)));
    }
}

public class DispatchPlannerTests
{
    private static readonly GeoCoordinate Depot = OnEquator(0);

    private static DispatchPlanner Optimized() => new(new PriorityBestFitStrategy(), new PriorityStopSequencer(new TwoOptRouteOptimizer(new NearestNeighborRouteOptimizer())));
    private static DispatchPlanner Naive() => new(new FirstFitInOrderStrategy(), new GivenOrderStopSequencer());

    [Fact]
    public void TripDistanceAndTime_ComeFromTheOrderedStopsAndTheSpeedOfTheVehicle()
    {
        var van = Vehicle("VAN-002", 1000, speedKmh: 60);
        var deliveries = new[] { Delivery("a", 10, longitude: 0.2), Delivery("b", 10, longitude: 0.5) };

        var plan = Optimized().Plan(Depot, deliveries, new[] { van });

        var trip = Assert.Single(plan.Trips);
        Assert.Equal(new[] { "a", "b" }, trip.Stops.Select(s => s.Description));
        Assert.Equal(0.5 * KmPerDegree, trip.DistanceKm, 9);                      // recorrido abierto: termina en la última entrega
        Assert.Equal(0.5 * KmPerDegree / 60 * 60, trip.Minutes, 9);
        Assert.Equal(1, plan.VehiclesUsed);
        Assert.Equal(2, plan.AssignedCount);
    }

    [Fact]
    public void TotalsAddUpAcrossTrips_AndTheLongestTripIsTheLatestFinish()
    {
        var vehicles = new[] { Vehicle("A", 10, speedKmh: 60), Vehicle("B", 10, speedKmh: 60) };
        var deliveries = new[] { Delivery("a", 10, longitude: 0.2), Delivery("b", 10, longitude: 0.6) };   // no caben juntas: un viaje cada una

        var plan = Optimized().Plan(Depot, deliveries, vehicles);

        Assert.Equal(2, plan.VehiclesUsed);
        Assert.Equal((0.2 + 0.6) * KmPerDegree, plan.TotalKm, 9);
        Assert.Equal(0.6 * KmPerDegree, plan.Trips.Max(t => t.DistanceKm), 9);
        Assert.Equal(plan.Trips.Max(t => t.Minutes), plan.LongestTripMinutes, 9);
        Assert.Equal(plan.Trips.Sum(t => t.Minutes), plan.TotalMinutes, 9);
    }

    [Fact]
    public void WithoutDeliveries_ThePlanIsEmpty()
    {
        var plan = Optimized().Plan(Depot, new List<PendingDelivery>(), new[] { Vehicle("A", 10) });

        Assert.Empty(plan.Trips);
        Assert.Equal(0, plan.TotalKm);
        Assert.Equal(0, plan.LongestTripMinutes);
    }

    [Fact]
    public void TheOptimizedPlanShortensAZigZagThatTheNaivePlanKeeps()
    {
        var van = Vehicle("VAN-002", 1000);
        var zigZag = new[]
        {
            Delivery("lejos", 10, longitude: 0.9),
            Delivery("cerca", 10, longitude: 0.1),
            Delivery("medio", 10, longitude: 0.5)
        };

        var naive = Naive().Plan(Depot, zigZag, new[] { van });
        var optimized = Optimized().Plan(Depot, zigZag, new[] { van });

        Assert.Equal((0.9 + 0.8 + 0.4) * KmPerDegree, naive.TotalKm, 9);
        Assert.Equal(0.9 * KmPerDegree, optimized.TotalKm, 9);
        Assert.True(optimized.TotalKm < naive.TotalKm);
    }

    [Fact]
    public void TheOptimizedPlanPlacesWhatTheNaivePlanCouldNot_AndUsesTheVehiclesItHas()
    {
        var vehicles = new[] { Vehicle("A", 10), Vehicle("B", 10) };
        var deliveries = new[] { Delivery("d1", 4), Delivery("d2", 4), Delivery("d3", 6), Delivery("d4", 6) };

        var naive = Naive().Plan(Depot, deliveries, vehicles);
        var optimized = Optimized().Plan(Depot, deliveries, vehicles);

        Assert.Equal(3, naive.AssignedCount);
        Assert.Equal(4, optimized.AssignedCount);
        Assert.Empty(optimized.Unassigned);
    }

    [Fact]
    public void DoesNotModifyTheVehiclesOrDeliveriesItReceives()
    {
        var vehicles = new[] { Vehicle("A", 100) };
        var deliveries = new[] { Delivery("b", 1, longitude: 0.9), Delivery("a", 1, longitude: 0.1) };

        Optimized().Plan(Depot, deliveries, vehicles);

        Assert.Equal(new[] { "b", "a" }, deliveries.Select(d => d.Description));
    }
}
