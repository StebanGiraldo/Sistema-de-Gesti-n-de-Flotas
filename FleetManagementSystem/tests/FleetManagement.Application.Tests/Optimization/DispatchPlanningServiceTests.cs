using FleetManagement.Application.Assignment;
using FleetManagement.Application.Builders;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Factories;
using FleetManagement.Application.Flyweights;
using FleetManagement.Application.Geo;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Optimization;
using FleetManagement.Application.Services;
using FleetManagement.Application.Tests.Assignment;
using FleetManagement.Application.Tests.Decorator;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Xunit;
using static FleetManagement.Application.Tests.Optimization.OptimizationTestData;

namespace FleetManagement.Application.Tests.Optimization;

/// <summary>
/// El plan de reparto con repositorios reales en memoria, el validador de reglas real y el servicio de rutas real (el
/// protegido por el Decorator). Todo sobre el ecuador, con el punto de salida en 0°, para calcular las distancias a mano.
/// </summary>
public class DispatchPlanningServiceTests
{
    private readonly AssignmentKit _kit = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly IVehicleTypeProfileFactory _profiles = new VehicleTypeProfileFactory(new FleetOnboardingFactoryProvider());
    private readonly DispatchPlanner _planner = new(new PriorityBestFitStrategy(), new PriorityStopSequencer(new TwoOptRouteOptimizer(new NearestNeighborRouteOptimizer())));

    private IDeliveryRouteService GuardedRoutes()
        => new AssignmentValidatingRouteService(
            new DeliveryRouteService(_kit.Routes, _kit.Vehicles, _kit.Drivers, new DeliveryRouteBuilder(), new DeliveryRouteDirector(), _audit, FleetMediators.CreateRouteColleague(_kit.Routes, _kit.Vehicles), _profiles),
            _kit.Validator);

    private DispatchPlanningService Service(IRouteAssignmentValidator? validator = null)
        => new(_kit.Vehicles, _kit.Routes, validator ?? _kit.Validator, GuardedRoutes(), _planner, _audit, _profiles);

    private async Task<Vehicle> AddVehicleAsync(string plate, double capacityKg = 1000, double? capacityM3 = null, VehicleType type = VehicleType.Van,
        VehicleStatus status = VehicleStatus.Available, double longitude = 0)
    {
        var vehicle = new Vehicle { LicensePlate = plate, Type = type, CapacityKg = capacityKg, CapacityM3 = capacityM3, Status = status };
        vehicle.UpdateLocation(0, longitude);
        return await _kit.Vehicles.AddAsync(vehicle);
    }

    private static DispatchDeliveryRequest Item(string description, double kg, double longitude, string? priority = null, double m3 = 0)
        => new(description, kg, m3, priority, 0, longitude);

    private static DispatchPlanRequest Request(params DispatchDeliveryRequest[] deliveries) => new(0, 0, deliveries.ToList());

    private async Task<int> RouteCountAsync() => (await _kit.Routes.GetAllAsync()).Count;

    // Dos furgonetas de 10 kg y cuatro entregas (4, 4, 6 y 6 kg): solo caben todas si las pesadas se reparten una por vehículo.
    private async Task TwoSmallVansAsync()
    {
        await AddVehicleAsync("A", 10);
        await AddVehicleAsync("B", 10);
    }

    private static DispatchDeliveryRequest[] FourDeliveries() => new[]
    {
        Item("d1", 4, 0.1), Item("d2", 4, 0.2), Item("d3", 6, 0.3), Item("d4", 6, 0.4)
    };

    // ------------------------------------------------------------------ proponer: qué vehículos salen

