using FleetManagement.Application.Analysis;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Enums;
using FleetManagement.Domain.States;
using Xunit;
using static FleetManagement.Application.Tests.Visitor.FleetData;

namespace FleetManagement.Application.Tests.Visitor;

/// <summary>
/// VISITANTE #1: uso de los vehículos. Cada valor esperado se calculó a mano a partir de una flota pequeña. Quién "puede operar" no
/// lo decide el visitante: se lo pregunta al estado del vehículo (State).
/// </summary>
public class VehicleUtilizationVisitorTests
{
    private const double Tolerance = 1e-9;

    // Flota de la prueba: 5 vehículos (3 pueden operar) y 5 rutas. Solo VAN-002 está en uso (ruta en curso y vehículo En ruta).
    private static FleetElementStructure Scenario()
    {
        var truck = Vehicle("TRK-001", VehicleType.Truck, VehicleStatus.Available, 45210.5);
        var van = Vehicle("VAN-002", VehicleType.Van, VehicleStatus.EnRoute, 18340.25);
        var car = Vehicle("CAR-003", VehicleType.Car, VehicleStatus.Maintenance, 62870);
        var moto = Vehicle("MOT-004", VehicleType.Motorcycle, VehicleStatus.Available, 9120);
        var brokenVan = Vehicle("VAN-005", VehicleType.Van, VehicleStatus.OutOfService, 98450);
        var routes = new[]
        {
            Route("En curso", DeliveryRouteStatus.InProgress, van),
            Route("Terminada de la furgoneta", DeliveryRouteStatus.Completed, van),
            Route("Terminada del camión", DeliveryRouteStatus.Completed, truck),
            Route("Planificada del camión", DeliveryRouteStatus.Planned, truck),
            Route("En curso sin vehículo", DeliveryRouteStatus.Delayed)
        };
        return Structure(new[] { truck, van, car, moto, brokenVan }, routes);
    }

    private static VehicleUtilizationDto Analyze(FleetElementStructure structure, IVehicleStateResolver? states = null)
        => structure.Analyze(new VehicleUtilizationVisitor(states));

    [Fact]
    public void TheTotals_AreCalculatedFromTheWholeFleet()
    {
        var result = Analyze(Scenario());

        Assert.Equal(5, result.TotalVehicles);
        Assert.Equal(3, result.OperableVehicles);                          // Disponible, En ruta, Disponible
        Assert.Equal(1, result.InUseVehicles);                             // solo VAN-002
        Assert.Equal(33.3, result.UtilizationPercent);                     // 1 de 3 operables
        Assert.Equal(233990.75, result.TotalMileageKm, Tolerance);         // 45210.5 + 18340.25 + 62870 + 9120 + 98450
        Assert.Equal(46798.15, result.AverageMileageKm, Tolerance);        // 233990.75 / 5
    }

    [Fact]
    public void EachVehicle_ShowsItsStatusItsMileageItsRoutesAndWhetherItIsInUse()
    {
        var result = Analyze(Scenario());

        Assert.Equal(new[] { "CAR-003", "MOT-004", "TRK-001", "VAN-002", "VAN-005" }, result.Vehicles.Select(v => v.LicensePlate)); // por placa

        var van = result.Vehicles.Single(v => v.LicensePlate == "VAN-002");
        Assert.Equal("Van", van.Type);
        Assert.Equal("EnRoute", van.Status);
        Assert.Equal(18340.25, van.MileageKm);
        Assert.Equal(2, van.RoutesAssigned);       // la que está en curso y la terminada
        Assert.Equal(1, van.RoutesCompleted);
        Assert.True(van.InUse);

        var truck = result.Vehicles.Single(v => v.LicensePlate == "TRK-001");
        Assert.Equal(2, truck.RoutesAssigned);     // una terminada y una planificada
        Assert.Equal(1, truck.RoutesCompleted);
        Assert.False(truck.InUse);                 // puede operar, pero no tiene una ruta en curso

        var car = result.Vehicles.Single(v => v.LicensePlate == "CAR-003");
        Assert.Equal(0, car.RoutesAssigned);
        Assert.False(car.InUse);
    }

