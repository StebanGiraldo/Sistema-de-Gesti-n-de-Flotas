using FleetManagement.Application.Analysis;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Enums;
using Xunit;
using static FleetManagement.Application.Tests.Visitor.FleetData;

namespace FleetManagement.Application.Tests.Visitor;

/// <summary>
/// VISITANTE #2: distancias de las rutas. Las rutas canceladas se listan pero no cuentan en los totales (nunca se recorrieron).
/// Flota de la prueba (distancia, estado, paradas, minutos, retraso): "Girón" 12,5 km en curso, "Bogotá" 390,25 km planificada,
/// "Centro" 8,125 km terminada con 10 min de retraso, "Cancelada" 100 km y "Retrasada" 40 km con 45 min de retraso.
/// </summary>
public class RouteDistanceVisitorTests
{
    private const double Tolerance = 1e-9;

    private static FleetElementStructure Scenario() => Structure(routes: new[]
    {
        Route("Girón", DeliveryRouteStatus.InProgress, distanceKm: 12.5, stops: 2, durationMinutes: 25),
        Route("Bogotá", DeliveryRouteStatus.Planned, distanceKm: 390.25, stops: 4, durationMinutes: 480),
        Route("Centro", DeliveryRouteStatus.Completed, distanceKm: 8.125, stops: 1, durationMinutes: 20.46, delayMinutes: 10),
        Route("Cancelada", DeliveryRouteStatus.Cancelled, distanceKm: 100, delayMinutes: 5),
        Route("Retrasada", DeliveryRouteStatus.Delayed, distanceKm: 40, stops: 0, durationMinutes: 80, delayMinutes: 45)
    });

    private static RouteDistanceAnalysisDto Analyze(FleetElementStructure structure) => structure.Analyze(new RouteDistanceVisitor());

    [Fact]
    public void TheTotals_CountOnlyTheRoutesThatWereNotCancelled()
    {
        var result = Analyze(Scenario());

        Assert.Equal(4, result.TotalRoutes);                              // Girón, Bogotá, Centro y Retrasada
        Assert.Equal(450.875, result.TotalDistanceKm, Tolerance);         // 12,5 + 390,25 + 8,125 + 40
        Assert.Equal(8.125, result.CompletedDistanceKm, Tolerance);
        Assert.Equal(52.5, result.ActiveDistanceKm, Tolerance);           // en curso (12,5) + retrasada (40)
        Assert.Equal(390.25, result.PlannedDistanceKm, Tolerance);
        Assert.Equal(112.719, result.AverageDistanceKm, Tolerance);       // 450,875 / 4 = 112,71875
    }

    [Fact]
    public void TheLongestRoute_IsTheLongestOneThatWasNotCancelled()
    {
        var longest = Analyze(Scenario()).LongestRoute!;

        Assert.Equal("Bogotá", longest.Name);                            // la cancelada (100 km) no compite, aunque sea más larga que las demás
        Assert.Equal(390.25, longest.DistanceKm);
        Assert.Equal(4, longest.Stops);
        Assert.Equal(480, longest.EstimatedDurationMinutes);
        Assert.Equal("Planned", longest.Status);
    }

    [Fact]
    public void TheDelays_CountOnlyTheRoutesThatWereNotCancelled()
    {
        var result = Analyze(Scenario());

        Assert.Equal(2, result.DelayedRoutes);          // Centro (10 min) y Retrasada (45 min); los 5 min de la cancelada no cuentan
        Assert.Equal(55, result.TotalDelayMinutes);
    }

    [Fact]
    public void TheBreakdownByStatus_ListsEveryStatusInTheOrderOfTheEnum_CancelledOnesIncluded()
    {
        var byStatus = Analyze(Scenario()).ByStatus;

        Assert.Equal(new[] { "Planned", "InProgress", "Delayed", "Completed", "Cancelled" }, byStatus.Select(status => status.Status));
        Assert.Equal(new[] { 1, 1, 1, 1, 1 }, byStatus.Select(status => status.Routes));
        Assert.Equal(new[] { 390.25, 12.5, 40, 8.125, 100 }, byStatus.Select(status => status.DistanceKm));
    }