    [Fact]
    public async Task Plan_OnlyUsesVehiclesThatCanLeave_AndListsTheOthersWithTheirReason()
    {
        await AddVehicleAsync("AVAIL");
        await AddVehicleAsync("MAINT", status: VehicleStatus.Maintenance);
        await AddVehicleAsync("OOS", status: VehicleStatus.OutOfService);
        await AddVehicleAsync("ENR", status: VehicleStatus.EnRoute);
        var overdue = await AddVehicleAsync("OVERDUE");
        _kit.MarkOverdue(overdue, "OilChange");
        var busy = await AddVehicleAsync("BUSY");
        await _kit.AddRouteAsync("Ruta en curso", DeliveryRouteStatus.InProgress, busy);

        var plan = await Service().PlanAsync(Request(Item("d1", 10, 0.1)));

        Assert.Equal("AVAIL", Assert.Single(plan.Trips).LicensePlate);
        Assert.Equal(new[] { "BUSY", "ENR", "MAINT", "OOS", "OVERDUE" }, plan.ExcludedVehicles.Select(v => v.LicensePlate));
        string ReasonOf(string plate) => plan.ExcludedVehicles.Single(v => v.LicensePlate == plate).Reason;
        Assert.Contains("viaje activo 'Ruta en curso'", ReasonOf("BUSY"));
        Assert.Contains("en ruta", ReasonOf("ENR"));
        Assert.Contains("en mantenimiento", ReasonOf("MAINT"));
        Assert.Contains("fuera de servicio", ReasonOf("OOS"));
        Assert.Contains("mantenimiento vencido (OilChange)", ReasonOf("OVERDUE"));
    }

    [Fact]
    public async Task Plan_AVehicleWithAPlannedRouteStillCanBeProposed_BecauseItHasNotLeft()
    {
        var van = await AddVehicleAsync("VAN-1");
        await _kit.AddRouteAsync("Ruta de mañana", DeliveryRouteStatus.Planned, van);

        var plan = await Service().PlanAsync(Request(Item("d1", 10, 0.1)));

        Assert.Equal("VAN-1", Assert.Single(plan.Trips).LicensePlate);
        Assert.Empty(plan.ExcludedVehicles);
    }

    [Fact]
    public async Task Plan_WithoutAvailableVehicles_LeavesEverythingUnassigned_NotAnError()
    {
        await AddVehicleAsync("MAINT", status: VehicleStatus.Maintenance);

        var plan = await Service().PlanAsync(Request(Item("d1", 10, 0.1), Item("d2", 10, 0.2)));

        Assert.Empty(plan.Trips);
        Assert.Equal(2, plan.Unassigned.Count);
        Assert.All(plan.Unassigned, left => Assert.Equal("No hay vehículos disponibles.", left.Reason));
        Assert.Equal(0, plan.Totals.VehiclesUsed);
        Assert.Equal(2, plan.Totals.UnassignedCount);
    }

    [Fact]
    public async Task Plan_VehicleIdsRestrictThePlanToThoseVehicles()
    {
        await AddVehicleAsync("A");
        var b = await AddVehicleAsync("B");

        var plan = await Service().PlanAsync(Request(Item("d1", 10, 0.1)) with { VehicleIds = new List<Guid> { b.Id } });

        Assert.Equal("B", Assert.Single(plan.Trips).LicensePlate);
        Assert.Empty(plan.ExcludedVehicles);      // A no se consideró: no es que no pudiera salir
    }

    [Fact]
    public async Task Plan_ARequestedVehicleThatCannotLeaveIsListedAsExcluded()
    {
        var maint = await AddVehicleAsync("MAINT", status: VehicleStatus.Maintenance);
        var ok = await AddVehicleAsync("OK");

        var plan = await Service().PlanAsync(Request(Item("d1", 10, 0.1)) with { VehicleIds = new List<Guid> { maint.Id, ok.Id } });

        Assert.Equal("OK", Assert.Single(plan.Trips).LicensePlate);
        Assert.Equal("MAINT", Assert.Single(plan.ExcludedVehicles).LicensePlate);
    }

    [Fact]
    public async Task Plan_AnUnknownVehicleIdIsRejected()
    {
        await AddVehicleAsync("A");

        var failure = await Assert.ThrowsAsync<ArgumentException>(() =>
            Service().PlanAsync(Request(Item("d1", 10, 0.1)) with { VehicleIds = new List<Guid> { Guid.NewGuid() } }));

        Assert.Contains("no existe", failure.Message);
    }

    // ------------------------------------------------------------------ proponer: el reparto

