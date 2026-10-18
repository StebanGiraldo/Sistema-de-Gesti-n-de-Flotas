using FleetManagement.Application.Factories;
using FleetManagement.Application.Flyweights;
using FleetManagement.Application.Simulation;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Simulation;
using Xunit;
using static FleetManagement.Application.Tests.Simulation.SimulationTestData;

namespace FleetManagement.Application.Tests.Simulation;

/// <summary>
/// Pruebas del simulador de movimiento (<see cref="VehicleMovementSimulator"/>) con dobles de prueba: el tiempo se
/// entrega como parámetro, así que no se espera nada y cada resultado es exacto. Casi todas usan la ruta del ecuador,
/// donde cada distancia esperada se calcula a mano; la ruta con curvas comprueba que el vehículo no corta camino.
/// </summary>
public class VehicleMovementSimulatorTests
{
    private const double Tolerance = 1e-9;

    private static readonly TimeSpan SixMinutes = TimeSpan.FromMinutes(6); // 0,1 h

    // ---------------- Movimiento sobre la ruta ----------------

    [Theory]
    [InlineData(VehicleType.Truck, 80.0)]
    [InlineData(VehicleType.Van, 100.0)]
    [InlineData(VehicleType.Car, 120.0)]
    [InlineData(VehicleType.Motorcycle, 80.0)]
    public async Task Advance_MovesTheVehicleAlongTheRouteAtTheSpeedOfItsType(VehicleType type, double speedKmh)
    {
        var rig = new SimulatorRig();
        var vehicle = EnRouteVehicle(type);
        await rig.AddTripAsync(vehicle, EquatorRoute());

        await rig.Simulator.AdvanceAsync(SixMinutes);

        var expectedKm = speedKmh * 0.1;
        Assert.Equal(expectedKm / KmPerDegree, vehicle.CurrentLocation.X, Tolerance);
        Assert.Equal(0.0, vehicle.CurrentLocation.Y, Tolerance);
    }

    [Fact]
    public async Task Advance_TakesTheSpeedFromTheSharedTypeProfile()
    {
        var profiles = new StubProfileFactory(speedKmh: 50);
        var rig = new SimulatorRig(profiles);
        var route = EquatorRoute();
        await rig.AddTripAsync(EnRouteVehicle(VehicleType.Car), route);

        await rig.Simulator.AdvanceAsync(TimeSpan.FromHours(1));

        Assert.Equal(50.0, rig.CoveredKm(route), Tolerance);
        Assert.Equal(new[] { VehicleType.Car }, profiles.Requested.ToArray());
    }

    [Fact]
    public async Task Advance_KeepsTheVehicleOnTheRoutePolylineAtEveryStep_WithoutCuttingCorners()
    {
        var rig = new SimulatorRig();
        var vehicle = EnRouteVehicle(VehicleType.Van, 7.0806, -73.1716);
        var route = BentRoute();
        await rig.AddTripAsync(vehicle, route);
        var polyline = RoutePath.From(route).Stops.Select(stop => stop.Location).ToList();
        var chord = new[] { polyline[0], polyline[^1] };

        for (var step = 0; step < 10; step++)
            await rig.Simulator.AdvanceAsync(TimeSpan.FromSeconds(20)); // 0,56 km por paso: todavía no llega

        Assert.Equal(10, rig.Vehicles.Updates.Count);
        Assert.All(rig.Vehicles.Updates, update =>
            Assert.True(DistanceToPolylineInDegrees(polyline, update.Latitude, update.Longitude) < Tolerance,
                $"({update.Latitude}, {update.Longitude}) se salió de la ruta."));

        // Si el vehículo fuera en línea recta de origen a destino, esta distancia sería ~0: la ruta tiene una curva.
        var deviationFromChord = rig.Vehicles.Updates.Max(update => DistanceToPolylineInDegrees(chord, update.Latitude, update.Longitude));
        Assert.True(deviationFromChord > 1e-3, "El vehículo cortó camino en vez de seguir la ruta.");
        Assert.Empty(rig.RouteService.StatusChanges);
    }

    [Fact]
    public async Task Advance_NeverMovesBackwards()
    {
        var rig = new SimulatorRig();
        var route = BentRoute();
        await rig.AddTripAsync(EnRouteVehicle(VehicleType.Truck, 7.0806, -73.1716), route);
        var previousKm = -1.0;

        for (var step = 0; step < 8; step++)
        {
            await rig.Simulator.AdvanceAsync(TimeSpan.FromSeconds(15));
            var coveredKm = rig.CoveredKm(route);
            Assert.True(coveredKm > previousKm);
            previousKm = coveredKm;
        }
    }

