using FleetManagement.Application.Simulation;
using FleetManagement.Application.Tests.Simulation;
using FleetManagement.Application.Tests.Visitor;
using FleetManagement.Domain.Enums;
using FleetManagement.Domain.States;
using Xunit;

namespace FleetManagement.Application.Tests.State;

/// <summary>
/// STATE en el seguimiento de los viajes: solo un vehículo "En ruta" sigue su viaje (<c>CanTrackTrip</c>). Lo usan la búsqueda de
/// viajes activos (de la que viven el simulador y la consulta de progreso) y, por ellos, el movimiento de los vehículos.
/// </summary>
public class VehicleStateTripTrackingTests
{
    private static readonly TimeSpan SixMinutes = TimeSpan.FromMinutes(6);

    // ---------------- Búsqueda de viajes activos ----------------

    [Theory]
    [InlineData(VehicleStatus.Available, DeliveryRouteStatus.InProgress, false)]
    [InlineData(VehicleStatus.Available, DeliveryRouteStatus.Delayed, false)]
    [InlineData(VehicleStatus.EnRoute, DeliveryRouteStatus.InProgress, true)]
    [InlineData(VehicleStatus.EnRoute, DeliveryRouteStatus.Delayed, true)]
    [InlineData(VehicleStatus.Maintenance, DeliveryRouteStatus.InProgress, false)]
    [InlineData(VehicleStatus.Maintenance, DeliveryRouteStatus.Delayed, false)]
    [InlineData(VehicleStatus.OutOfService, DeliveryRouteStatus.InProgress, false)]
    [InlineData(VehicleStatus.OutOfService, DeliveryRouteStatus.Delayed, false)]
    [InlineData(VehicleStatus.EnRoute, DeliveryRouteStatus.Planned, false)]
    [InlineData(VehicleStatus.EnRoute, DeliveryRouteStatus.Completed, false)]
    [InlineData(VehicleStatus.EnRoute, DeliveryRouteStatus.Cancelled, false)]
    public void ATrip_IsFollowedOnlyWhileTheRouteIsActive_AndTheVehicleIsEnRoute(VehicleStatus vehicleStatus, DeliveryRouteStatus routeStatus, bool followed)
    {
        var vehicle = FleetData.Vehicle("VAN-001", status: vehicleStatus);
        var route = FleetData.Route("Ruta", routeStatus, vehicle);

        var trips = ActiveTrips.Find(new[] { route }, new[] { vehicle });

        Assert.Equal(followed, trips.Count == 1);
    }

    // ---------------- El movimiento ----------------

    [Theory]
    [InlineData(VehicleStatus.Available)]
    [InlineData(VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService)]
    public async Task AVehicleThatIsNotEnRoute_IsNotMoved_EvenWithAnActiveRoute(VehicleStatus status)
    {
        var rig = new SimulatorRig();
        var trip = await rig.AddTripAsync(VehicleType.Van, vehicleStatus: status);
        var mileage = trip.Vehicle.MileageKm;

        await rig.AdvanceAsync(SixMinutes);

        Assert.Empty(rig.Vehicles.Updates);
        Assert.Equal(mileage, trip.Vehicle.MileageKm);
        Assert.Empty(rig.RouteService.StatusChanges);
    }

    [Fact]
    public async Task APersonPuttingTheVehicleInMaintenance_StopsItsTrip_AndSendingItBackEnRouteMovesItAgain()
    {
        var rig = new SimulatorRig();
        var trip = await rig.AddTripAsync(VehicleType.Van);
        var context = new VehicleStateContext(trip.Vehicle);

        await rig.AdvanceAsync(SixMinutes);
        var movedBefore = rig.Vehicles.Updates.Count;
        Assert.Equal(1, movedBefore);

        context.ChangeTo(VehicleStatus.Maintenance);                  // se lo llevan al taller
        await rig.AdvanceAsync(SixMinutes);
        await rig.AdvanceAsync(SixMinutes);
        Assert.Equal(movedBefore, rig.Vehicles.Updates.Count);        // mientras esté allí no se mueve

        context.ChangeTo(VehicleStatus.Available);                    // por Disponible: el estado no deja pasar de Mantenimiento a En ruta
        context.ChangeTo(VehicleStatus.EnRoute);
        await rig.AdvanceAsync(SixMinutes);
        Assert.Equal(movedBefore + 1, rig.Vehicles.Updates.Count);    // vuelve a seguir su viaje
    }

    [Fact]
    public async Task TheProgressQuery_AndTheSimulator_AgreeOnWhichTripsExist()
    {
        // Los dos usan ActiveTrips: lo que el simulador mueve es exactamente lo que la consulta de progreso muestra.
        var rig = new SimulatorRig();
        await rig.AddTripAsync(VehicleType.Van);
        await rig.AddTripAsync(VehicleType.Car, vehicleStatus: VehicleStatus.Maintenance);
        await rig.AddTripAsync(VehicleType.Truck, vehicleStatus: VehicleStatus.Available);
        var progress = new FleetManagement.Application.Services.TripProgressService(rig.Routes, rig.Vehicles, rig.Tracker, rig.Profiles, rig.Options);

        await rig.AdvanceAsync(SixMinutes);

        Assert.Equal(1, rig.Vehicles.Updates.Count);
        Assert.Single(await progress.GetActiveTripsAsync());
    }
}
