using FleetManagement.Application.Analysis;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Enums;
using FleetManagement.Domain.States;
using Xunit;
using static FleetManagement.Application.Tests.Visitor.FleetData;

namespace FleetManagement.Application.Tests.Visitor;

/// <summary>
/// VISITANTE #4: información relevante para decidir en la operación. Cruza los tres tipos de elemento y, para saber si un vehículo
/// puede operar o seguir un viaje, pregunta a su estado (State). Cada regla se prueba con el escenario mínimo que la dispara y con el
/// que casi la dispara, y los mensajes se comparan completos porque son lo que lee el operador.
/// </summary>
public class OperationalInsightsVisitorTests
{
    private static DateTime Day(int month, int day) => new(2026, month, day, 0, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyList<OperationalInsightDto> Insights(
        FleetElementStructure structure, IVehicleStateResolver? states = null, InsightThresholds? thresholds = null)
        => structure.Analyze(new OperationalInsightsVisitor(states, thresholds));

    private static OperationalInsightDto Single(IEnumerable<OperationalInsightDto> insights, string code)
        => Assert.Single(insights, insight => insight.Code == code);

    // ---------------- Una flota sana no dice nada ----------------

    [Fact]
    public void AHealthyFleet_HasNothingToReport()
    {
        var van = Vehicle("VAN-002", status: VehicleStatus.EnRoute, mileageKm: 5000);
        var truck = Vehicle("TRK-001", VehicleType.Truck, mileageKm: 5000);
        var structure = Structure(
            new[] { van, truck },
            new[] { Route("En curso", DeliveryRouteStatus.InProgress, van, delayMinutes: 5), Route("Planificada", DeliveryRouteStatus.Planned, truck) },
            new[] { Record(van, MaintenanceType.OilChange, Day(1, 1), 4900), Record(truck, MaintenanceType.OilChange, Day(1, 1), 4500) });

        Assert.Empty(Insights(structure));
    }

    [Fact]
    public void AnEmptyFleet_HasNothingToReport()
    {
        Assert.Empty(Insights(Structure()));
    }

    // ---------------- Rutas en curso ----------------

    [Theory]
    [InlineData(VehicleStatus.Maintenance, "El vehículo CAR-003 está en mantenimiento; no se puede usar en una ruta.")]
    [InlineData(VehicleStatus.OutOfService, "El vehículo CAR-003 está fuera de servicio; no se puede usar en una ruta.")]
    public void AnActiveRoute_WhoseVehicleCannotOperate_IsCritical_WithTheReasonGivenByTheState(VehicleStatus status, string reason)
    {
        var car = Vehicle("CAR-003", VehicleType.Car, status);

        var insight = Single(Insights(Structure(new[] { car }, new[] { Route("Ruta Centro", DeliveryRouteStatus.InProgress, car) })), InsightCodes.ActiveRouteWithoutOperableVehicle);

        Assert.Equal("Critical", insight.Severity);
        Assert.Equal("Ruta Centro", insight.Subject);
        Assert.Equal($"La ruta 'Ruta Centro' está en curso pero no puede avanzar. {reason}", insight.Message);
    }

    [Fact]
    public void ADelayedRoute_CountsAsActiveToo()
    {
        var car = Vehicle("CAR-003", VehicleType.Car, VehicleStatus.Maintenance);

        var insights = Insights(Structure(new[] { car }, new[] { Route("Ruta retrasada", DeliveryRouteStatus.Delayed, car) }));

        Assert.Contains(insights, insight => insight.Code == InsightCodes.ActiveRouteWithoutOperableVehicle && insight.Subject == "Ruta retrasada");
    }

    [Fact]
    public void AnActiveRoute_WithoutAVehicle_IsCritical()
    {
        var insight = Single(Insights(Structure(routes: new[] { Route("Ruta huérfana", DeliveryRouteStatus.InProgress) })), InsightCodes.ActiveRouteWithoutOperableVehicle);

        Assert.Equal("La ruta 'Ruta huérfana' está en curso pero no puede avanzar. No tiene vehículo asignado: nadie la recorre.", insight.Message);
    }

    [Fact]
    public void AnActiveRoute_WhoseVehicleNoLongerExists_IsCritical()
    {
        var removed = Vehicle("GHOST-1");

        var insight = Single(Insights(Structure(routes: new[] { Route("Ruta", DeliveryRouteStatus.InProgress, removed) })), InsightCodes.ActiveRouteWithoutOperableVehicle);

        Assert.Equal("La ruta 'Ruta' está en curso pero no puede avanzar. Su vehículo asignado ya no existe.", insight.Message);
    }

    [Fact]
    public void AnActiveRoute_WhoseVehicleWasLeftAvailable_IsWarned_BecauseNothingMovesIt()
    {
        // Pasa cuando alguien deja "Disponible" a mano un vehículo cuya ruta sigue en marcha: el estado lo permite, pero ya nadie mueve la ruta.
        var van = Vehicle("VAN-002", status: VehicleStatus.Available);

        var insight = Single(Insights(Structure(new[] { van }, new[] { Route("Ruta Girón", DeliveryRouteStatus.InProgress, van) })), InsightCodes.ActiveRouteVehicleNotEnRoute);

        Assert.Equal("Warning", insight.Severity);
        Assert.Equal("Ruta Girón", insight.Subject);
        Assert.Equal("La ruta 'Ruta Girón' está en curso pero su vehículo VAN-002 no está En ruta (estado: Available): la simulación no la mueve.", insight.Message);
    }

    [Fact]
    public void AnActiveRoute_WithAVehicleThatIsEnRoute_IsFine()
    {
        var van = Vehicle("VAN-002", status: VehicleStatus.EnRoute);

        Assert.Empty(Insights(Structure(new[] { van }, new[] { Route("Ruta", DeliveryRouteStatus.InProgress, van) })));
    }

    [Fact]
    public void ABlockedVehicle_IsReportedOnce_AsCritical_NotAlsoAsNotEnRoute()
    {
        var car = Vehicle("CAR-003", VehicleType.Car, VehicleStatus.Maintenance);

        var insights = Insights(Structure(new[] { car }, new[] { Route("Ruta", DeliveryRouteStatus.InProgress, car) }));

        Assert.Equal(new[] { InsightCodes.ActiveRouteWithoutOperableVehicle }, insights.Select(insight => insight.Code));
    }

    [Theory]
    [InlineData(29, false)]
    [InlineData(30, true)]    // el umbral es "a partir de": 30 min ya avisa
    [InlineData(31, true)]
    public void ADelayOnAnActiveRoute_IsReportedFromTheThreshold(int delayMinutes, bool reported)
    {
        var van = Vehicle("VAN-002", status: VehicleStatus.EnRoute);

        var insights = Insights(Structure(new[] { van }, new[] { Route("Ruta", DeliveryRouteStatus.Delayed, van, delayMinutes: delayMinutes) }));

        Assert.Equal(reported, insights.Any(insight => insight.Code == InsightCodes.ActiveRouteDelayed));
    }

    [Fact]
    public void TheDelayMessage_SaysHowLateTheRouteIsAndWhatTheThresholdWas()
    {
        var van = Vehicle("VAN-002", status: VehicleStatus.EnRoute);

        var insight = Single(Insights(Structure(new[] { van }, new[] { Route("Ruta Centro", DeliveryRouteStatus.Delayed, van, delayMinutes: 45) })), InsightCodes.ActiveRouteDelayed);

        Assert.Equal("Warning", insight.Severity);
        Assert.Equal("La ruta 'Ruta Centro' acumula 45 min de retraso (umbral: 30 min).", insight.Message);
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.Planned)]
    [InlineData(DeliveryRouteStatus.Completed)]
    [InlineData(DeliveryRouteStatus.Cancelled)]
    public void ADelayOnARouteThatIsNotActive_IsNotReported(DeliveryRouteStatus status)
    {
        var van = Vehicle("VAN-002");

        Assert.DoesNotContain(
            Insights(Structure(new[] { van }, new[] { Route("Ruta", status, van, delayMinutes: 120) })),
            insight => insight.Code == InsightCodes.ActiveRouteDelayed);
    }