    [Fact]
    public async Task Plan_OrdersTheStopsByTheShortestPath_AndMeasuresTheTrip()
    {
        await AddVehicleAsync("VAN-1", type: VehicleType.Van);

        var plan = await Service().PlanAsync(Request(Item("lejos", 10, 0.5), Item("cerca", 10, 0.1), Item("medio", 10, 0.3)));

        var trip = Assert.Single(plan.Trips);
        Assert.Equal(new[] { "cerca", "medio", "lejos" }, trip.Stops.Select(s => s.Description));
        Assert.Equal(0.5 * KmPerDegree, trip.DistanceKm, 2);
        Assert.Equal(0.5 * KmPerDegree / trip.SpeedKmh * 60, trip.EstimatedMinutes, 1);
    }

    [Theory]
    [InlineData(VehicleType.Truck)]
    [InlineData(VehicleType.Van)]
    [InlineData(VehicleType.Car)]
    [InlineData(VehicleType.Motorcycle)]
    public async Task Plan_TheSpeedOfEachTripIsTheOneOfItsVehicleType(VehicleType type)
    {
        await AddVehicleAsync("V-1", type: type);

        var plan = await Service().PlanAsync(Request(Item("d1", 5, 0.2)));

        Assert.Equal(_profiles.GetProfile(type).MaxSpeedKmh, Assert.Single(plan.Trips).SpeedKmh);
    }

    [Fact]
    public async Task Plan_ReportsTheLoadOfEachVehicleAgainstItsCapacity()
    {
        await AddVehicleAsync("VAN-1", capacityKg: 1000, capacityM3: 8);

        var plan = await Service().PlanAsync(Request(Item("d1", 150, 0.1, m3: 1.5), Item("d2", 100, 0.2, m3: 0.5)));

        var trip = Assert.Single(plan.Trips);
        Assert.Equal(250, trip.LoadKg);
        Assert.Equal(2, trip.LoadM3);
        Assert.Equal(25, trip.LoadPercent);
        Assert.Equal(1000, trip.CapacityKg);
        Assert.Equal(8, trip.CapacityM3);
    }

    [Fact]
    public async Task Plan_NeverOverloadsAVehicle_ByWeightOrByVolume()
    {
        await AddVehicleAsync("A", capacityKg: 100, capacityM3: 1);
        await AddVehicleAsync("B", capacityKg: 100, capacityM3: 1);

        var plan = await Service().PlanAsync(Request(
            Item("pesada", 90, 0.1), Item("voluminosa", 5, 0.2, m3: 0.9), Item("otra", 80, 0.3), Item("pequeña", 5, 0.4, m3: 0.05)));

        Assert.All(plan.Trips, trip =>
        {
            Assert.True(trip.LoadKg <= trip.CapacityKg);
            Assert.True(trip.LoadM3 <= trip.CapacityM3);
        });
    }

    [Fact]
    public async Task Plan_UnassignedDeliveriesComeWithTheirReason()
    {
        await AddVehicleAsync("VAN-1", capacityKg: 100);

        var plan = await Service().PlanAsync(Request(Item("cabe", 50, 0.1), Item("enorme", 500, 0.2)));

        var left = Assert.Single(plan.Unassigned);
        Assert.Equal("enorme", left.Delivery.Description);
        Assert.Contains("Excede la capacidad", left.Reason);
        Assert.Equal(1, plan.Totals.AssignedCount);
        Assert.Equal(1, plan.Totals.UnassignedCount);
    }

    [Fact]
    public async Task Plan_ReadsThePriorityWithoutCaringAboutCase_AndEchoesItNormalized()
    {
        await AddVehicleAsync("VAN-1");

        var plan = await Service().PlanAsync(Request(
            Item("a", 1, 0.1, "urgent"), Item("b", 1, 0.2, "HIGH"), Item("c", 1, 0.3, null), Item("d", 1, 0.4, "  fragile ")));

        var stops = Assert.Single(plan.Trips).Stops.ToDictionary(s => s.Description, s => s.Priority);
        Assert.Equal("Urgent", stops["a"]);
        Assert.Equal("High", stops["b"]);
        Assert.Equal("Standard", stops["c"]);
        Assert.Equal("Fragile", stops["d"]);
    }

    [Fact]
    public async Task Plan_UrgentDeliveriesAreVisitedFirst_EvenWhenTheyAreFarther()
    {
        await AddVehicleAsync("VAN-1");

        var plan = await Service().PlanAsync(Request(Item("cerca", 1, 0.1), Item("urgente lejos", 1, 0.9, "Urgent")));

        Assert.Equal(new[] { "urgente lejos", "cerca" }, Assert.Single(plan.Trips).Stops.Select(s => s.Description));
    }