    [Fact]
    public async Task Advance_FirstStepWithZeroTime_PlacesTheVehicleAtTheOriginWithoutAddingMileage()
    {
        var rig = new SimulatorRig();
        var vehicle = EnRouteVehicle(VehicleType.Van, latitude: 7.0, longitude: -73.0, mileageKm: 1000);
        var route = EquatorRoute();
        await rig.AddTripAsync(vehicle, route);

        await rig.Simulator.AdvanceAsync(TimeSpan.Zero);

        Assert.Equal(0.0, vehicle.CurrentLocation.X);
        Assert.Equal(0.0, vehicle.CurrentLocation.Y);
        Assert.Equal(0.0, rig.CoveredKm(route));
        Assert.Equal(1000.0, vehicle.MileageKm); // el salto al origen no es distancia recorrida
    }

    [Fact]
    public async Task Advance_ResumesFromTheStoredDistance_SoTwoShortStepsEqualOneLongStep()
    {
        var twoSteps = new SimulatorRig();
        var twoStepsVehicle = EnRouteVehicle(VehicleType.Van);
        var twoStepsRoute = EquatorRoute();
        await twoSteps.AddTripAsync(twoStepsVehicle, twoStepsRoute);
        var oneStep = new SimulatorRig();
        var oneStepVehicle = EnRouteVehicle(VehicleType.Van);
        var oneStepRoute = EquatorRoute();
        await oneStep.AddTripAsync(oneStepVehicle, oneStepRoute);

        await twoSteps.Simulator.AdvanceAsync(SixMinutes);
        await twoSteps.Simulator.AdvanceAsync(SixMinutes);
        await oneStep.Simulator.AdvanceAsync(TimeSpan.FromMinutes(12));

        Assert.Equal(oneStep.CoveredKm(oneStepRoute), twoSteps.CoveredKm(twoStepsRoute), Tolerance);
        Assert.Equal(oneStepVehicle.CurrentLocation.X, twoStepsVehicle.CurrentLocation.X, Tolerance);
    }

    [Fact]
    public async Task Advance_MovesEachTripIndependently_AtItsOwnTypeSpeed()
    {
        var rig = new SimulatorRig();
        var truckRoute = EquatorRoute();
        var carRoute = BentRoute();
        await rig.AddTripAsync(EnRouteVehicle(VehicleType.Truck), truckRoute);
        await rig.AddTripAsync(EnRouteVehicle(VehicleType.Car, 7.0806, -73.1716), carRoute);

        await rig.Simulator.AdvanceAsync(TimeSpan.FromMinutes(3)); // 0,05 h

        Assert.Equal(80.0 * 0.05, rig.CoveredKm(truckRoute), Tolerance);
        Assert.Equal(120.0 * 0.05, rig.CoveredKm(carRoute), Tolerance);
    }

    // ---------------- Velocidad: ruta retrasada y aceleración del tiempo ----------------

    [Fact]
    public async Task Advance_DelayedRoute_AdvancesSlowerThanARouteInProgress()
    {
        var rig = new SimulatorRig();
        var normal = EquatorRoute(DeliveryRouteStatus.InProgress);
        var delayed = EquatorRoute(DeliveryRouteStatus.Delayed);
        await rig.AddTripAsync(EnRouteVehicle(VehicleType.Van), normal);
        await rig.AddTripAsync(EnRouteVehicle(VehicleType.Van), delayed);

        await rig.Simulator.AdvanceAsync(SixMinutes);

        Assert.Equal(10.0, rig.CoveredKm(normal), Tolerance);
        Assert.Equal(10.0 * rig.Options.DelayedSpeedFactor, rig.CoveredKm(delayed), Tolerance);
        Assert.True(rig.CoveredKm(delayed) < rig.CoveredKm(normal));
    }

    [Fact]
    public async Task Advance_TimeAcceleration_MultipliesTheDistanceCovered()
    {
        var rig = new SimulatorRig();
        rig.Options.TimeAcceleration = 10;
        var route = EquatorRoute();
        await rig.AddTripAsync(EnRouteVehicle(VehicleType.Van), route);

        await rig.Simulator.AdvanceAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(100.0 * 10 * (30.0 / 3600.0), rig.CoveredKm(route), Tolerance);
    }