    // ---------------- Rutas planificadas ----------------

    [Fact]
    public void APlannedRoute_WithoutAVehicle_IsJustInformation()
    {
        var insight = Single(Insights(Structure(routes: new[] { Route("Ruta Bogotá", DeliveryRouteStatus.Planned) })), InsightCodes.PlannedRouteWithoutVehicle);

        Assert.Equal("Info", insight.Severity);
        Assert.Equal("La ruta planificada 'Ruta Bogotá' todavía no tiene vehículo asignado.", insight.Message);
    }

    [Theory]
    [InlineData(VehicleStatus.Maintenance, "El vehículo CAR-003 está en mantenimiento; no se puede usar en una ruta.")]
    [InlineData(VehicleStatus.OutOfService, "El vehículo CAR-003 está fuera de servicio; no se puede usar en una ruta.")]
    public void APlannedRoute_WhoseVehicleCannotOperate_IsWarned_WithTheReasonGivenByTheState(VehicleStatus status, string reason)
    {
        var car = Vehicle("CAR-003", VehicleType.Car, status);

        var insight = Single(Insights(Structure(new[] { car }, new[] { Route("Ruta Bogotá", DeliveryRouteStatus.Planned, car) })), InsightCodes.PlannedRouteVehicleNotOperable);

        Assert.Equal("Warning", insight.Severity);
        Assert.Equal("Ruta Bogotá", insight.Subject);
        Assert.Equal($"La ruta planificada 'Ruta Bogotá' no podrá iniciarse así. {reason}", insight.Message);
    }

