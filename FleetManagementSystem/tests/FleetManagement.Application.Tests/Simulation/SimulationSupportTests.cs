using System.ComponentModel.DataAnnotations;
using FleetManagement.Application.Flyweights;
using FleetManagement.Application.Simulation;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Simulation;
using Xunit;
using static FleetManagement.Application.Tests.Simulation.SimulationTestData;

namespace FleetManagement.Application.Tests.Simulation;

/// <summary>
/// La velocidad de un viaje se calcula en un solo lugar (<see cref="TripSpeed"/>): la usan el simulador y el servicio de
/// progreso, así que si esta cuenta cambia, la ETA que ve el usuario y el movimiento real cambian juntos.
/// </summary>
public class TripSpeedTests
{
    private const double Tolerance = 1e-9;

    private static readonly VehicleTypeProfile VanProfile =
        new(VehicleType.Van, "Perfil de prueba", 100, false, false, false, Array.Empty<MaintenanceTaskTemplate>());

    [Fact]
    public void KmPerHour_ForARouteOnTime_IsTheProfileSpeedTimesTheAcceleration()
    {
        var options = new SimulationOptions { TimeAcceleration = 3 };

        Assert.Equal(300.0, TripSpeed.KmPerHour(VanProfile, DeliveryRouteStatus.InProgress, options), Tolerance);
    }

    [Fact]
    public void KmPerHour_ForADelayedRoute_AppliesTheDelayedFactor()
    {
        var options = new SimulationOptions { TimeAcceleration = 2, DelayedSpeedFactor = 0.25 };

        Assert.Equal(50.0, TripSpeed.KmPerHour(VanProfile, DeliveryRouteStatus.Delayed, options), Tolerance);
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.Planned)]
    [InlineData(DeliveryRouteStatus.InProgress)]
    [InlineData(DeliveryRouteStatus.Completed)]
    [InlineData(DeliveryRouteStatus.Cancelled)]
    public void KmPerHour_OnlyADelayedRouteIsSlowedDown(DeliveryRouteStatus status)
    {
        Assert.Equal(100.0, TripSpeed.KmPerHour(VanProfile, status, new SimulationOptions()), Tolerance);
    }
}

/// <summary>
/// Qué es un "viaje activo" se define una sola vez (<see cref="ActiveTrips"/>): un vehículo EnRoute con su ruta InProgress
/// o Delayed. Estas pruebas fijan esa definición, que comparten el simulador y el servicio de progreso.
/// </summary>
public class ActiveTripsTests
{
    [Fact]
    public void Find_PairsEachEnRouteVehicleWithItsOwnActiveRoute()
    {
        var van = EnRouteVehicle(VehicleType.Van);
        var car = EnRouteVehicle(VehicleType.Car);
        var vanRoute = EquatorRoute(DeliveryRouteStatus.InProgress, van.Id);
        var carRoute = BentRoute(DeliveryRouteStatus.Delayed, car.Id);

        var trips = ActiveTrips.Find(new[] { carRoute, vanRoute }, new[] { van, car });

        Assert.Equal(2, trips.Count);
        Assert.Same(vanRoute, trips.Single(trip => trip.Vehicle == van).Route);
        Assert.Same(carRoute, trips.Single(trip => trip.Vehicle == car).Route);
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.Planned)]
    [InlineData(DeliveryRouteStatus.Completed)]
    [InlineData(DeliveryRouteStatus.Cancelled)]
    public void Find_IgnoresRoutesThatAreNotActive(DeliveryRouteStatus status)
    {
        var van = EnRouteVehicle(VehicleType.Van);

        Assert.Empty(ActiveTrips.Find(new[] { EquatorRoute(status, van.Id) }, new[] { van }));
    }

    [Theory]
    [InlineData(VehicleStatus.Available)]
    [InlineData(VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService)]
    public void Find_IgnoresVehiclesThatAreNotEnRoute(VehicleStatus status)
    {
        var van = EnRouteVehicle(VehicleType.Van);
        van.Status = status;

        Assert.Empty(ActiveTrips.Find(new[] { EquatorRoute(DeliveryRouteStatus.InProgress, van.Id) }, new[] { van }));
    }

    [Fact]
    public void Find_IgnoresAnEnRouteVehicleWithoutAnActiveRoute()
    {
        var van = EnRouteVehicle(VehicleType.Van);
        var someoneElsesRoute = EquatorRoute(DeliveryRouteStatus.InProgress, Guid.NewGuid());
        var unassignedRoute = EquatorRoute(DeliveryRouteStatus.InProgress, null);

        Assert.Empty(ActiveTrips.Find(new[] { someoneElsesRoute, unassignedRoute }, new[] { van }));
    }

    [Fact]
    public void Find_WithTwoActiveRoutesForTheSameVehicle_FollowsTheFirstOne()
    {
        var van = EnRouteVehicle(VehicleType.Van);
        var first = EquatorRoute(DeliveryRouteStatus.InProgress, van.Id);
        var second = BentRoute(DeliveryRouteStatus.InProgress, van.Id);

        var trip = Assert.Single(ActiveTrips.Find(new[] { first, second }, new[] { van }));

        Assert.Same(first, trip.Route);
    }
}

