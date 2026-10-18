using FleetManagement.Application.Geo;
using FleetManagement.Application.Simulation;
using FleetManagement.Domain.Entities;
using Xunit;
using static FleetManagement.Application.Tests.Simulation.SimulationTestData;

namespace FleetManagement.Application.Tests.Simulation;

/// <summary>
/// Pruebas de la poligonal de la simulación (<see cref="RoutePath"/>) y del avance que calcula sobre ella
/// (<see cref="TripProgress"/>). Se usa una ruta sobre el ecuador para conocer a mano cada distancia esperada.
/// </summary>
public class RoutePathTests
{
    private const double Tolerance = 1e-9;

    private static readonly double[] ExpectedCumulativeKm =
    {
        0.0, 0.10 * KmPerDegree, 0.30 * KmPerDegree, 0.60 * KmPerDegree
    };

    [Fact]
    public void From_BuildsOriginThenWaypointsSortedByOrderThenDestination()
    {
        var path = RoutePath.From(EquatorRoute());

        Assert.Equal(new[] { "Origen", "Primera", "Segunda", "Destino" }, path.Stops.Select(s => s.Label).ToArray());
        Assert.Equal(new[] { 0.00, 0.10, 0.30, 0.60 }, path.Stops.Select(s => s.Location.Longitude).ToArray());
    }

    [Fact]
    public void CumulativeKm_IsTheRunningSumOfTheSegments_AndTotalKmIsTheLastOne()
    {
        var path = RoutePath.From(EquatorRoute());

        for (var i = 0; i < ExpectedCumulativeKm.Length; i++)
            Assert.Equal(ExpectedCumulativeKm[i], path.CumulativeKm[i], Tolerance);
        Assert.Equal(path.CumulativeKm[^1], path.TotalKm);
    }

    [Fact]
    public void PointAt_ZeroIsTheOrigin_AndTheTotalDistanceIsExactlyTheDestination()
    {
        var path = RoutePath.From(EquatorRoute());

        Assert.Equal(new GeoCoordinate(0, 0.00), path.PointAt(0));
        Assert.Equal(new GeoCoordinate(0, 0.60), path.PointAt(path.TotalKm));
    }

    [Fact]
    public void PointAt_BeyondTheEndOrBeforeTheStart_IsClampedToTheEndpoints()
    {
        var path = RoutePath.From(EquatorRoute());

        Assert.Equal(new GeoCoordinate(0, 0.60), path.PointAt(path.TotalKm + 25));
        Assert.Equal(new GeoCoordinate(0, 0.00), path.PointAt(-5));
    }

    [Fact]
    public void PointAt_InsideASegment_InterpolatesOnThatSegment()
    {
        var path = RoutePath.From(EquatorRoute());
        var halfOfTheSecondSegment = (ExpectedCumulativeKm[1] + ExpectedCumulativeKm[2]) / 2;

        var point = path.PointAt(halfOfTheSecondSegment);

        Assert.Equal(0.0, point.Latitude, Tolerance);
        Assert.Equal(0.20, point.Longitude, Tolerance);   // mitad entre 0,10° y 0,30°
    }

    [Fact]
    public void PointAt_AtAWaypointDistance_IsExactlyThatWaypoint()
    {
        var path = RoutePath.From(EquatorRoute());

        Assert.Equal(new GeoCoordinate(0, 0.10), path.PointAt(path.CumulativeKm[1]));
        Assert.Equal(new GeoCoordinate(0, 0.30), path.PointAt(path.CumulativeKm[2]));
    }

    [Fact]
    public void PointAt_OnARouteWithCurves_ALwaysStaysOnTheStraightSegmentsOfThePolyline()
    {
        var path = RoutePath.From(BentRoute());
        var polyline = path.Stops.Select(s => s.Location).ToList();

        for (var distance = 0.0; distance <= path.TotalKm; distance += path.TotalKm / 250)
        {
            var point = path.PointAt(distance);
            Assert.True(DistanceToPolylineInDegrees(polyline, point.Latitude, point.Longitude) < Tolerance,
                $"A {distance:F3} km el punto ({point.Latitude}, {point.Longitude}) se salió de la poligonal.");
        }
    }