    [Fact]
    public void TheBreakdownByType_ListsEveryTypeInTheOrderOfTheEnum_WithZerosWhereThereIsNothing()
    {
        var result = Analyze(Scenario());

        Assert.Equal(new[] { "Truck", "Van", "Car", "Motorcycle" }, result.ByType.Select(t => t.Type));
        var van = result.ByType.Single(t => t.Type == "Van");
        Assert.Equal(2, van.Vehicles);                             // VAN-002 y VAN-005
        Assert.Equal(1, van.InUse);
        Assert.Equal(58395.125, van.AverageMileageKm, Tolerance);  // (18340.25 + 98450) / 2
        var truck = result.ByType.Single(t => t.Type == "Truck");
        Assert.Equal((1, 0, 45210.5), (truck.Vehicles, truck.InUse, truck.AverageMileageKm));
    }

    [Fact]
    public void AVehicleThatCannotOperate_IsNeverInUse_EvenWithAnActiveRoute()
    {
        var broken = Vehicle("VAN-005", status: VehicleStatus.OutOfService);
        var inShop = Vehicle("CAR-003", VehicleType.Car, VehicleStatus.Maintenance);
        var structure = Structure(new[] { broken, inShop }, new[] { Route("A", DeliveryRouteStatus.InProgress, broken), Route("B", DeliveryRouteStatus.Delayed, inShop) });

        var result = Analyze(structure);

        Assert.Equal(0, result.InUseVehicles);
        Assert.Equal(0, result.OperableVehicles);
        Assert.Equal(0, result.UtilizationPercent);     // sin vehículos operables no hay porcentaje que calcular (ni división entre cero)
        Assert.All(result.Vehicles, usage => Assert.False(usage.InUse));
    }

    [Fact]
    public void AnAvailableVehicle_WithAnActiveRoute_CountsAsInUse()
    {
        // Es operable y la ruta está en curso: aunque su estado no sea "En ruta", la ruta lo está usando.
        var vehicle = Vehicle("VAN-002", status: VehicleStatus.Available);

        var result = Analyze(Structure(new[] { vehicle }, new[] { Route("Ruta", DeliveryRouteStatus.InProgress, vehicle) }));

        Assert.Equal(1, result.InUseVehicles);
        Assert.Equal(100, result.UtilizationPercent);
    }

    [Fact]
    public void ARouteThatIsNotActive_DoesNotPutAVehicleInUse()
    {
        var vehicle = Vehicle("VAN-002", status: VehicleStatus.EnRoute);
        var routes = new[]
        {
            Route("Planificada", DeliveryRouteStatus.Planned, vehicle),
            Route("Terminada", DeliveryRouteStatus.Completed, vehicle),
            Route("Cancelada", DeliveryRouteStatus.Cancelled, vehicle)
        };

        var result = Analyze(Structure(new[] { vehicle }, routes));

        Assert.Equal(0, result.InUseVehicles);
        Assert.Equal(3, result.Vehicles.Single().RoutesAssigned);
        Assert.Equal(1, result.Vehicles.Single().RoutesCompleted);
    }

    [Fact]
    public void ThePercentage_IsRoundedToOneDecimal_AndTheMileageToThree()
    {
        var a = Vehicle("A", status: VehicleStatus.EnRoute, mileageKm: 1000.12345);
        var b = Vehicle("B", mileageKm: 2000.54321);
        var c = Vehicle("C", mileageKm: 3000);
        var structure = Structure(new[] { a, b, c }, new[] { Route("R1", DeliveryRouteStatus.InProgress, a), Route("R2", DeliveryRouteStatus.InProgress, b) });

        var result = Analyze(structure);

        Assert.Equal(66.7, result.UtilizationPercent);                  // 2 de 3 = 66,666…
        Assert.Equal(1000.123, result.Vehicles.Single(v => v.LicensePlate == "A").MileageKm);
        Assert.Equal(2000.543, result.Vehicles.Single(v => v.LicensePlate == "B").MileageKm);
        Assert.Equal(6000.666, result.TotalMileageKm);                  // 1000,123 + 2000,543 + 3000: la suma de lo que se muestra
    }