/// <summary>Los parámetros de la simulación se validan al arrancar (DataAnnotations): un valor imposible detiene el arranque.</summary>
public class SimulationOptionsTests
{
    [Fact]
    public void TheDefaults_AreTheDocumentedOnes_AndAreValid()
    {
        var options = new SimulationOptions();

        Assert.Equal("Simulation", SimulationOptions.SectionName);
        Assert.Equal(1.0, options.TickSeconds);
        Assert.Equal(1.0, options.TimeAcceleration);
        Assert.Equal(0.5, options.DelayedSpeedFactor);
        Assert.Empty(Validate(options));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void ATickThatIsNotPositive_IsInvalid(double tickSeconds)
        => AssertInvalid(new SimulationOptions { TickSeconds = tickSeconds }, nameof(SimulationOptions.TickSeconds));

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    public void AnAccelerationThatIsNotPositive_IsInvalid(double acceleration)
        => AssertInvalid(new SimulationOptions { TimeAcceleration = acceleration }, nameof(SimulationOptions.TimeAcceleration));

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.01)]
    public void ADelayedFactorOutsideZeroExclusiveAndOne_IsInvalid(double factor)
        => AssertInvalid(new SimulationOptions { DelayedSpeedFactor = factor }, nameof(SimulationOptions.DelayedSpeedFactor));

    [Theory]
    [InlineData(0.01)]
    [InlineData(1.0)]
    public void ADelayedFactorUpToOne_IsValid(double factor)
        => Assert.Empty(Validate(new SimulationOptions { DelayedSpeedFactor = factor }));

    private static void AssertInvalid(SimulationOptions options, string member)
    {
        var failure = Assert.Single(Validate(options));
        Assert.Contains(member, failure.MemberNames);
    }

    private static List<ValidationResult> Validate(SimulationOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }
}

/// <summary>Seguimiento del avance de los viajes en memoria: una distancia por ruta, consultable y borrable, segura entre hilos.</summary>
public class InMemoryTripProgressTrackerTests
{
    [Fact]
    public void ARouteWithoutRecordedProgress_IsNotFound()
    {
        var tracker = new InMemoryTripProgressTracker();

        Assert.False(tracker.TryGetCoveredKm(Guid.NewGuid(), out var coveredKm));
        Assert.Equal(0.0, coveredKm);
    }

    [Fact]
    public void SetCoveredKm_StoresTheDistance_AndASecondValueReplacesIt()
    {
        var tracker = new InMemoryTripProgressTracker();
        var routeId = Guid.NewGuid();

        tracker.SetCoveredKm(routeId, 2.5);
        tracker.SetCoveredKm(routeId, 4.0);

        Assert.True(tracker.TryGetCoveredKm(routeId, out var coveredKm));
        Assert.Equal(4.0, coveredKm);
    }

    [Fact]
    public void Clear_ForgetsOnlyThatRoute_AndClearingAnUnknownRouteIsHarmless()
    {
        var tracker = new InMemoryTripProgressTracker();
        var cleared = Guid.NewGuid();
        var kept = Guid.NewGuid();
        tracker.SetCoveredKm(cleared, 1);
        tracker.SetCoveredKm(kept, 2);

        tracker.Clear(cleared);
        tracker.Clear(Guid.NewGuid());

        Assert.False(tracker.TryGetCoveredKm(cleared, out _));
        Assert.True(tracker.TryGetCoveredKm(kept, out _));
    }

    [Fact]
    public void ClearExcept_ForgetsEveryRouteThatIsNotInTheKeepSet()
    {
        var tracker = new InMemoryTripProgressTracker();
        var kept = Guid.NewGuid();
        var other = Guid.NewGuid();
        tracker.SetCoveredKm(kept, 1);
        tracker.SetCoveredKm(other, 2);

        tracker.ClearExcept(new HashSet<Guid> { kept });

        Assert.True(tracker.TryGetCoveredKm(kept, out _));
        Assert.False(tracker.TryGetCoveredKm(other, out _));
    }

    [Fact]
    public void ClearExcept_WithAnEmptySet_ForgetsEverything()
    {
        var tracker = new InMemoryTripProgressTracker();
        var routeId = Guid.NewGuid();
        tracker.SetCoveredKm(routeId, 1);

        tracker.ClearExcept(new HashSet<Guid>());

        Assert.False(tracker.TryGetCoveredKm(routeId, out _));
    }

    [Fact]
    public void EachTracker_HasItsOwnState_ThereIsNothingStatic()
    {
        var first = new InMemoryTripProgressTracker();
        var second = new InMemoryTripProgressTracker();
        var routeId = Guid.NewGuid();

        first.SetCoveredKm(routeId, 1);

        Assert.False(second.TryGetCoveredKm(routeId, out _));
    }

    [Fact]
    public void ConcurrentWritesAndClears_DoNotCorruptTheTracker()
    {
        var tracker = new InMemoryTripProgressTracker();
        var routeIds = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToArray();

        Parallel.For(0, 20_000, index =>
        {
            var routeId = routeIds[index % routeIds.Length];
            tracker.SetCoveredKm(routeId, index);
            if (index % 7 == 0) tracker.Clear(routeId);
            if (index % 101 == 0) tracker.ClearExcept(new HashSet<Guid>(routeIds));
        });

        foreach (var routeId in routeIds)
            tracker.SetCoveredKm(routeId, 1);
        Assert.All(routeIds, routeId => Assert.True(tracker.TryGetCoveredKm(routeId, out var covered) && covered == 1));
    }
}
