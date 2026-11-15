using FleetManagement.Application.Monitoring;
using FleetManagement.Application.Simulation;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Xunit;
using static FleetManagement.Application.Tests.Simulation.SimulationTestData;

namespace FleetManagement.Application.Tests.Observer;

/// <summary>
/// Lado del PUBLICADOR: cómo se traduce un paso de la simulación en eventos. Es una función pura (<see cref="TripTelemetryEvents.ForStep"/>),
/// así que se prueba sin simulador. Ruta del ecuador (0,6° ≈ 66,717 km) con paradas "Primera" a 0,1° y "Segunda" a 0,3°, y una
/// furgoneta a 100 km/h: los valores esperados se calculan a mano y no se copian de la implementación.
/// </summary>
public class TripTelemetryEventsTests
{
    private const double Tolerance = 1e-9;
    private const double SpeedKmh = 100;
    private const double FirstStopKm = 0.1 * KmPerDegree;
    private const double SecondStopKm = 0.3 * KmPerDegree;
    private const double TotalKm = 0.6 * KmPerDegree;

    private static (ActiveTrip Trip, RoutePath Path) Trip(DeliveryRoute? route = null, double mileageKm = 1005)
    {
        var assigned = route ?? EquatorRoute();
        var vehicle = EnRouteVehicle(VehicleType.Van, mileageKm: mileageKm);
        assigned.AssignedVehicleId = vehicle.Id;
        return (new ActiveTrip(assigned, vehicle), RoutePath.From(assigned));
    }

    private static IReadOnlyList<TelemetryEvent> Step((ActiveTrip Trip, RoutePath Path) trip, double previousKm, double currentKm)
        => TripTelemetryEvents.ForStep(trip.Trip, trip.Path, SpeedKmh, previousKm, currentKm, Telemetry.At);

    private static string[] Kinds(IEnumerable<TelemetryEvent> events)
        => events.Select(telemetry => telemetry switch
        {
            VehicleMovedEvent => "moved",
            TripStopReachedEvent stop => $"stop:{stop.StopLabel}",
            TripArrivedEvent => "arrived",
            _ => "other"
        }).ToArray();

    // ---------------- El movimiento ----------------

    [Fact]
    public void AStepWithoutStops_ProducesOnlyTheMovement()
    {
        var trip = Trip();

        var events = Step(trip, 0, 5);

        var moved = Assert.IsType<VehicleMovedEvent>(Assert.Single(events));
        Assert.Equal(trip.Trip.Vehicle.Id, moved.VehicleId);
        Assert.Equal(trip.Trip.Vehicle.LicensePlate, moved.LicensePlate);
        Assert.Equal(trip.Trip.Route.Id, moved.RouteId);
        Assert.Equal(Telemetry.At, moved.OccurredAt);
        Assert.Equal(5, moved.StepKm, Tolerance);
        Assert.Equal(1005, moved.MileageKm);                                   // el kilometraje que ya trae el vehículo
        Assert.Equal(5 / TotalKm * 100, moved.ProgressPercent, Tolerance);
        Assert.Equal("Primera", moved.NextStopLabel);
        Assert.Equal(TotalKm - 5, moved.RemainingKm, Tolerance);
        Assert.Equal((TotalKm - 5) / SpeedKmh * 60, moved.EtaMinutes, Tolerance);
        Assert.Equal(0, moved.Latitude, Tolerance);                            // sobre el ecuador
        Assert.Equal(5 / KmPerDegree, moved.Longitude, 1e-6);
    }

