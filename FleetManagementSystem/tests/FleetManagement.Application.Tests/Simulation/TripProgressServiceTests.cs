using FleetManagement.Application.Services;
using FleetManagement.Domain.Enums;
using Xunit;
using static FleetManagement.Application.Tests.Simulation.SimulationTestData;

namespace FleetManagement.Application.Tests.Simulation;

/// <summary>
/// Pruebas del servicio de progreso: lo que el frontend muestra (porcentaje, kilómetros restantes, próxima parada y ETA)
/// debe salir de las mismas reglas que mueven los vehículos, y la consulta no puede crecer con el número de viajes.
/// Ruta usada casi siempre: la del ecuador (66,717 km; paradas "Primera" a 11,1 km y "Segunda" a 33,4 km) con una furgoneta
/// (100 km/h), de modo que cada valor esperado se calcula a mano.
/// </summary>
public class TripProgressServiceTests
{
    private const double Tolerance = 1e-9;
    private const double RouteKm = 0.6 * KmPerDegree;

    [Fact]
    public async Task GetActiveTrips_WithoutTrips_IsEmpty()
    {
        var rig = new SimulatorRig();

        Assert.Empty(await CreateService(rig).GetActiveTripsAsync());
    }

    [Fact]
    public async Task GetActiveTrips_ReturnsOnlyTheTripsThatAreActive()
    {
        var rig = new SimulatorRig();
        var onTime = await rig.AddTripAsync(VehicleType.Van, routeStatus: DeliveryRouteStatus.InProgress);
        var delayed = await rig.AddTripAsync(VehicleType.Car, BentRoute(), DeliveryRouteStatus.Delayed);
        await rig.AddTripAsync(VehicleType.Truck, routeStatus: DeliveryRouteStatus.Planned);
        await rig.AddTripAsync(VehicleType.Truck, routeStatus: DeliveryRouteStatus.Completed);
        await rig.AddTripAsync(VehicleType.Motorcycle, vehicleStatus: VehicleStatus.Available);

        var trips = await CreateService(rig).GetActiveTripsAsync();

        Assert.Equal(
            new[] { onTime.Route.Id, delayed.Route.Id }.OrderBy(id => id).ToArray(),
            trips.Select(trip => trip.RouteId).OrderBy(id => id).ToArray());
    }

    [Fact]
    public async Task GetActiveTrips_IdentifiesTheRouteAndTheVehicle()
    {
        var rig = new SimulatorRig();
        var trip = await rig.AddTripAsync(VehicleType.Van, routeStatus: DeliveryRouteStatus.Delayed);

        var dto = Assert.Single(await CreateService(rig).GetActiveTripsAsync());

        Assert.Equal(trip.Route.Id, dto.RouteId);
        Assert.Equal(trip.Route.Name, dto.RouteName);
        Assert.Equal(trip.Vehicle.Id, dto.VehicleId);
        Assert.Equal(trip.Vehicle.LicensePlate, dto.LicensePlate);
        Assert.Equal("Delayed", dto.RouteStatus);
    }

    [Fact]
    public async Task GetActiveTrips_ReportsCoveredDistancePercentRemainingNextStopAndEta()
    {
        var rig = new SimulatorRig();
        var trip = await rig.AddTripAsync(VehicleType.Van);
        rig.Tracker.SetCoveredKm(trip.Route.Id, 25.0);

        var dto = Assert.Single(await CreateService(rig).GetActiveTripsAsync());

        Assert.Equal(25.0, dto.DistanceCoveredKm, Tolerance);
        Assert.Equal(Math.Round(RouteKm, 3), dto.TotalKm, Tolerance);
        Assert.Equal(Math.Round(25.0 / RouteKm * 100, 1), dto.Percent, Tolerance);
        Assert.Equal(Math.Round(RouteKm - 25.0, 3), dto.RemainingKm, Tolerance);
        Assert.Equal(Math.Round((RouteKm - 25.0) / 100.0 * 60, 1), dto.EtaMinutes, Tolerance);
        Assert.Equal("Segunda", dto.NextStopLabel);
        Assert.False(dto.Arrived);
    }

    [Fact]
    public async Task GetActiveTrips_ReportsThePositionOnTheRoute()
    {
        var rig = new SimulatorRig();
        var trip = await rig.AddTripAsync(VehicleType.Van);
        rig.Tracker.SetCoveredKm(trip.Route.Id, 25.0);

        var dto = Assert.Single(await CreateService(rig).GetActiveTripsAsync());

        Assert.Equal(Math.Round(25.0 / KmPerDegree, 6), dto.Longitude, Tolerance);
        Assert.Equal(0.0, dto.Latitude, Tolerance);
    }

    [Fact]
    public async Task GetActiveTrips_IncludesTheStopsOfTheRouteInOrder_SoTheMapCanDrawIt()
    {
        var rig = new SimulatorRig();
        await rig.AddTripAsync(VehicleType.Van);

        var dto = Assert.Single(await CreateService(rig).GetActiveTripsAsync());

        Assert.Equal(new[] { "Origen", "Primera", "Segunda", "Destino" }, dto.Stops.Select(stop => stop.Label).ToArray());
        Assert.Equal(new[] { 0.00, 0.10, 0.30, 0.60 }, dto.Stops.Select(stop => stop.Longitude).ToArray());
        Assert.All(dto.Stops, stop => Assert.Equal(0.0, stop.Latitude));
    }