    [Fact]
    public void APlannedRoute_WhoseVehicleNoLongerExists_IsWarned()
    {
        var removed = Vehicle("GHOST-1");

        var insight = Single(Insights(Structure(routes: new[] { Route("Ruta", DeliveryRouteStatus.Planned, removed) })), InsightCodes.PlannedRouteVehicleNotOperable);

        Assert.Equal("La ruta planificada 'Ruta' no podrá iniciarse así. Su vehículo asignado ya no existe.", insight.Message);
    }

    [Theory]
    [InlineData(VehicleStatus.Available)]
    [InlineData(VehicleStatus.EnRoute)]
    public void APlannedRoute_WithAnOperableVehicle_IsFine(VehicleStatus status)
    {
        var van = Vehicle("VAN-002", status: status);

        // (un vehículo En ruta sin ruta en curso sí genera su propio aviso, pero no uno sobre la ruta planificada)
        Assert.DoesNotContain(
            Insights(Structure(new[] { van }, new[] { Route("Ruta", DeliveryRouteStatus.Planned, van) })),
            insight => insight.Subject == "Ruta");
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.Completed)]
    [InlineData(DeliveryRouteStatus.Cancelled)]
    public void ARouteThatIsOver_NeverProducesRouteInsights(DeliveryRouteStatus status)
    {
        var car = Vehicle("CAR-003", VehicleType.Car, VehicleStatus.Maintenance);

        Assert.Empty(Insights(Structure(new[] { car }, new[] { Route("Ruta", status, car, delayMinutes: 90) })));
    }

    // ---------------- Vehículos ----------------

    [Fact]
    public void AVehicleEnRoute_WithoutAnActiveRoute_IsWarned()
    {
        var van = Vehicle("VAN-002", status: VehicleStatus.EnRoute);

        var insight = Single(Insights(Structure(new[] { van })), InsightCodes.EnRouteVehicleWithoutActiveRoute);

        Assert.Equal("Warning", insight.Severity);
        Assert.Equal("VAN-002", insight.Subject);
        Assert.Equal("El vehículo VAN-002 figura En ruta pero no tiene ninguna ruta en curso: la simulación no lo mueve.", insight.Message);
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.Planned)]
    [InlineData(DeliveryRouteStatus.Completed)]
    [InlineData(DeliveryRouteStatus.Cancelled)]
    public void AVehicleEnRoute_WhoseRoutesAreNotActive_IsStillWarned(DeliveryRouteStatus status)
    {
        var van = Vehicle("VAN-002", status: VehicleStatus.EnRoute);

        var insights = Insights(Structure(new[] { van }, new[] { Route("Ruta", status, van) }));

        Assert.Contains(insights, insight => insight.Code == InsightCodes.EnRouteVehicleWithoutActiveRoute);
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.InProgress)]
    [InlineData(DeliveryRouteStatus.Delayed)]
    public void AVehicleEnRoute_WithAnActiveRoute_IsNotWarned(DeliveryRouteStatus status)
    {
        var van = Vehicle("VAN-002", status: VehicleStatus.EnRoute);

        Assert.DoesNotContain(
            Insights(Structure(new[] { van }, new[] { Route("Ruta", status, van) })),
            insight => insight.Code == InsightCodes.EnRouteVehicleWithoutActiveRoute);
    }

    [Theory]
    [InlineData(VehicleStatus.Available)]
    [InlineData(VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService)]
    public void AVehicleThatIsNotEnRoute_WithoutRoutes_IsNotWarnedAboutTrips(VehicleStatus status)
    {
        Assert.Empty(Insights(Structure(new[] { Vehicle("VAN-002", status: status) })));
    }

    // ---------------- Kilómetros sin mantenimiento ----------------

    [Fact]
    public void AVehicleThatWasNeverServiced_IsWarnedOnceItReachesTheThreshold()
    {
        var van = Vehicle("VAN-002", mileageKm: 12000);

        var insight = Single(Insights(Structure(new[] { van })), InsightCodes.ServiceDistanceExceeded);

        Assert.Equal("Warning", insight.Severity);
        Assert.Equal("VAN-002", insight.Subject);
        Assert.Equal("El vehículo VAN-002 no tiene ningún mantenimiento registrado y lleva 12000 km (umbral: 10000 km).", insight.Message);
    }

    [Fact]
    public void AServicedVehicle_IsWarnedByTheKilometersSinceItsLatestService()
    {
        var van = Vehicle("VAN-002", mileageKm: 25000.5);

        var insight = Single(
            Insights(Structure(new[] { van }, records: new[] { Record(van, MaintenanceType.OilChange, Day(1, 1), 14000) })),
            InsightCodes.ServiceDistanceExceeded);

        Assert.Equal("El vehículo VAN-002 lleva 11000.5 km desde su último mantenimiento (umbral: 10000 km).", insight.Message);
    }

    [Theory]
    [InlineData(9999.9, false)]
    [InlineData(10000, true)]     // "a partir de": justo el umbral ya avisa
    [InlineData(10000.1, true)]
    public void TheServiceDistance_IsReportedFromTheThreshold(double kilometers, bool reported)
    {
        var van = Vehicle("VAN-002", mileageKm: kilometers);

        Assert.Equal(reported, Insights(Structure(new[] { van })).Any(insight => insight.Code == InsightCodes.ServiceDistanceExceeded));
    }

    [Fact]
    public void OnlyTheLatestService_CountsForTheDistance()
    {
        var van = Vehicle("VAN-002", mileageKm: 30000);
        var records = new[] { Record(van, MaintenanceType.OilChange, Day(1, 1), 5000), Record(van, MaintenanceType.Repair, Day(6, 1), 29000) };

        Assert.DoesNotContain(Insights(Structure(new[] { van }, records: records)), insight => insight.Code == InsightCodes.ServiceDistanceExceeded);
    }

    // ---------------- Umbrales ----------------

    [Fact]
    public void TheThresholds_CanBeChangedWithoutTouchingTheVisitor()
    {
        var van = Vehicle("VAN-002", status: VehicleStatus.EnRoute, mileageKm: 700);
        var structure = Structure(new[] { van }, new[] { Route("Ruta", DeliveryRouteStatus.InProgress, van, delayMinutes: 12) });
        var strict = new InsightThresholds(DelayAlertMinutes: 10, ServiceDistanceAlertKm: 500);

        Assert.Empty(Insights(structure));   // con los umbrales por defecto no hay nada que avisar

        var insights = Insights(structure, thresholds: strict);
        Assert.Equal("La ruta 'Ruta' acumula 12 min de retraso (umbral: 10 min).", Single(insights, InsightCodes.ActiveRouteDelayed).Message);
        Assert.Equal("El vehículo VAN-002 no tiene ningún mantenimiento registrado y lleva 700 km (umbral: 500 km).", Single(insights, InsightCodes.ServiceDistanceExceeded).Message);
    }

    [Fact]
    public void TheDefaultThresholds_AreThirtyMinutesAndTenThousandKilometers()
    {
        var defaults = new InsightThresholds();

        Assert.Equal(30, defaults.DelayAlertMinutes);
        Assert.Equal(10_000, defaults.ServiceDistanceAlertKm);
    }

    // ---------------- Orden de los avisos ----------------

    [Fact]
    public void TheInsights_AreListedFromTheMostToTheLeastUrgent_ThenByCode_ThenBySubject()
    {
        var shop = Vehicle("CAR-003", VehicleType.Car, VehicleStatus.Maintenance);
        var idleEnRoute = Vehicle("MOT-004", VehicleType.Motorcycle, VehicleStatus.EnRoute);
        var worn = Vehicle("TRK-001", VehicleType.Truck, mileageKm: 50000);
        var routes = new[]
        {
            Route("Ruta B sin vehículo", DeliveryRouteStatus.Planned),
            Route("Ruta A sin vehículo", DeliveryRouteStatus.Planned),
            Route("Ruta bloqueada", DeliveryRouteStatus.InProgress, shop)
        };

        var ordered = Insights(Structure(new[] { shop, idleEnRoute, worn }, routes)).Select(insight => (insight.Severity, insight.Code, insight.Subject)).ToArray();

        Assert.Equal(
            new[]
            {
                ("Critical", InsightCodes.ActiveRouteWithoutOperableVehicle, "Ruta bloqueada"),
                ("Warning", InsightCodes.EnRouteVehicleWithoutActiveRoute, "MOT-004"),
                ("Warning", InsightCodes.ServiceDistanceExceeded, "TRK-001"),
                ("Info", InsightCodes.PlannedRouteWithoutVehicle, "Ruta A sin vehículo"),
                ("Info", InsightCodes.PlannedRouteWithoutVehicle, "Ruta B sin vehículo")
            },
            ordered);
    }

    [Fact]
    public void TheResult_DoesNotDependOnTheOrderTheElementsAreVisited_NorOnVisitingOneTwice()
    {
        var shop = Vehicle("CAR-003", VehicleType.Car, VehicleStatus.Maintenance, 20000);
        var van = Vehicle("VAN-002", status: VehicleStatus.EnRoute);
        var structure = Structure(
            new[] { shop, van },
            new[] { Route("Ruta bloqueada", DeliveryRouteStatus.InProgress, shop), Route("Planificada", DeliveryRouteStatus.Planned) },
            new[] { Record(shop, MaintenanceType.Repair, Day(1, 1), 1000) });
        var inOrder = new OperationalInsightsVisitor();
        var shuffled = new OperationalInsightsVisitor();

        structure.Accept(inOrder);
        foreach (var element in structure.Elements.Reverse().Concat(structure.Elements))
            element.Accept(shuffled);

        Assert.Equal(Json.Of(inOrder.Result), Json.Of(shuffled.Result));
        Assert.True(inOrder.Result.Count >= 3);
    }

    // ---------------- State: el visitante le pregunta al estado ----------------

    [Fact]
    public void WhetherAVehicleCanOperate_AndWhy_IsDecidedByTheState_NotByTheVisitor()
    {
        var shop = Vehicle("CAR-003", VehicleType.Car, VehicleStatus.Maintenance);
        var structure = Structure(new[] { shop }, new[] { Route("Ruta", DeliveryRouteStatus.InProgress, shop) });
        var custom = new ConfigurableState(VehicleStatus.Maintenance, canOperate: false, canTrackTrip: false, blockReason: "Bloqueado por la prueba");

        var insight = Single(Insights(structure, new ConfigurableResolver().Override(custom)), InsightCodes.ActiveRouteWithoutOperableVehicle);

        Assert.Equal("La ruta 'Ruta' está en curso pero no puede avanzar. Bloqueado por la prueba (CAR-003)", insight.Message);   // el texto lo puso el estado
        Assert.Contains("CAR-003", custom.BlockReasonAskedFor);
    }

    [Fact]
    public void IfTheStateAllowsOperating_ThereIsNoCriticalInsight_ButTheTripStillIsNotFollowed()
    {
        var shop = Vehicle("CAR-003", VehicleType.Car, VehicleStatus.Maintenance);
        var structure = Structure(new[] { shop }, new[] { Route("Ruta", DeliveryRouteStatus.InProgress, shop) });
        var permissive = new ConfigurableResolver().Override(new ConfigurableState(VehicleStatus.Maintenance, canOperate: true, canTrackTrip: false));

        var insights = Insights(structure, permissive);

        Assert.DoesNotContain(insights, insight => insight.Code == InsightCodes.ActiveRouteWithoutOperableVehicle);
        Assert.Contains(insights, insight => insight.Code == InsightCodes.ActiveRouteVehicleNotEnRoute);
    }

    [Fact]
    public void IfTheStateFollowsTrips_AnActiveRouteWithThatVehicleIsFine()
    {
        var van = Vehicle("VAN-002", status: VehicleStatus.Available);
        var structure = Structure(new[] { van }, new[] { Route("Ruta", DeliveryRouteStatus.InProgress, van) });
        var following = new ConfigurableResolver().Override(new ConfigurableState(VehicleStatus.Available, canOperate: true, canTrackTrip: true));

        Assert.Empty(Insights(structure, following));
    }
}