    [Fact]
    public void TheMovement_ComesFromTheSameCalculationAsTheProgressQuery()
    {
        var trip = Trip();
        var expected = trip.Path.ProgressAt(trip.Trip.Vehicle.Id, 20, SpeedKmh);

        var moved = Assert.IsType<VehicleMovedEvent>(Assert.Single(Step(trip, 15, 20)));   // a los 20 km ya pasó "Primera" (11,1 km) y falta "Segunda" (33,4 km)

        Assert.Equal(expected.Latitude, moved.Latitude);
        Assert.Equal(expected.Longitude, moved.Longitude);
        Assert.Equal(expected.Percent, moved.ProgressPercent);
        Assert.Equal(expected.NextStopLabel, moved.NextStopLabel);
        Assert.Equal(expected.RemainingKm, moved.RemainingKm);
        Assert.Equal(expected.EtaMinutes, moved.EtaMinutes);
        Assert.Equal("Segunda", moved.NextStopLabel);
    }

    [Fact]
    public void ATripThatDidNotMove_StillProducesTheMovement_WithZeroDistance()
    {
        var events = Step(Trip(), 0, 0);

        var moved = Assert.IsType<VehicleMovedEvent>(Assert.Single(events));
        Assert.Equal(0, moved.StepKm);
        Assert.Equal(0, moved.ProgressPercent);
    }

    [Fact]
    public void EveryEventOfAStep_CarriesTheInstantItWasGiven()
    {
        var instant = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var trip = Trip();

        var events = TripTelemetryEvents.ForStep(trip.Trip, trip.Path, SpeedKmh, 0, TotalKm, instant);

        Assert.True(events.Count >= 4);
        Assert.All(events, telemetry => Assert.Equal(instant, telemetry.OccurredAt));
    }

    // ---------------- Las paradas ----------------

    [Fact]
    public void AStepThatCrossesAStop_ReportsItAfterTheMovement()
    {
        var trip = Trip();

        var events = Step(trip, 5, 15);

        Assert.Equal(new[] { "moved", "stop:Primera" }, Kinds(events));
        var stop = Assert.IsType<TripStopReachedEvent>(events[1]);
        Assert.Equal(FirstStopKm, stop.DistanceCoveredKm, Tolerance);
        Assert.Equal(FirstStopKm / TotalKm * 100, stop.ProgressPercent, Tolerance);   // el avance EN la parada, no donde terminó el paso
        Assert.Equal(trip.Trip.Vehicle.Id, stop.VehicleId);
        Assert.Equal(trip.Trip.Route.Id, stop.RouteId);
        Assert.Equal(Telemetry.At, stop.OccurredAt);
    }

    [Fact]
    public void AStepThatEndsExactlyOnAStop_ReportsIt()
    {
        var trip = Trip();

        Assert.Equal(new[] { "moved", "stop:Primera" }, Kinds(Step(trip, 5, FirstStopKm)));
    }

    [Fact]
    public void AStepThatStartsExactlyOnAStop_DoesNotReportItAgain()
    {
        var trip = Trip();

        Assert.Equal(new[] { "moved" }, Kinds(Step(trip, FirstStopKm, FirstStopKm + 1)));   // ya se informó en el paso que la alcanzó
    }

    [Fact]
    public void ABigStepThatCrossesSeveralStops_ReportsEachOnce_InRouteOrder()
    {
        var trip = Trip();

        var events = Step(trip, 0, 40);

        Assert.Equal(new[] { "moved", "stop:Primera", "stop:Segunda" }, Kinds(events));
        Assert.Equal(SecondStopKm, ((TripStopReachedEvent)events[2]).DistanceCoveredKm, Tolerance);
    }

    [Fact]
    public void WalkingTheWholeRouteInSmallSteps_ReportsEachStopExactlyOnce()
    {
        var trip = Trip();
        var all = new List<TelemetryEvent>();
        const double stepKm = 3;

        for (var previous = 0.0; previous < TotalKm; previous += stepKm)
            all.AddRange(Step(trip, previous, Math.Min(previous + stepKm, TotalKm)));

        Assert.Equal(new[] { "Primera", "Segunda" }, all.OfType<TripStopReachedEvent>().Select(stop => stop.StopLabel));
        Assert.Single(all.OfType<TripArrivedEvent>());
    }