    // ---------------- Kilometraje ----------------

    [Fact]
    public async Task Advance_AddsToTheMileageExactlyTheDistanceCoveredOnTheRoute()
    {
        var rig = new SimulatorRig();
        var vehicle = EnRouteVehicle(VehicleType.Van, mileageKm: 1000);
        var route = EquatorRoute();
        await rig.AddTripAsync(vehicle, route);
        var totalKm = RoutePath.From(route).TotalKm;

        await rig.Simulator.AdvanceAsync(TimeSpan.FromMinutes(25)); // 41,67 km
        Assert.Equal(1000 + 100.0 * 25 / 60, vehicle.MileageKm, Tolerance);

        await rig.Simulator.AdvanceAsync(TimeSpan.FromMinutes(25)); // el segundo paso llegaría a 83 km, pero la ruta termina en 66,7
        Assert.Equal(1000 + totalKm, vehicle.MileageKm, Tolerance);
    }

    // ---------------- Llegada ----------------

    [Fact]
    public async Task Advance_OnArrival_PlacesTheVehicleAtTheExactDestination_AndCompletesTheRouteOnce()
    {
        var rig = new SimulatorRig();
        var vehicle = EnRouteVehicle(VehicleType.Van);
        var route = EquatorRoute();
        await rig.AddTripAsync(vehicle, route);

        await rig.Simulator.AdvanceAsync(TimeSpan.FromHours(1)); // 100 km: sobra distancia

        Assert.Equal(0.60, vehicle.CurrentLocation.X);
        Assert.Equal(0.0, vehicle.CurrentLocation.Y);
        var change = Assert.Single(rig.RouteService.StatusChanges);
        Assert.Equal((route.Id, "Completed"), change);
        Assert.False(rig.Tracker.TryGetCoveredKm(route.Id, out _));
    }

    [Fact]
    public async Task Advance_OnArrival_SavesTheFinalPositionBeforeCompletingTheRoute()
    {
        var rig = new SimulatorRig();
        await rig.AddTripAsync(EnRouteVehicle(VehicleType.Van), EquatorRoute());

        await rig.Simulator.AdvanceAsync(TimeSpan.FromHours(1));

        // Completar la ruta vuelve a leer el vehículo para liberarlo: si se guardara después, se pisaría ese cambio.
        Assert.Equal(new[] { "vehicle.update", "route.status:Completed" }, rig.Log.Events.ToArray());
    }

    [Fact]
    public async Task Advance_OnArrival_WithTheRealRouteService_FreesTheVehicleAndAuditsWithoutDuplicatingThatLogic()
    {
        var fleet = new RealFleet();
        var vehicle = EnRouteVehicle(VehicleType.Van);
        var route = EquatorRoute();
        route.AssignedVehicleId = vehicle.Id;
        await fleet.VehicleRepository.AddAsync(vehicle);
        await fleet.RouteRepository.AddAsync(route);
        var simulator = new VehicleMovementSimulator(
            fleet.RouteRepository, fleet.VehicleRepository, fleet.Routes, new InMemoryTripProgressTracker(),
            new VehicleTypeProfileFactory(new FleetOnboardingFactoryProvider()), new SimulationOptions());

        await simulator.AdvanceAsync(TimeSpan.FromHours(1));

        Assert.Equal(DeliveryRouteStatus.Completed, route.Status);
        Assert.Equal(VehicleStatus.Available, vehicle.Status);
        Assert.Contains(fleet.Audit.Entries, entry => entry.Category == "Ruta" && entry.Message.Contains("Completed"));

        // Ya liberado, el vehículo deja de ser parte de un viaje: no se mueve más ni suma kilómetros.
        var mileageAtArrival = vehicle.MileageKm;
        await simulator.AdvanceAsync(TimeSpan.FromHours(1));
        Assert.Equal(mileageAtArrival, vehicle.MileageKm);
        Assert.Equal(0.60, vehicle.CurrentLocation.X);
    }

    // ---------------- Lo que NO debe moverse ----------------