    [Fact]
    public void TheRouteList_ShowsEveryRoute_FromTheLongestToTheShortest()
    {
        var routes = Analyze(Scenario()).Routes;

        Assert.Equal(new[] { "Bogotá", "Cancelada", "Retrasada", "Girón", "Centro" }, routes.Select(route => route.Name));
        Assert.Equal(new[] { 390.25, 100, 40, 12.5, 8.125 }, routes.Select(route => route.DistanceKm));
    }

    [Fact]
    public void TwoRoutesOfTheSameLength_AreOrderedByName_SoTheLongestIsAlwaysTheSame()
    {
        var structure = Structure(routes: new[] { Route("B", distanceKm: 10), Route("A", distanceKm: 10), Route("C", distanceKm: 9) });

        var result = Analyze(structure);

        Assert.Equal("A", result.LongestRoute!.Name);
        Assert.Equal(new[] { "A", "B", "C" }, result.Routes.Select(route => route.Name));
    }

    [Fact]
    public void TheNumbers_AreRoundedToTheSamePrecisionAsTheRestOfTheApi()
    {
        var structure = Structure(routes: new[] { Route("Ruta", distanceKm: 12.3456, durationMinutes: 20.46) });

        var route = Analyze(structure).Routes.Single();

        Assert.Equal(12.346, route.DistanceKm);                 // 3 decimales
        Assert.Equal(20.5, route.EstimatedDurationMinutes);     // 1 decimal
    }

    [Fact]
    public void WithoutRoutes_EverythingIsZero_AndThereIsNoLongestRoute()
    {
        var result = Analyze(Structure());

        Assert.Equal(0, result.TotalRoutes);
        Assert.Equal(0, result.TotalDistanceKm);
        Assert.Equal(0, result.AverageDistanceKm);               // sin rutas no hay promedio que calcular (ni división entre cero)
        Assert.Null(result.LongestRoute);
        Assert.Equal(5, result.ByStatus.Count);
        Assert.All(result.ByStatus, status => Assert.Equal((0, 0.0), (status.Routes, status.DistanceKm)));
        Assert.Empty(result.Routes);
    }

    [Fact]
    public void WithOnlyCancelledRoutes_NothingWasTravelled_ButTheyAreStillListed()
    {
        var result = Analyze(Structure(routes: new[] { Route("Cancelada", DeliveryRouteStatus.Cancelled, distanceKm: 50) }));

        Assert.Equal(0, result.TotalRoutes);
        Assert.Equal(0, result.TotalDistanceKm);
        Assert.Null(result.LongestRoute);
        Assert.Single(result.Routes);
        Assert.Equal(1, result.ByStatus.Single(status => status.Status == "Cancelled").Routes);
    }

    [Fact]
    public void TheResult_DoesNotDependOnTheOrderTheRoutesAreVisited_NorOnVisitingOneTwice()
    {
        var routes = new[] { Route("A", distanceKm: 1), Route("B", DeliveryRouteStatus.Completed, distanceKm: 2), Route("C", DeliveryRouteStatus.Delayed, distanceKm: 3) };
        var forward = new RouteDistanceVisitor();
        var backwardAndRepeated = new RouteDistanceVisitor();

        foreach (var route in routes)
            RouteElement.From(route).Accept(forward);
        foreach (var route in routes.Reverse().Concat(routes))
            RouteElement.From(route).Accept(backwardAndRepeated);

        Assert.Equal(Json.Of(forward.Result), Json.Of(backwardAndRepeated.Result));
    }

    [Fact]
    public void TheVisitor_IgnoresVehiclesAndMaintenanceRecords()
    {
        var van = Vehicle("VAN-002");
        var withOthers = Structure(new[] { van }, new[] { Route("Ruta", distanceKm: 5) }, new[] { Record(van, MaintenanceType.OilChange, DateTime.UtcNow, 100) });
        var routesOnly = Structure(routes: new[] { Route("Ruta", distanceKm: 5) });

        Assert.Equal(Analyze(routesOnly).TotalDistanceKm, Analyze(withOthers).TotalDistanceKm);
    }
}