    [Fact]
    public async Task Plan_AcceptsTheMaximumNumberOfDeliveries()
    {
        await AddVehicleAsync("TRK-1", capacityKg: 1_000_000);
        var deliveries = Enumerable.Range(0, DispatchPlanningService.MaxDeliveries).Select(i => Item($"d{i}", 1, (i % 50) * 0.01 + 0.01)).ToArray();

        var plan = await Service().PlanAsync(Request(deliveries));

        Assert.Equal(DispatchPlanningService.MaxDeliveries, plan.Totals.AssignedCount);
    }

    // ------------------------------------------------------------------ proponer: la comparación

    [Fact]
    public async Task Plan_ComparesAgainstTheUnoptimizedPlan_AndShowsTheSavings()
    {
        await AddVehicleAsync("VAN-1");

        var plan = await Service().PlanAsync(Request(Item("lejos", 10, 0.9), Item("cerca", 10, 0.1), Item("medio", 10, 0.5)));

        // Sin optimizar: 0 → 0,9 → 0,1 → 0,5 = 2,1°. Optimizado: 0 → 0,1 → 0,5 → 0,9 = 0,9°.
        Assert.Equal(2.1 * KmPerDegree, plan.Baseline.TotalKm, 2);
        Assert.Equal(0.9 * KmPerDegree, plan.Totals.TotalKm, 2);
        Assert.True(plan.Comparison.Comparable);
        Assert.Equal(1.2 * KmPerDegree, plan.Comparison.KmSaved, 2);
        Assert.Equal(57.1, plan.Comparison.KmSavedPercent);
        Assert.Equal(plan.Baseline.TotalMinutes - plan.Totals.TotalMinutes, plan.Comparison.MinutesSaved, 1);
        Assert.Null(plan.Comparison.Note);
    }

    [Fact]
    public async Task Plan_TheUnoptimizedPlanKeepsTheReceivedOrderOfTheStops()
    {
        await AddVehicleAsync("VAN-1");
        var deliveries = new[] { Item("lejos", 10, 0.9), Item("cerca", 10, 0.1), Item("medio", 10, 0.5) };

        var plan = await Service().PlanAsync(Request(deliveries));

        // La línea base sigue el orden recibido: 0,9 + 0,8 + 0,4 = 2,1° (ya comprobado arriba); aquí, que el plan optimizado no lo imita.
        Assert.Equal(new[] { "cerca", "medio", "lejos" }, Assert.Single(plan.Trips).Stops.Select(s => s.Description));
    }

    [Fact]
    public async Task Plan_WhenThePlansAssignDifferentNumbersOfDeliveries_TheyAreNotComparable()
    {
        await TwoSmallVansAsync();

        var plan = await Service().PlanAsync(Request(FourDeliveries()));

        Assert.Equal(4, plan.Totals.AssignedCount);
        Assert.Equal(3, plan.Baseline.AssignedCount);          // el orden simple deja una sin asignar
        Assert.False(plan.Comparison.Comparable);
        Assert.Equal(0, plan.Comparison.KmSaved);
        Assert.Contains("no asignan la misma cantidad", plan.Comparison.Note);
    }

    [Fact]
    public async Task Plan_TotalsAddUpAcrossTrips()
    {
        await TwoSmallVansAsync();

        var plan = await Service().PlanAsync(Request(FourDeliveries()));

        Assert.Equal(2, plan.Totals.VehiclesUsed);
        Assert.Equal(plan.Trips.Sum(t => t.DistanceKm), plan.Totals.TotalKm, 2);
        Assert.Equal(plan.Trips.Sum(t => t.EstimatedMinutes), plan.Totals.TotalMinutes, 1);
        Assert.Equal(plan.Trips.Max(t => t.EstimatedMinutes), plan.Totals.LongestTripMinutes, 1);
    }