    [Fact]
    public void NextStopLabelAt_NamesTheStopThatComesNext_AndSwitchesWhenAStopIsReached()
    {
        var path = RoutePath.From(EquatorRoute());

        Assert.Equal("Primera", path.NextStopLabelAt(0));
        Assert.Equal("Primera", path.NextStopLabelAt(ExpectedCumulativeKm[1] - 0.001));
        Assert.Equal("Segunda", path.NextStopLabelAt(ExpectedCumulativeKm[1]));          // ya llegó a la primera
        Assert.Equal("Destino", path.NextStopLabelAt(ExpectedCumulativeKm[2] + 0.001));
        Assert.Equal("Destino", path.NextStopLabelAt(path.TotalKm));
    }

    [Fact]
    public void SegmentAt_ReportsTheCurrentSegmentWithItsBounds()
    {
        var path = RoutePath.From(EquatorRoute());

        var segment = path.SegmentAt(ExpectedCumulativeKm[1] + 1);

        Assert.Equal(1, segment.Index);
        Assert.Equal("Primera", segment.Start.Label);
        Assert.Equal("Segunda", segment.End.Label);
        Assert.Equal(ExpectedCumulativeKm[1], segment.StartKm, Tolerance);
        Assert.Equal(ExpectedCumulativeKm[2], segment.EndKm, Tolerance);
    }

    [Fact]
    public void ARouteWithoutWaypoints_HasASingleSegmentFromOriginToDestination()
    {
        var route = new DeliveryRoute { Origin = PointAt(0, 0), Destination = PointAt(0, 0.5) };

        var path = RoutePath.From(route);

        Assert.Equal(2, path.Stops.Count);
        Assert.Equal(0, path.SegmentAt(10).Index);
        Assert.Equal("Destino", path.NextStopLabelAt(10));
        Assert.Equal(0.5 * KmPerDegree, path.TotalKm, Tolerance);
    }

    [Fact]
    public void AWaypointWithoutLabel_GetsAGenericNameWithItsPosition()
    {
        var route = EquatorRoute();
        route.Waypoints.Single(w => w.Order == 0).Label = "  ";

        var path = RoutePath.From(route);

        Assert.Equal("Parada 1", path.Stops[1].Label);
        Assert.Equal("Segunda", path.Stops[2].Label);
    }

    [Fact]
    public void ARouteWithZeroLength_HasNothingToTravel_AndIsAlreadyAtItsDestination()
    {
        var route = new DeliveryRoute { Origin = PointAt(7.1, -73.1), Destination = PointAt(7.1, -73.1) };
        var path = RoutePath.From(route);

        var progress = path.ProgressAt(Guid.NewGuid(), 0, 60);

        Assert.Equal(0.0, path.TotalKm);
        Assert.Equal(new GeoCoordinate(7.1, -73.1), path.PointAt(0));
        Assert.Equal(100.0, progress.Percent);
        Assert.True(progress.Arrived);
    }

    [Fact]
    public void ConsecutiveCoincidentPoints_AreCrossedWithoutDividingByZero()
    {
        var route = EquatorRoute();
        route.Waypoints.Add(new Waypoint { Order = 2, Label = "Repetida", Location = PointAt(0, 0.30) });   // igual que "Segunda"

        var path = RoutePath.From(route);

        Assert.Equal(path.CumulativeKm[2], path.CumulativeKm[3], Tolerance);
        Assert.Equal("Segunda", path.NextStopLabelAt(path.CumulativeKm[2] - 0.001));
        Assert.Equal("Destino", path.NextStopLabelAt(path.CumulativeKm[2]));   // la parada repetida se cruza en el mismo instante
        var point = path.PointAt(path.CumulativeKm[2] + 1);
        Assert.Equal(0.30 + 1 / KmPerDegree, point.Longitude, 9);
        Assert.Equal(0.0, point.Latitude, Tolerance);
    }

