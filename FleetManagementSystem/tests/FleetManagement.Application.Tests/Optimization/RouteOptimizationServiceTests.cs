using FleetManagement.Application.Builders;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Factories;
using FleetManagement.Application.Flyweights;
using FleetManagement.Application.Geo;
using FleetManagement.Application.Optimization;
using FleetManagement.Application.Services;
using FleetManagement.Application.Tests.Decorator;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Persistence;
using NetTopologySuite.Geometries;
using Xunit;
using static FleetManagement.Application.Tests.Optimization.OptimizationTestData;

namespace FleetManagement.Application.Tests.Optimization;

/// <summary>
/// Optimizar el orden de las paradas de una ruta real, con repositorios reales en memoria. Casi todo ocurre sobre el
/// ecuador (origen en 0°, destino en 0,6°), donde las distancias salen a mano: 1° de longitud = <see cref="KmPerDegree"/> km.
/// </summary>
public class RouteOptimizationServiceTests
{
    private readonly InMemoryDeliveryRouteRepository _routes = new();
    private readonly InMemoryVehicleRepository _vehicles = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly IVehicleTypeProfileFactory _profiles = new VehicleTypeProfileFactory(new FleetOnboardingFactoryProvider());

    private RouteOptimizationService Service()
        => new(_routes, _vehicles, new TwoOptRouteOptimizer(new NearestNeighborRouteOptimizer()), _audit, _profiles);

    private static Point PointAt(double longitude, double latitude = 0) => new(longitude, latitude) { SRID = 4326 };

    private async Task<Vehicle> AddVehicleAsync(VehicleType type)
        => await _vehicles.AddAsync(new Vehicle { LicensePlate = $"{type}-1", Type = type });

    private async Task<DeliveryRoute> AddRouteAsync(DeliveryRouteStatus status, Vehicle? vehicle, params (string Label, double Longitude)[] stops)
    {
        var route = new DeliveryRoute
        {
            Name = "Ruta de prueba",
            Origin = PointAt(0),
            Destination = PointAt(0.6),
            Status = status,
            AssignedVehicleId = vehicle?.Id,
            EstimatedDistanceKm = 1,
            EstimatedDurationMinutes = 2
        };
        for (var index = 0; index < stops.Length; index++)
            route.Waypoints.Add(new Waypoint { Order = index, Label = stops[index].Label, Location = PointAt(stops[index].Longitude) });

        return await _routes.AddAsync(route);
    }

    private Task<DeliveryRoute> PlannedRouteAsync(params (string Label, double Longitude)[] stops)
        => AddRouteAsync(DeliveryRouteStatus.Planned, null, stops);

    // Mismo orden de zig-zag en casi todas las pruebas: 0 → 0,5 → 0,1 → 0,3 → 0,6 = 1,4° (el mejor, 0,1 → 0,3 → 0,5, mide 0,6°).
    private static readonly (string, double)[] ZigZag = { ("A", 0.5), ("B", 0.1), ("C", 0.3) };

    private static IEnumerable<string> Labels(RouteSequenceDto sequence) => sequence.Waypoints.Select(waypoint => waypoint.Label);

    // ------------------------------------------------------------------ proponer

    [Fact]
    public async Task Propose_ShowsTheCurrentAndTheBetterOrder_WithKilometresAndMinutes()
    {
        var route = await PlannedRouteAsync(ZigZag);

        var result = await Service().ProposeAsync(route.Id);

        Assert.Equal(new[] { "A", "B", "C" }, Labels(result.Current));
        Assert.Equal(new[] { "B", "C", "A" }, Labels(result.Proposed));
        Assert.Equal(1.4 * KmPerDegree, result.Current.DistanceKm, 2);
        Assert.Equal(0.6 * KmPerDegree, result.Proposed.DistanceKm, 2);
        Assert.Equal(1.4 * KmPerDegree / TripEstimate.DefaultSpeedKmh * 60, result.Current.DurationMinutes, 1);   // sin vehículo: velocidad por defecto
        Assert.Equal(0.6 * KmPerDegree / TripEstimate.DefaultSpeedKmh * 60, result.Proposed.DurationMinutes, 1);
        Assert.True(result.Improves);
        Assert.False(result.Applied);
    }