    [Fact]
    public void TheOriginAndTheDestination_AreNeverReportedAsStops()
    {
        var trip = Trip();

        var stops = Step(trip, 0, TotalKm).OfType<TripStopReachedEvent>().Select(stop => stop.StopLabel).ToArray();

        Assert.DoesNotContain(RoutePath.OriginLabel, stops);
        Assert.DoesNotContain(RoutePath.DestinationLabel, stops);
        Assert.Equal(new[] { "Primera", "Segunda" }, stops);
    }

    [Fact]
    public void AWaypointWithoutALabel_IsReportedWithTheFallbackLabel()
    {
        var route = EquatorRoute();
        route.Waypoints.Clear();
        route.Waypoints.Add(new Waypoint { Order = 0, Label = "  ", Location = PointAt(0, 0.20) });
        var trip = Trip(route);

        var stop = Assert.Single(Step(trip, 0, TotalKm).OfType<TripStopReachedEvent>());

        Assert.Equal("Parada 1", stop.StopLabel);
    }

    [Fact]
    public void ARouteWithoutWaypoints_HasNoStops_OnlyMovementAndArrival()
    {
        var route = EquatorRoute();
        route.Waypoints.Clear();
        var trip = Trip(route);

        Assert.Equal(new[] { "moved", "arrived" }, Kinds(Step(trip, 0, trip.Path.TotalKm)));
    }

    [Fact]
    public void ABentRoute_ReportsItsOwnStops_InTheOrderOfTheJourney()
    {
        var trip = Trip(BentRoute());

        var stops = Step(trip, 0, trip.Path.TotalKm).OfType<TripStopReachedEvent>().Select(stop => stop.StopLabel).ToArray();

        Assert.Equal(new[] { "Terminal de carga", "Puente" }, stops);
    }

    // ---------------- La llegada ----------------

    [Fact]
    public void ReachingTheDestination_AddsTheArrival_AfterTheMovement()
    {
        var trip = Trip();

        var events = Step(trip, 60, TotalKm);

        Assert.Equal(new[] { "moved", "arrived" }, Kinds(events));
        var moved = Assert.IsType<VehicleMovedEvent>(events[0]);
        Assert.Equal(100, moved.ProgressPercent, Tolerance);
        Assert.Equal(0, moved.RemainingKm, Tolerance);
        Assert.Equal(0, moved.EtaMinutes, Tolerance);
        Assert.Equal(RoutePath.DestinationLabel, moved.NextStopLabel);
        var arrived = Assert.IsType<TripArrivedEvent>(events[1]);
        Assert.Equal(TotalKm, arrived.TotalKm, Tolerance);
    }

    [Fact]
    public void AStepThatCrossesStopsAndArrives_ListsTheMovement_ThenTheStops_ThenTheArrival()
    {
        var trip = Trip();

        Assert.Equal(new[] { "moved", "stop:Primera", "stop:Segunda", "arrived" }, Kinds(Step(trip, 0, TotalKm)));
    }

    [Fact]
    public void BeforeTheDestination_ThereIsNoArrival()
    {
        var trip = Trip();

        Assert.DoesNotContain(Step(trip, 60, TotalKm - 0.001), telemetry => telemetry is TripArrivedEvent);
    }

    // ---------------- Función pura ----------------

    [Fact]
    public void TheSameStep_ProducesTheSameEvents()
    {
        var trip = Trip();

        Assert.Equal(Step(trip, 5, 40), Step(trip, 5, 40));
    }

    [Fact]
    public void ComputingTheEvents_DoesNotChangeTheTripNorTheVehicle()
    {
        var trip = Trip();
        var mileageBefore = trip.Trip.Vehicle.MileageKm;
        var statusBefore = trip.Trip.Vehicle.Status;
        var routeStatusBefore = trip.Trip.Route.Status;

        Step(trip, 0, TotalKm);

        Assert.Equal(mileageBefore, trip.Trip.Vehicle.MileageKm);
        Assert.Equal(statusBefore, trip.Trip.Vehicle.Status);
        Assert.Equal(routeStatusBefore, trip.Trip.Route.Status);
    }
}