    [Fact]
    public async Task Plan_ExplainsAnOptimizedPlanThatTravelsMoreThanTheSimpleOne()
    {
        // La entrega urgente está lejos: atenderla primero alarga el recorrido frente al orden simple (cerca → lejos).
        await AddVehicleAsync("VAN-1");

        var plan = await Service().PlanAsync(Request(Item("cerca", 10, 0.1), Item("urgente lejos", 10, 0.9, "Urgent")));

        Assert.Equal(0.9 * KmPerDegree, plan.Baseline.TotalKm, 2);          // 0 → 0,1 → 0,9
        Assert.Equal((0.9 + 0.8) * KmPerDegree, plan.Totals.TotalKm, 2);    // 0 → 0,9 → 0,1
        Assert.True(plan.Comparison.Comparable);
        Assert.True(plan.Comparison.KmSaved < 0);
        Assert.NotNull(plan.Comparison.Note);
    }

    // ------------------------------------------------------------------ proponer: otras garantías

    [Fact]
    public async Task Plan_SavesNothing()
    {
        await AddVehicleAsync("VAN-1");

        await Service().PlanAsync(Request(Item("d1", 10, 0.1)));

        Assert.Equal(0, await RouteCountAsync());
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task Plan_IsTheSameForTheSameData_WhateverTheOrderTheVehiclesWereRegistered()
    {
        var request = Request(Item("d1", 40, 0.1), Item("d2", 40, 0.2), Item("d3", 40, 0.3), Item("d4", 40, 0.4));

        async Task<string> SignatureAsync(params string[] plates)
        {
            var kit = new AssignmentKit();
            foreach (var plate in plates)
            {
                var vehicle = new Vehicle { LicensePlate = plate, Type = VehicleType.Van, CapacityKg = 100 };
                vehicle.UpdateLocation(0, 0);
                await kit.Vehicles.AddAsync(vehicle);
            }
            var service = new DispatchPlanningService(kit.Vehicles, kit.Routes, kit.Validator, GuardedRoutes(), _planner, _audit, _profiles);
            var plan = await service.PlanAsync(request);
            return string.Join(" | ", plan.Trips.Select(t => $"{t.LicensePlate}: {string.Join(",", t.Stops.Select(s => s.Description))}"));
        }

        Assert.Equal(await SignatureAsync("A", "B", "C"), await SignatureAsync("C", "B", "A"));
    }

    // ------------------------------------------------------------------ proponer: solicitudes inválidas

    private async Task AssertRejectedAsync(DispatchPlanRequest request, string expectedText)
    {
        await AddVehicleAsync("VAN-1");

        var planFailure = await Assert.ThrowsAsync<ArgumentException>(() => Service().PlanAsync(request));
        var applyFailure = await Assert.ThrowsAsync<ArgumentException>(() => Service().ApplyAsync(request));

        Assert.Contains(expectedText, planFailure.Message);
        Assert.Contains(expectedText, applyFailure.Message);
        Assert.Equal(0, await RouteCountAsync());      // y no se crea nada
    }

    [Theory]
    [InlineData("  ", 10.0, 0.0, "Standard", 0.0, 0.1, "no tiene descripción")]
    [InlineData("x", 0.0, 0.0, "Standard", 0.0, 0.1, "más de 0 kg")]
    [InlineData("x", -5.0, 0.0, "Standard", 0.0, 0.1, "más de 0 kg")]
    [InlineData("x", double.NaN, 0.0, "Standard", 0.0, 0.1, "más de 0 kg")]
    [InlineData("x", 10.0, -1.0, "Standard", 0.0, 0.1, "volumen")]
    [InlineData("x", 10.0, 0.0, "Standard", 91.0, 0.1, "coordenada válida")]
    [InlineData("x", 10.0, 0.0, "Standard", 0.0, -181.0, "coordenada válida")]
    [InlineData("x", 10.0, 0.0, "Maybe", 0.0, 0.1, "prioridad")]
    [InlineData("x", 10.0, 0.0, "99", 0.0, 0.1, "prioridad")]
    public Task InvalidDeliveries_AreRejectedWithAClearMessage_ByPlanAndByApply(
        string description, double kg, double m3, string priority, double latitude, double longitude, string expectedText)
        => AssertRejectedAsync(Request(new DispatchDeliveryRequest(description, kg, m3, priority, latitude, longitude)), expectedText);

    [Fact]
    public Task WithoutDeliveries_TheRequestIsRejected()
        => AssertRejectedAsync(new DispatchPlanRequest(0, 0, null), "al menos una entrega");

    [Fact]
    public Task WithAnEmptyList_TheRequestIsRejected()
        => AssertRejectedAsync(new DispatchPlanRequest(0, 0, new List<DispatchDeliveryRequest>()), "al menos una entrega");

    [Theory]
    [InlineData(100.0, 0.0)]
    [InlineData(-91.0, 0.0)]
    [InlineData(0.0, 200.0)]
    [InlineData(0.0, -181.0)]
    public Task AnInvalidStartingPoint_IsRejected(double latitude, double longitude)
        => AssertRejectedAsync(new DispatchPlanRequest(latitude, longitude, new List<DispatchDeliveryRequest> { Item("d1", 10, 0.1) }), "punto de salida");

    [Fact]
    public Task MoreThanTheMaximumNumberOfDeliveries_IsRejected()
        => AssertRejectedAsync(
            Request(Enumerable.Range(0, DispatchPlanningService.MaxDeliveries + 1).Select(i => Item($"d{i}", 1, 0.1)).ToArray()),
            "como máximo");

    // ------------------------------------------------------------------ aplicar

    [Fact]
    public async Task Apply_CreatesOnePlannedRoutePerTrip_WithItsVehicleCargoAndOrderedStops()
    {
        await TwoSmallVansAsync();

        var result = await Service().ApplyAsync(Request(FourDeliveries()));

        Assert.Equal(2, result.CreatedRoutes.Count);
        Assert.Equal(2, await RouteCountAsync());
        Assert.All(result.CreatedRoutes, route => Assert.Equal("Planned", route.Status));

        // A: las entregas de 6 kg (0,3°) y 4 kg (0,1°) → visita 0,1° y termina en 0,3°. B: 0,2° y termina en 0,4°.
        var a = result.CreatedRoutes.Single(route => route.AssignedVehiclePlate == "A");
        Assert.Equal("Despacho A (2 entregas)", a.Name);
        Assert.Equal((0.0, 0.0), (a.OriginLat, a.OriginLng));
        Assert.Equal(new[] { "d1" }, a.Waypoints.Select(w => w.Label));
        Assert.Equal((0.0, 0.3), (a.DestinationLat, a.DestinationLng));
        Assert.Equal(new[] { "d1", "d3" }, a.CargoItems.Select(c => c.Description));
        Assert.Equal(10, a.CargoItems.Sum(c => c.WeightKg));

        var b = result.CreatedRoutes.Single(route => route.AssignedVehiclePlate == "B");
        Assert.Equal(new[] { "d2" }, b.Waypoints.Select(w => w.Label));
        Assert.Equal((0.0, 0.4), (b.DestinationLat, b.DestinationLng));
        Assert.Equal(10, b.CargoItems.Sum(c => c.WeightKg));
    }

    [Fact]
    public async Task Apply_AOneStopTripBecomesARouteWithoutIntermediateStops()
    {
        await AddVehicleAsync("VAN-1");

        var result = await Service().ApplyAsync(Request(Item("única", 10, 0.2)));

        var route = Assert.Single(result.CreatedRoutes);
        Assert.Equal("Despacho VAN-1 (1 entrega)", route.Name);
        Assert.Empty(route.Waypoints);
        Assert.Equal(0.2, route.DestinationLng);
    }

    [Fact]
    public async Task Apply_TheRoutesCarryTheSameKilometresAndMinutesAsThePlan()
    {
        await AddVehicleAsync("VAN-1", type: VehicleType.Van);
        var request = Request(Item("lejos", 10, 0.5), Item("cerca", 10, 0.1), Item("medio", 10, 0.3));

        var plan = await Service().PlanAsync(request);
        var result = await Service().ApplyAsync(request);

        var trip = Assert.Single(plan.Trips);
        var route = Assert.Single(result.CreatedRoutes);
        Assert.Equal(trip.DistanceKm, route.EstimatedDistanceKm);
        Assert.Equal(trip.EstimatedMinutes, route.EstimatedDurationMinutes);
    }

    [Fact]
    public async Task Apply_KeepsThePriorityOfEveryDeliveryInTheCargo()
    {
        await AddVehicleAsync("VAN-1");

        var result = await Service().ApplyAsync(Request(Item("a", 1, 0.1, "Urgent"), Item("b", 1, 0.2, "Fragile"), Item("c", 1, 0.3)));

        var cargo = Assert.Single(result.CreatedRoutes).CargoItems.ToDictionary(c => c.Description, c => c.Priority);
        Assert.Equal("Urgent", cargo["a"]);
        Assert.Equal("Fragile", cargo["b"]);
        Assert.Equal("Standard", cargo["c"]);
    }

    [Fact]
    public async Task Apply_PassesTheScheduledDateToEveryRoute()
    {
        await TwoSmallVansAsync();
        var date = new DateTime(2030, 5, 17, 8, 0, 0, DateTimeKind.Utc);

        var result = await Service().ApplyAsync(Request(FourDeliveries()) with { ScheduledDate = date });

        Assert.All(result.CreatedRoutes, route => Assert.Equal(date, route.ScheduledDate));
    }

    [Fact]
    public async Task Apply_DoesNotStartTheTrips_NorTouchTheVehicles()
    {
        await TwoSmallVansAsync();

        await Service().ApplyAsync(Request(FourDeliveries()));

        Assert.All(await _kit.Vehicles.GetAllAsync(), vehicle => Assert.Equal(VehicleStatus.Available, vehicle.Status));
        Assert.All(await _kit.Routes.GetAllAsync(), route => Assert.Equal(DeliveryRouteStatus.Planned, route.Status));
    }

    [Fact]
    public async Task Apply_LeavesUnassignedDeliveriesOutAndReportsThem()
    {
        await AddVehicleAsync("VAN-1", capacityKg: 100);

        var result = await Service().ApplyAsync(Request(Item("cabe", 50, 0.1), Item("enorme", 500, 0.2)));

        Assert.Single(result.CreatedRoutes);
        var left = Assert.Single(result.Unassigned);
        Assert.Equal("enorme", left.Delivery.Description);
        Assert.Contains("Excede la capacidad", left.Reason);
        Assert.Equal(1, result.Totals.AssignedCount);
        Assert.Equal(1, result.Totals.UnassignedCount);
    }

    [Fact]
    public async Task Apply_WithNothingAssignable_CreatesNothingAndFails()
    {
        await AddVehicleAsync("MAINT", status: VehicleStatus.Maintenance);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Service().ApplyAsync(Request(Item("d1", 10, 0.1))));

        Assert.Contains("no se creó ninguna ruta", failure.Message);
        Assert.Equal(0, await RouteCountAsync());
    }