    [Fact]
    public async Task GetActiveTrips_BeforeTheFirstStep_ReportsTheStartOfTheRoute()
    {
        var rig = new SimulatorRig();
        await rig.AddTripAsync(VehicleType.Van);

        var dto = Assert.Single(await CreateService(rig).GetActiveTripsAsync());

        Assert.Equal(0.0, dto.DistanceCoveredKm);
        Assert.Equal(0.0, dto.Percent);
        Assert.Equal(0.0, dto.Longitude);
        Assert.Equal("Primera", dto.NextStopLabel);
        Assert.Equal(Math.Round(RouteKm, 3), dto.RemainingKm, Tolerance);
    }

    [Fact]
    public async Task GetActiveTrips_ADelayedTripHasALongerEta_BecauseItMovesSlower()
    {
        var rig = new SimulatorRig();
        var onTime = await rig.AddTripAsync(VehicleType.Van, routeStatus: DeliveryRouteStatus.InProgress);
        var delayed = await rig.AddTripAsync(VehicleType.Van, routeStatus: DeliveryRouteStatus.Delayed);

        var trips = await CreateService(rig).GetActiveTripsAsync();

        var onTimeEta = trips.Single(trip => trip.RouteId == onTime.Route.Id).EtaMinutes;
        var delayedEta = trips.Single(trip => trip.RouteId == delayed.Route.Id).EtaMinutes;
        Assert.Equal(Math.Round(RouteKm / 100.0 * 60, 1), onTimeEta, Tolerance);
        Assert.Equal(Math.Round(RouteKm / (100.0 * rig.Options.DelayedSpeedFactor) * 60, 1), delayedEta, Tolerance);
        Assert.True(delayedEta > onTimeEta);
    }

    [Fact]
    public async Task GetActiveTrips_TheEtaIsTheTimeTheSimulationReallyNeedsToArrive()
    {
        // Con aceleración y retraso a la vez, la velocidad efectiva es 100 km/h x 0,5 x 3 = 150 km/h.
        var estimator = await DelayedTripWithBoostedSpeedAsync();
        var etaMinutes = Assert.Single(await CreateService(estimator).GetActiveTripsAsync()).EtaMinutes;
        var tooEarly = await DelayedTripWithBoostedSpeedAsync();
        var onTime = await DelayedTripWithBoostedSpeedAsync();

        await tooEarly.AdvanceAsync(TimeSpan.FromMinutes(etaMinutes - 0.2));
        await onTime.AdvanceAsync(TimeSpan.FromMinutes(etaMinutes + 0.2));

        Assert.Equal(Math.Round(RouteKm / 150.0 * 60, 1), etaMinutes, Tolerance);
        Assert.Empty(tooEarly.RouteService.StatusChanges);
        Assert.Single(onTime.RouteService.StatusChanges);
    }

    [Fact]
    public async Task GetActiveTrips_ReadsEachRepositoryOnce_NoMatterHowManyTripsThereAre()
    {
        var rig = new SimulatorRig();
        for (var count = 0; count < 5; count++)
            await rig.AddTripAsync(VehicleType.Van);

        var trips = await CreateService(rig).GetActiveTripsAsync();

        Assert.Equal(5, trips.Count);
        Assert.Equal(1, rig.Routes.GetAllCalls);
        Assert.Equal(1, rig.Vehicles.GetAllCalls);
        Assert.Equal(0, rig.Routes.GetByIdCalls);
        Assert.Equal(0, rig.Vehicles.GetByIdCalls);
    }

    [Fact]
    public async Task GetActiveTrips_OnlyReads_ItNeitherMovesVehiclesNorRecordsProgress()
    {
        var rig = new SimulatorRig();
        var trip = await rig.AddTripAsync(VehicleType.Van);

        await CreateService(rig).GetActiveTripsAsync();

        Assert.Empty(rig.Vehicles.Updates);
        Assert.False(rig.Tracker.TryGetCoveredKm(trip.Route.Id, out _));
    }

    [Fact]
    public async Task GetActiveTrips_AgreesWithWhereTheSimulatorPutTheVehicle()
    {
        var rig = new SimulatorRig();
        var trip = await rig.AddTripAsync(VehicleType.Van, BentRoute());
        await rig.AdvanceAsync(TimeSpan.FromMinutes(1));

        var dto = Assert.Single(await CreateService(rig).GetActiveTripsAsync());

        Assert.Equal(Math.Round(rig.CoveredKm(trip), 3), dto.DistanceCoveredKm, Tolerance);
        Assert.Equal(trip.Vehicle.CurrentLocation.Y, dto.Latitude, 1e-6);
        Assert.Equal(trip.Vehicle.CurrentLocation.X, dto.Longitude, 1e-6);
    }

    private static TripProgressService CreateService(SimulatorRig rig)
        => new(rig.Routes, rig.Vehicles, rig.Tracker, rig.Profiles, rig.Options);

    private static async Task<SimulatorRig> DelayedTripWithBoostedSpeedAsync()
    {
        var rig = new SimulatorRig { Options = { TimeAcceleration = 3, DelayedSpeedFactor = 0.5 } };
        await rig.AddTripAsync(VehicleType.Van, routeStatus: DeliveryRouteStatus.Delayed);
        return rig;
    }
}