    [Fact]
    public void ThePath_IsASnapshot_ChangingTheRouteAfterwardsDoesNotAffectIt()
    {
        var route = EquatorRoute();
        var path = RoutePath.From(route);
        var totalBefore = path.TotalKm;

        route.Destination = PointAt(0, 5);
        route.Waypoints.Clear();

        Assert.Equal(totalBefore, path.TotalKm);
        Assert.Equal(4, path.Stops.Count);
    }

    [Fact]
    public void ThePublishedCollections_CannotBeModifiedByTheCaller()
    {
        var path = RoutePath.From(EquatorRoute());

        Assert.Throws<NotSupportedException>(() => ((IList<RouteStop>)path.Stops).Add(new RouteStop(new GeoCoordinate(1, 1), "Intrusa")));
        Assert.Throws<NotSupportedException>(() => ((IList<double>)path.CumulativeKm)[0] = 99);
    }

    [Fact]
    public void ProgressAt_DescribesWhereTheVehicleIsAndHowMuchIsLeft()
    {
        var path = RoutePath.From(EquatorRoute());
        var vehicleId = Guid.NewGuid();
        var covered = ExpectedCumulativeKm[2];   // llegó a la parada "Segunda", a la mitad de la ruta

        var progress = path.ProgressAt(vehicleId, covered, speedKmh: 60);

        Assert.Equal(path.RouteId, progress.RouteId);
        Assert.Equal(vehicleId, progress.VehicleId);
        Assert.Equal(covered, progress.DistanceCoveredKm, Tolerance);
        Assert.Equal(path.TotalKm, progress.TotalKm);
        Assert.Equal(50.0, progress.Percent, 6);
        Assert.Equal(0.0, progress.Latitude, Tolerance);
        Assert.Equal(0.30, progress.Longitude, Tolerance);
        Assert.Equal("Destino", progress.NextStopLabel);
        Assert.Equal(path.TotalKm - covered, progress.RemainingKm, Tolerance);
        Assert.Equal((path.TotalKm - covered) / 60 * 60, progress.EtaMinutes, Tolerance);   // a 60 km/h, un km por minuto
        Assert.False(progress.Arrived);
    }

    [Fact]
    public void ProgressAt_BeyondTheEnd_IsClampedAndMarkedAsArrived()
    {
        var path = RoutePath.From(EquatorRoute());

        var progress = path.ProgressAt(Guid.NewGuid(), path.TotalKm + 10, speedKmh: 60);

        Assert.Equal(path.TotalKm, progress.DistanceCoveredKm);
        Assert.Equal(100.0, progress.Percent);
        Assert.Equal(0.0, progress.RemainingKm);
        Assert.Equal(0.0, progress.EtaMinutes);
        Assert.True(progress.Arrived);
        Assert.Equal(0.60, progress.Longitude);
    }

    [Fact]
    public void ProgressAt_WithAFasterVehicle_NeedsFewerMinutesForTheSameDistance()
    {
        var path = RoutePath.From(EquatorRoute());

        var slow = path.ProgressAt(Guid.NewGuid(), 0, speedKmh: 40);
        var fast = path.ProgressAt(Guid.NewGuid(), 0, speedKmh: 80);

        Assert.Equal(slow.EtaMinutes / 2, fast.EtaMinutes, Tolerance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void ProgressAt_RequiresAPositiveSpeed_ToAvoidAnInfiniteEta(double speedKmh)
    {
        var path = RoutePath.From(EquatorRoute());

        Assert.Throws<ArgumentOutOfRangeException>(() => path.ProgressAt(Guid.NewGuid(), 1, speedKmh));
    }
}