    [Fact]
    public async Task Apply_LeavesAnAuditEntryForThePlan()
    {
        await TwoSmallVansAsync();

        await Service().ApplyAsync(Request(FourDeliveries()));

        var entry = Assert.Single(_audit.Entries, e => e.Category == "Despacho");
        Assert.Contains("2 ruta(s)", entry.Message);
        Assert.Contains("4 entrega(s)", entry.Message);
    }

    [Fact]
    public async Task Apply_IsAllOrNothing_WhenARouteIsRejectedAtTheLastMinute()
    {
        await TwoSmallVansAsync();
        var b = (await _kit.Vehicles.GetByLicensePlateAsync("B"))!;

        // Pasa la elección de vehículos (primera comprobación) pero el viaje de B se rechaza al comprobar antes de crear.
        var failure = await Assert.ThrowsAsync<AssignmentRuleException>(() =>
            Service(new RejectsSecondCheck(b.Id)).ApplyAsync(Request(FourDeliveries())));

        Assert.Contains("Rechazada", failure.Message);
        Assert.Equal(0, await RouteCountAsync());       // ni siquiera la ruta de A, que sí era válida
    }

    private sealed class RejectsSecondCheck : IRouteAssignmentValidator
    {
        private readonly Guid _vehicleId;
        private int _checks;

        public RejectsSecondCheck(Guid vehicleId) => _vehicleId = vehicleId;

        public Task ValidateNewRouteAsync(Guid? vehicleId, Guid? driverId, IEnumerable<CargoItemDto>? cargo)
        {
            if (vehicleId == _vehicleId && ++_checks == 2)
                throw new AssignmentRuleException(new[] { "Rechazada a último momento." });

            return Task.CompletedTask;
        }

        public Task ValidateStartAsync(Guid routeId) => Task.CompletedTask;
    }
}