    [Fact]
    public async Task Advance_VehicleEnRouteWithoutAnyRoute_DoesNotMove()
    {
        var rig = new SimulatorRig();
        var vehicle = EnRouteVehicle(VehicleType.Van, latitude: 7.1, longitude: -73.1, mileageKm: 500);
        await rig.Vehicles.AddAsync(vehicle);

        await rig.Simulator.AdvanceAsync(TimeSpan.FromHours(1));

        AssertDidNotMove(rig, vehicle, 7.1, -73.1, 500);
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.Planned)]
    [InlineData(DeliveryRouteStatus.Completed)]
    [InlineData(DeliveryRouteStatus.Cancelled)]
    public async Task Advance_VehicleEnRouteWhoseRouteIsNotActive_DoesNotMove(DeliveryRouteStatus routeStatus)
    {
        var rig = new SimulatorRig();
        var vehicle = EnRouteVehicle(VehicleType.Van, latitude: 7.1, longitude: -73.1, mileageKm: 500);
        await rig.AddTripAsync(vehicle, EquatorRoute(routeStatus));

        await rig.Simulator.AdvanceAsync(TimeSpan.FromHours(1));

        AssertDidNotMove(rig, vehicle, 7.1, -73.1, 500);
    }

    [Fact]
    public async Task Advance_ActiveRouteAssignedToAnotherVehicle_DoesNotMoveTheIdleOne()
    {
        var rig = new SimulatorRig();
        var idle = EnRouteVehicle(VehicleType.Van, latitude: 7.1, longitude: -73.1, mileageKm: 500);
        await rig.Vehicles.AddAsync(idle);
        await rig.AddTripAsync(EnRouteVehicle(VehicleType.Van), EquatorRoute());

        await rig.Simulator.AdvanceAsync(TimeSpan.FromHours(1));

        Assert.Equal(7.1, idle.CurrentLocation.Y);
        Assert.Equal(-73.1, idle.CurrentLocation.X);
        Assert.Equal(500.0, idle.MileageKm);
    }

    [Theory]
    [InlineData(VehicleStatus.Available)]
    [InlineData(VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService)]
    public async Task Advance_VehicleNotEnRoute_DoesNotMoveEvenWithAnActiveRoute(VehicleStatus vehicleStatus)
    {
        var rig = new SimulatorRig();
        var vehicle = EnRouteVehicle(VehicleType.Van, latitude: 7.1, longitude: -73.1, mileageKm: 500);
        vehicle.Status = vehicleStatus;
        await rig.AddTripAsync(vehicle, EquatorRoute());

        await rig.Simulator.AdvanceAsync(TimeSpan.FromHours(1));

        AssertDidNotMove(rig, vehicle, 7.1, -73.1, 500);
    }

    // ---------------- Cambios manuales, validaciones y cancelación ----------------

    [Fact]
    public async Task Advance_WhenTheRouteStopsBeingActiveManually_ClearsItsProgressAndStopsMovingIt()
    {
        var rig = new SimulatorRig();
        var vehicle = EnRouteVehicle(VehicleType.Van);
        var route = EquatorRoute();
        await rig.AddTripAsync(vehicle, route);
        await rig.Simulator.AdvanceAsync(SixMinutes);
        Assert.True(rig.Tracker.TryGetCoveredKm(route.Id, out _));
        var positionBefore = vehicle.CurrentLocation.X;

        route.Status = DeliveryRouteStatus.Cancelled; // cambio manual desde la API
        await rig.Simulator.AdvanceAsync(SixMinutes);

        Assert.False(rig.Tracker.TryGetCoveredKm(route.Id, out _));
        Assert.Equal(positionBefore, vehicle.CurrentLocation.X);
        Assert.Empty(rig.RouteService.StatusChanges); // el simulador no completa una ruta que ya no está activa
    }

    [Fact]
    public async Task Advance_NegativeElapsedTime_IsRejected()
    {
        var rig = new SimulatorRig();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => rig.Simulator.AdvanceAsync(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public async Task Advance_WhenCancelled_ThrowsBeforeMovingAnything()
    {
        var rig = new SimulatorRig();
        var vehicle = EnRouteVehicle(VehicleType.Van);
        await rig.AddTripAsync(vehicle, EquatorRoute());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => rig.Simulator.AdvanceAsync(SixMinutes, cancellation.Token));

        Assert.Empty(rig.Vehicles.Updates);
    }

    private static void AssertDidNotMove(SimulatorRig rig, Vehicle vehicle, double latitude, double longitude, double mileageKm)
    {
        Assert.Equal(latitude, vehicle.CurrentLocation.Y);
        Assert.Equal(longitude, vehicle.CurrentLocation.X);
        Assert.Equal(mileageKm, vehicle.MileageKm);
        Assert.Empty(rig.Vehicles.Updates);
        Assert.Empty(rig.RouteService.StatusChanges);
    }
}