    [Fact]
    public async Task Propose_ReportsTheSavingsAsKilometresPercentAndMinutes()
    {
        var route = await PlannedRouteAsync(ZigZag);

        var result = await Service().ProposeAsync(route.Id);

        Assert.Equal(result.Current.DistanceKm - result.Proposed.DistanceKm, result.DistanceSavedKm, 2);
        Assert.Equal(57.1, result.DistanceSavedPercent);                                                              // 0,8° de 1,4°
        Assert.Equal(result.Current.DurationMinutes - result.Proposed.DurationMinutes, result.MinutesSaved, 1);
    }

    [Fact]
    public async Task Propose_ChangesNothing()
    {
        var route = await PlannedRouteAsync(ZigZag);

        await Service().ProposeAsync(route.Id);

        var stored = (await _routes.GetByIdAsync(route.Id))!;
        Assert.Equal(new[] { "A", "B", "C" }, stored.Waypoints.OrderBy(waypoint => waypoint.Order).Select(waypoint => waypoint.Label));
        Assert.Equal(1, stored.EstimatedDistanceKm);
        Assert.Equal(2, stored.EstimatedDurationMinutes);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task Propose_KeepsTheCurrentOrder_WhenItIsAlreadyTheBest()
    {
        var route = await PlannedRouteAsync(("B", 0.1), ("C", 0.3), ("A", 0.5));

        var result = await Service().ProposeAsync(route.Id);

        Assert.False(result.Improves);
        Assert.Equal(Labels(result.Current), Labels(result.Proposed));
        Assert.Equal(result.Current.DistanceKm, result.Proposed.DistanceKm);
        Assert.Equal(0, result.DistanceSavedKm);
        Assert.Equal(0, result.DistanceSavedPercent);
        Assert.Equal(0, result.MinutesSaved);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Propose_HasNothingToReorder_WithFewerThanTwoStops(int stopCount)
    {
        var route = await PlannedRouteAsync(ZigZag.Take(stopCount).ToArray());

        var result = await Service().ProposeAsync(route.Id);

        Assert.False(result.Improves);
        Assert.Equal(stopCount, result.Proposed.Waypoints.Count);
        Assert.Equal(result.Current.DistanceKm, result.Proposed.DistanceKm);
    }

    [Fact]
    public async Task Propose_NumbersTheStopsByPosition_EvenIfTheStoredNumbersHaveGaps()
    {
        var route = await PlannedRouteAsync(("A", 0.5), ("B", 0.1), ("C", 0.3));
        route.Waypoints[0].Order = 10;
        route.Waypoints[1].Order = 20;
        route.Waypoints[2].Order = 30;
        await _routes.UpdateAsync(route);

        var result = await Service().ProposeAsync(route.Id);

        Assert.Equal(new[] { 0, 1, 2 }, result.Current.Waypoints.Select(waypoint => waypoint.Order));
        Assert.Equal(new[] { 0, 1, 2 }, result.Proposed.Waypoints.Select(waypoint => waypoint.Order));
        Assert.Equal(new[] { "A", "B", "C" }, Labels(result.Current));
    }

    [Fact]
    public async Task Propose_StaysConsistent_TheKilometresOfEachListMatchTheOrderItShows()
    {
        var route = await PlannedRouteAsync(ZigZag);

        var result = await Service().ProposeAsync(route.Id);

        foreach (var sequence in new[] { result.Current, result.Proposed })
        {
            var stops = sequence.Waypoints.Select(waypoint => At(waypoint.Latitude, waypoint.Longitude)).ToList();
            Assert.Equal(TourLength.Km(OnEquator(0), stops, OnEquator(0.6)), sequence.DistanceKm, 2);
        }
    }

    [Fact]
    public async Task Propose_NeverReturnsALongerRoute_NorLosesOrInventsStops()
    {
        var random = new Random(8);
        for (var run = 0; run < 60; run++)
        {
            var points = RandomPoints(random, 2 + random.Next(0, 9));      // origen, destino y de 0 a 8 paradas
            var route = new DeliveryRoute { Name = $"R{run}", Origin = PointAt(points[0].Longitude, points[0].Latitude), Destination = PointAt(points[1].Longitude, points[1].Latitude) };
            for (var index = 2; index < points.Count; index++)
                route.Waypoints.Add(new Waypoint { Order = index - 2, Label = $"S{index}", Location = PointAt(points[index].Longitude, points[index].Latitude) });
            await _routes.AddAsync(route);

            var result = await Service().ProposeAsync(route.Id);

            Assert.True(result.Proposed.DistanceKm <= result.Current.DistanceKm, $"corrida {run}: {result.Proposed.DistanceKm} km propuestos frente a {result.Current.DistanceKm} km actuales");
            Assert.Equal(Labels(result.Current).OrderBy(label => label), Labels(result.Proposed).OrderBy(label => label));
            Assert.Equal(result.Improves, result.DistanceSavedKm > 0);
        }
    }

    [Fact]
    public async Task Propose_EstimatesTheTimeWithTheSpeedOfTheAssignedVehicleType()
    {
        var truck = await AddVehicleAsync(VehicleType.Truck);
        var car = await AddVehicleAsync(VehicleType.Car);
        var byTruck = await AddRouteAsync(DeliveryRouteStatus.Planned, truck, ZigZag);
        var byCar = await AddRouteAsync(DeliveryRouteStatus.Planned, car, ZigZag);

        var truckResult = await Service().ProposeAsync(byTruck.Id);
        var carResult = await Service().ProposeAsync(byCar.Id);

        Assert.Equal(truckResult.Proposed.DistanceKm / _profiles.GetProfile(VehicleType.Truck).MaxSpeedKmh * 60, truckResult.Proposed.DurationMinutes, 1);
        Assert.Equal(carResult.Proposed.DistanceKm / _profiles.GetProfile(VehicleType.Car).MaxSpeedKmh * 60, carResult.Proposed.DurationMinutes, 1);
        Assert.NotEqual(truckResult.Proposed.DurationMinutes, carResult.Proposed.DurationMinutes);
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.InProgress)]
    [InlineData(DeliveryRouteStatus.Delayed)]
    [InlineData(DeliveryRouteStatus.Completed)]
    [InlineData(DeliveryRouteStatus.Cancelled)]
    public async Task Propose_WorksForARouteInAnyStatus_BecauseItOnlyLooks(DeliveryRouteStatus status)
    {
        var route = await AddRouteAsync(status, null, ZigZag);

        var result = await Service().ProposeAsync(route.Id);

        Assert.Equal(status.ToString(), result.Status);
        Assert.True(result.Improves);
        Assert.False(result.Applied);
    }

    [Fact]
    public async Task NeverMakesARouteLonger_EvenWhenTheHeuristicWouldHaveDoneSo()
    {
        // El vecino más cercano con 2-opt es una heurística: a veces deja un recorrido más largo que el mejor posible. Si la
        // ruta YA está en el mejor orden, aplicar lo que la heurística calcula la empeoraría, y el servicio debe dejarla igual.
        // Se prueba con rutas puestas a propósito en su mejor orden (se calcula probando todos los órdenes).
        var heuristic = new TwoOptRouteOptimizer(new NearestNeighborRouteOptimizer());
        var random = new Random(31);
        var heuristicWasWorse = 0;

        for (var run = 0; run < 60; run++)
        {
            var points = RandomPoints(random, 8);
            var origin = points[0];
            var destination = points[1];
            var stops = points.Skip(2).ToList();
            var bestOrder = BestPossibleOrder(origin, stops, destination);

            var route = new DeliveryRoute
            {
                Name = $"Ruta en su mejor orden {run}",
                Origin = PointAt(origin.Longitude, origin.Latitude),
                Destination = PointAt(destination.Longitude, destination.Latitude),
                Status = DeliveryRouteStatus.Planned
            };
            for (var position = 0; position < bestOrder.Count; position++)
            {
                var stop = stops[bestOrder[position]];
                route.Waypoints.Add(new Waypoint { Order = position, Label = $"P{bestOrder[position]}", Location = PointAt(stop.Longitude, stop.Latitude) });
            }
            route = await _routes.AddAsync(route);

            var inBestOrder = bestOrder.Select(index => stops[index]).ToList();
            var bestKm = TourLength.Km(origin, inBestOrder, destination);
            var heuristicKm = TourLength.Km(origin, inBestOrder, heuristic.Order(origin, inBestOrder, destination), destination);
            if (heuristicKm > bestKm + 0.05) heuristicWasWorse++;

            var proposal = await Service().ProposeAsync(route.Id);
            Assert.False(proposal.Improves, $"caso {run}: la ruta ya estaba en su mejor orden y el servicio propuso cambiarla");
            Assert.Equal(Labels(proposal.Current), Labels(proposal.Proposed));
            Assert.Equal(proposal.Current.DistanceKm, proposal.Proposed.DistanceKm);

            var applied = await Service().ApplyAsync(route.Id);
            Assert.False(applied.Applied);
            var stored = (await _routes.GetByIdAsync(route.Id))!;
            Assert.Equal(bestOrder.Select(index => $"P{index}"), stored.Waypoints.OrderBy(waypoint => waypoint.Order).Select(waypoint => waypoint.Label));
        }

        // Sin esto la prueba sería vacía: tiene que haber casos en los que la heurística de verdad lo habría empeorado.
        Assert.True(heuristicWasWorse >= 5, $"solo {heuristicWasWorse} de 60 casos en los que la heurística queda por encima del mejor orden");
    }

    // ------------------------------------------------------------------ aplicar

    [Fact]
    public async Task Apply_SavesTheBetterOrder_AndRecalculatesDistanceAndTime()
    {
        var van = await AddVehicleAsync(VehicleType.Van);
        var route = await AddRouteAsync(DeliveryRouteStatus.Planned, van, ZigZag);

        var result = await Service().ApplyAsync(route.Id);

        var stored = (await _routes.GetByIdAsync(route.Id))!;
        var ordered = stored.Waypoints.OrderBy(waypoint => waypoint.Order).ToList();
        Assert.Equal(new[] { "B", "C", "A" }, ordered.Select(waypoint => waypoint.Label));
        Assert.Equal(new[] { 0, 1, 2 }, ordered.Select(waypoint => waypoint.Order));
        Assert.Equal(result.Proposed.DistanceKm, stored.EstimatedDistanceKm);
        Assert.Equal(result.Proposed.DurationMinutes, stored.EstimatedDurationMinutes);
        Assert.Equal(0.6 * KmPerDegree / _profiles.GetProfile(VehicleType.Van).MaxSpeedKmh * 60, stored.EstimatedDurationMinutes, 1);
        Assert.True(result.Applied);
        Assert.True(result.Improves);
    }

    [Fact]
    public async Task Apply_ReportsWhatItChanged_EvenThoughTheStopsWereRenumbered()
    {
        var route = await PlannedRouteAsync(ZigZag);

        var result = await Service().ApplyAsync(route.Id);

        Assert.Equal(new[] { "A", "B", "C" }, Labels(result.Current));     // "lo anterior", no lo ya reordenado
        Assert.Equal(new[] { "B", "C", "A" }, Labels(result.Proposed));
    }

    [Fact]
    public async Task Apply_KeepsTheOriginAndTheDestination_AndTheStopCoordinates()
    {
        var route = await PlannedRouteAsync(ZigZag);

        await Service().ApplyAsync(route.Id);

        var stored = (await _routes.GetByIdAsync(route.Id))!;
        Assert.Equal((0.0, 0.0), (stored.Origin.X, stored.Origin.Y));
        Assert.Equal((0.6, 0.0), (stored.Destination.X, stored.Destination.Y));
        Assert.Equal(new[] { 0.5, 0.1, 0.3 }.OrderBy(x => x), stored.Waypoints.Select(waypoint => waypoint.Location.X).OrderBy(x => x));
    }

    [Fact]
    public async Task Apply_LeavesAnAuditEntry()
    {
        var route = await PlannedRouteAsync(ZigZag);

        await Service().ApplyAsync(route.Id);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("Ruta", entry.Category);
        Assert.Contains("Ruta de prueba", entry.Message);
    }

    [Fact]
    public async Task Apply_ChangesNothing_WhenTheOrderIsAlreadyTheBest()
    {
        var route = await PlannedRouteAsync(("B", 0.1), ("C", 0.3), ("A", 0.5));

        var result = await Service().ApplyAsync(route.Id);

        Assert.False(result.Applied);
        Assert.False(result.Improves);
        var stored = (await _routes.GetByIdAsync(route.Id))!;
        Assert.Equal(1, stored.EstimatedDistanceKm);      // ni siquiera recalcula la estimación guardada
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task Apply_Twice_TheSecondTimeFindsNothingLeftToImprove()
    {
        var route = await PlannedRouteAsync(ZigZag);
        var service = Service();

        await service.ApplyAsync(route.Id);
        var second = await service.ApplyAsync(route.Id);
        var proposal = await service.ProposeAsync(route.Id);

        Assert.False(second.Applied);
        Assert.False(proposal.Improves);
        Assert.Single(_audit.Entries);
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.InProgress, "en progreso")]
    [InlineData(DeliveryRouteStatus.Delayed, "retrasada")]
    [InlineData(DeliveryRouteStatus.Completed, "completada")]
    [InlineData(DeliveryRouteStatus.Cancelled, "cancelada")]
    public async Task Apply_RefusesARouteThatIsNotPlanned_AndLeavesItAsItWas(DeliveryRouteStatus status, string described)
    {
        var route = await AddRouteAsync(status, null, ZigZag);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Service().ApplyAsync(route.Id));

        Assert.Contains("planificada", failure.Message);
        Assert.Contains(described, failure.Message);
        var stored = (await _routes.GetByIdAsync(route.Id))!;
        Assert.Equal(new[] { "A", "B", "C" }, stored.Waypoints.OrderBy(waypoint => waypoint.Order).Select(waypoint => waypoint.Label));
        Assert.Equal(1, stored.EstimatedDistanceKm);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task TheEstimateAfterApplying_IsTheSameOneCreatingTheRouteInThatOrderGives()
    {
        // Dos cálculos distintos (el de crear una ruta y el de optimizarla) tienen que dar la misma distancia y el mismo tiempo.
        var van = await AddVehicleAsync(VehicleType.Van);
        var creator = new DeliveryRouteService(_routes, _vehicles, new InMemoryDriverRepository(), new DeliveryRouteBuilder(), new DeliveryRouteDirector(), _audit, FleetMediators.CreateRouteColleague(_routes, _vehicles), _profiles);
        static WaypointDto Stop(string label, double longitude, int order) => new(0, longitude, label, order);
        var zigZag = await creator.CreateRouteAsync(new CreateDeliveryRouteRequest("zig-zag", 0, 0, 0, 0.6,
            new List<WaypointDto> { Stop("A", 0.5, 0), Stop("B", 0.1, 1), Stop("C", 0.3, 2) }, null, van.Id, null, null));
        var direct = await creator.CreateRouteAsync(new CreateDeliveryRouteRequest("directa", 0, 0, 0, 0.6,
            new List<WaypointDto> { Stop("B", 0.1, 0), Stop("C", 0.3, 1), Stop("A", 0.5, 2) }, null, van.Id, null, null));

        await Service().ApplyAsync(zigZag.Id);

        var stored = (await _routes.GetByIdAsync(zigZag.Id))!;
        Assert.Equal(direct.EstimatedDistanceKm, stored.EstimatedDistanceKm);
        Assert.Equal(direct.EstimatedDurationMinutes, stored.EstimatedDurationMinutes);
    }

    // ------------------------------------------------------------------ rutas que no existen

    [Fact]
    public async Task AnUnknownRoute_IsNotFound_ForBothOperations()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service().ProposeAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service().ApplyAsync(Guid.NewGuid()));
    }
}