    [Fact]
    public void TheTotalMileage_IsTheSumOfTheVehiclesListed_SoTheClientCanCheckItByAdding()
    {
        var result = Analyze(Scenario());

        Assert.Equal(result.Vehicles.Sum(vehicle => vehicle.MileageKm), result.TotalMileageKm, Tolerance);
        Assert.Equal(result.TotalMileageKm / result.TotalVehicles, result.AverageMileageKm, 1e-3);
    }

    [Fact]
    public void AnEmptyFleet_GivesZerosAndEveryType_NotAnError()
    {
        var result = Analyze(Structure());

        Assert.Equal(0, result.TotalVehicles);
        Assert.Equal(0, result.UtilizationPercent);
        Assert.Equal(0, result.AverageMileageKm);
        Assert.Empty(result.Vehicles);
        Assert.Equal(4, result.ByType.Count);
        Assert.All(result.ByType, type => Assert.Equal((0, 0, 0.0), (type.Vehicles, type.InUse, type.AverageMileageKm)));
    }

    [Fact]
    public void TheResult_DoesNotDependOnTheOrderTheElementsAreVisited()
    {
        var truck = Vehicle("TRK-001", VehicleType.Truck, VehicleStatus.Available, 100);
        var van = Vehicle("VAN-002", VehicleType.Van, VehicleStatus.EnRoute, 200);
        var route = Route("Ruta", DeliveryRouteStatus.InProgress, van);
        var routeFirst = new VehicleUtilizationVisitor();
        var vehiclesFirst = new VehicleUtilizationVisitor();

        RouteElement.From(route).Accept(routeFirst);
        VehicleElement.From(van).Accept(routeFirst);
        VehicleElement.From(truck).Accept(routeFirst);
        VehicleElement.From(truck).Accept(vehiclesFirst);
        VehicleElement.From(van).Accept(vehiclesFirst);
        RouteElement.From(route).Accept(vehiclesFirst);

        Assert.Equal(Json.Of(vehiclesFirst.Result), Json.Of(routeFirst.Result));
    }

    [Fact]
    public void VisitingTheSameElementTwice_DoesNotCountItTwice()
    {
        var van = Vehicle("VAN-002", status: VehicleStatus.EnRoute, mileageKm: 500);
        var visitor = new VehicleUtilizationVisitor();

        VehicleElement.From(van).Accept(visitor);
        VehicleElement.From(van).Accept(visitor);

        Assert.Equal(1, visitor.Result.TotalVehicles);
        Assert.Equal(500, visitor.Result.TotalMileageKm);
    }

    [Fact]
    public void AskingForTheResultTwice_GivesTheSameAnswer()
    {
        var visitor = new VehicleUtilizationVisitor();
        Scenario().Accept(visitor);

        Assert.Equal(Json.Of(visitor.Result), Json.Of(visitor.Result));
    }

    // ---------------- State: el visitante le pregunta al estado ----------------

    [Fact]
    public void WhoCanOperate_IsDecidedByTheState_NotByTheVisitor()
    {
        var inShop = Vehicle("CAR-003", VehicleType.Car, VehicleStatus.Maintenance, 100);
        var structure = Structure(new[] { inShop }, new[] { Route("Ruta", DeliveryRouteStatus.InProgress, inShop) });

        var normal = Analyze(structure);
        // Un estado "En mantenimiento" distinto, que sí deja operar: el visitante lo refleja sin que nadie lo modifique.
        var permissive = Analyze(structure, new ConfigurableResolver().Override(new ConfigurableState(VehicleStatus.Maintenance, canOperate: true, canTrackTrip: false)));

        Assert.Equal((0, 0), (normal.OperableVehicles, normal.InUseVehicles));
        Assert.Equal((1, 1), (permissive.OperableVehicles, permissive.InUseVehicles));
    }

    [Fact]
    public void WithoutAResolver_TheVisitorUsesTheRealStates()
    {
        var explicitly = Analyze(Scenario(), VehicleStateResolver.Default);
        var byDefault = Analyze(Scenario());

        Assert.Equal(explicitly.OperableVehicles, byDefault.OperableVehicles);
        Assert.Equal(3, byDefault.OperableVehicles);
    }
}
