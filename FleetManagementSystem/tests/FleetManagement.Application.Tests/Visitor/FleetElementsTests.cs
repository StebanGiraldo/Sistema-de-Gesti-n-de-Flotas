using System.Reflection;
using FleetManagement.Application.Analysis;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Xunit;
using static FleetManagement.Application.Tests.Visitor.FleetData;

namespace FleetManagement.Application.Tests.Visitor;

/// <summary>
/// ELEMENTOS del patrón Visitor: los adaptadores inmutables que presentan las entidades del dominio como elementos analizables, y
/// la estructura que los recorre. Se prueba que copian lo que el análisis necesita, que despachan al método correcto del visitante
/// (doble despacho) y que las entidades no se tocan.
/// </summary>
public class FleetElementsTests
{
    // ---------------- Adaptadores ----------------

    [Fact]
    public void AVehicleElement_CopiesWhatTheAnalysisNeeds()
    {
        var vehicle = Vehicle("TRK-001", VehicleType.Truck, VehicleStatus.Maintenance, 45210.5);

        var element = VehicleElement.From(vehicle);

        Assert.Equal(vehicle.Id, element.Id);
        Assert.Equal("TRK-001", element.LicensePlate);
        Assert.Equal(VehicleType.Truck, element.Type);
        Assert.Equal(VehicleStatus.Maintenance, element.Status);
        Assert.Equal(45210.5, element.MileageKm);
    }

    [Fact]
    public void ARouteElement_CopiesWhatTheAnalysisNeeds()
    {
        var vehicle = Vehicle("VAN-002");
        var route = Route("Ruta Girón", DeliveryRouteStatus.Delayed, vehicle, distanceKm: 12.5, stops: 3, durationMinutes: 25.5, delayMinutes: 20);

        var element = RouteElement.From(route);

        Assert.Equal(route.Id, element.Id);
        Assert.Equal("Ruta Girón", element.Name);
        Assert.Equal(DeliveryRouteStatus.Delayed, element.Status);
        Assert.Equal(vehicle.Id, element.AssignedVehicleId);
        Assert.Equal(12.5, element.DistanceKm);
        Assert.Equal(3, element.StopCount);
        Assert.Equal(25.5, element.EstimatedDurationMinutes);
        Assert.Equal(20, element.DelayMinutes);
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.Planned, false)]
    [InlineData(DeliveryRouteStatus.InProgress, true)]
    [InlineData(DeliveryRouteStatus.Delayed, true)]
    [InlineData(DeliveryRouteStatus.Completed, false)]
    [InlineData(DeliveryRouteStatus.Cancelled, false)]
    public void ARoute_IsActiveOnlyWhileItIsInProgressOrDelayed(DeliveryRouteStatus status, bool expected)
    {
        Assert.Equal(expected, RouteElement.From(Route("Ruta", status)).IsActive);
    }

    [Fact]
    public void AMaintenanceElement_CopiesWhatTheAnalysisNeeds()
    {
        var vehicle = Vehicle("CAR-003");
        var performedAt = new DateTime(2026, 5, 20, 8, 30, 0, DateTimeKind.Utc);
        var record = Record(vehicle, MaintenanceType.BrakeInspection, performedAt, 40000);

        var element = MaintenanceRecordElement.From(record);

        Assert.Equal(record.Id, element.Id);
        Assert.Equal(vehicle.Id, element.VehicleId);
        Assert.Equal(MaintenanceType.BrakeInspection, element.Type);
        Assert.Equal(performedAt, element.PerformedAt);
        Assert.Equal(40000, element.MileageAtServiceKm);
    }

    [Fact]
    public void TheAdapters_RejectANullEntity()
    {
        Assert.Throws<ArgumentNullException>(() => VehicleElement.From(null!));
        Assert.Throws<ArgumentNullException>(() => RouteElement.From(null!));
        Assert.Throws<ArgumentNullException>(() => MaintenanceRecordElement.From(null!));
    }

    [Fact]
    public void AnElement_IsASnapshot_ItDoesNotFollowLaterChangesOfTheEntity()
    {
        var vehicle = Vehicle("VAN-002", status: VehicleStatus.EnRoute, mileageKm: 1000);
        var element = VehicleElement.From(vehicle);

        vehicle.MileageKm = 2000;                       // la simulación sigue moviendo al vehículo mientras se analiza
        vehicle.Status = VehicleStatus.Available;

        Assert.Equal(1000, element.MileageKm);
        Assert.Equal(VehicleStatus.EnRoute, element.Status);
    }

    [Fact]
    public void TheElements_HoldNoReferenceToTheEntities_SoAVisitorCannotChangeThem()
    {
        var elementTypes = new[] { typeof(VehicleElement), typeof(RouteElement), typeof(MaintenanceRecordElement) };
        var entityTypes = new[] { typeof(Vehicle), typeof(DeliveryRoute), typeof(MaintenanceRecord), typeof(Waypoint) };

        const BindingFlags instanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var elementType in elementTypes)
        {
            // Lo que el elemento GUARDA (campos y propiedades de instancia). El método estático From sí recibe la entidad, pero no la conserva.
            var carried = elementType.GetFields(instanceMembers).Select(field => field.FieldType)
                .Concat(elementType.GetProperties(instanceMembers).Select(property => property.PropertyType));
            Assert.DoesNotContain(carried, type => entityTypes.Contains(type));
            Assert.True(elementType.IsSealed);
        }
    }

    [Fact]
    public void TheEntities_AreNotModified_TheyKnowNothingOfVisitors()
    {
        var entityTypes = new[] { typeof(Vehicle), typeof(DeliveryRoute), typeof(MaintenanceRecord) };

        foreach (var entity in entityTypes)
        {
            Assert.False(typeof(IFleetElement).IsAssignableFrom(entity), $"{entity.Name} no debe ser un elemento visitable");
            Assert.Null(entity.GetMethod("Accept"));
            Assert.DoesNotContain(SignatureDependencies.Of(entity), type => type.Namespace == "FleetManagement.Application.Analysis");
        }
    }

    // ---------------- Doble despacho ----------------

    [Fact]
    public void EachElement_CallsTheVisitMethodOfItsOwnKind_ExactlyOnce()
    {
        var vehicle = VehicleElement.From(Vehicle("VAN-002"));
        var route = RouteElement.From(Route("Ruta"));
        var record = MaintenanceRecordElement.From(Record(Vehicle("VAN-002"), MaintenanceType.OilChange, DateTime.UtcNow, 100));
        var visitor = new CallRecorder();

        vehicle.Accept(visitor);
        route.Accept(visitor);
        record.Accept(visitor);

        Assert.Equal(new[] { "vehicle:VAN-002", $"route:Ruta", "record:OilChange" }, visitor.Calls);
    }

    [Fact]
    public void AVisitorThatOverridesOneMethod_IgnoresTheOtherKindsOfElement()
    {
        var visitor = new RoutesOnlyVisitor();
        var structure = Structure(
            new[] { Vehicle("VAN-002") },
            new[] { Route("A"), Route("B") },
            new[] { Record(Vehicle("VAN-002"), MaintenanceType.Repair, DateTime.UtcNow, 10) });

        structure.Accept(visitor);

        Assert.Equal(new[] { "A", "B" }, visitor.Seen);   // los vehículos y los registros llegaron al método base, que no hace nada
    }

    // ---------------- Estructura de objetos ----------------

    [Fact]
    public void TheStructure_ListsVehiclesThenRoutesThenRecords_EachInAStableOrder()
    {
        var van = Vehicle("VAN-002");
        var truck = Vehicle("TRK-001");
        var older = Record(van, MaintenanceType.OilChange, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 100);
        var newer = Record(truck, MaintenanceType.Repair, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), 200);

        var structure = Structure(new[] { van, truck }, new[] { Route("Zeta"), Route("Alfa") }, new[] { newer, older });

        var order = structure.Elements.Select(element => element switch
        {
            VehicleElement vehicle => vehicle.LicensePlate,
            RouteElement route => route.Name,
            MaintenanceRecordElement record => record.Id == older.Id ? "registro-antiguo" : "registro-nuevo",
            _ => "?"
        });
        Assert.Equal(new[] { "TRK-001", "VAN-002", "Alfa", "Zeta", "registro-antiguo", "registro-nuevo" }, order);
    }

    [Fact]
    public void TheStructure_OrdersRoutesWithTheSameNameByIdSoTheOrderIsAlwaysTheSame()
    {
        var first = Route("Igual");
        var second = Route("Igual");

        var structure = Structure(routes: new[] { first, second });

        var expected = new[] { first.Id, second.Id }.Order().ToArray();
        Assert.Equal(expected, structure.Elements.OfType<RouteElement>().Select(route => route.Id));
    }

    [Fact]
    public void TheStructure_CountsEachKindOfElement()
    {
        var van = Vehicle("VAN-002");
        var structure = Structure(
            new[] { van, Vehicle("TRK-001") },
            new[] { Route("A") },
            new[] { Record(van, MaintenanceType.OilChange, DateTime.UtcNow, 1), Record(van, MaintenanceType.Repair, DateTime.UtcNow, 2), Record(van, MaintenanceType.Other, DateTime.UtcNow, 3) });

        Assert.Equal(2, structure.Count<VehicleElement>());
        Assert.Equal(1, structure.Count<RouteElement>());
        Assert.Equal(3, structure.Count<MaintenanceRecordElement>());
        Assert.Equal(6, structure.Elements.Count);
    }

    [Fact]
    public void AnEmptyFleet_IsAValidStructure()
    {
        var structure = Structure();

        Assert.Empty(structure.Elements);
        Assert.Equal(0, structure.Count<VehicleElement>());
    }

    [Fact]
    public void TheStructure_VisitsEveryElementOnce_InItsOrder()
    {
        var structure = Structure(new[] { Vehicle("VAN-002"), Vehicle("TRK-001") }, new[] { Route("Ruta") });
        var visitor = new CallRecorder();

        structure.Accept(visitor);

        Assert.Equal(new[] { "vehicle:TRK-001", "vehicle:VAN-002", "route:Ruta" }, visitor.Calls);
    }

    [Fact]
    public void Analyze_RunsTheVisitorOverEverythingAndReturnsItsResult()
    {
        var structure = Structure(routes: new[] { Route("A"), Route("B"), Route("C") });

        var routes = structure.Analyze(new RoutesOnlyVisitor());

        Assert.Equal(3, routes);
    }

    [Fact]
    public void TheStructure_RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => FleetElementStructure.Build(null!, Enumerable.Empty<DeliveryRoute>(), Enumerable.Empty<MaintenanceRecord>()));
        Assert.Throws<ArgumentNullException>(() => FleetElementStructure.Build(Enumerable.Empty<Vehicle>(), null!, Enumerable.Empty<MaintenanceRecord>()));
        Assert.Throws<ArgumentNullException>(() => FleetElementStructure.Build(Enumerable.Empty<Vehicle>(), Enumerable.Empty<DeliveryRoute>(), null!));
        Assert.Throws<ArgumentNullException>(() => Structure().Accept(null!));
    }

    [Fact]
    public void BuildingAndVisiting_DoesNotChangeTheEntities()
    {
        var vehicle = Vehicle("VAN-002", status: VehicleStatus.EnRoute, mileageKm: 1234.5);
        var route = Route("Ruta", DeliveryRouteStatus.InProgress, vehicle, distanceKm: 10, stops: 2, delayMinutes: 5);
        var record = Record(vehicle, MaintenanceType.OilChange, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 1000);
        var before = Snapshot(vehicle, route, record);

        var structure = Structure(new[] { vehicle }, new[] { route }, new[] { record });
        structure.Analyze(new VehicleUtilizationVisitor());
        structure.Analyze(new RouteDistanceVisitor());
        structure.Analyze(new MaintenanceHistoryVisitor());
        structure.Analyze(new OperationalInsightsVisitor());

        Assert.Equal(before, Snapshot(vehicle, route, record));
    }

    // Texto con todos los datos de las tres entidades (la geometría se serializa mal a JSON, así que se escriben a mano).
    private static string Snapshot(Vehicle vehicle, DeliveryRoute route, MaintenanceRecord record)
        => string.Join("|",
            vehicle.Id, vehicle.LicensePlate, vehicle.Type, vehicle.Status, vehicle.MileageKm, vehicle.CurrentLocation.X, vehicle.CurrentLocation.Y,
            route.Id, route.Name, route.Status, route.AssignedVehicleId, route.EstimatedDistanceKm, route.EstimatedDurationMinutes,
            route.DelayMinutes, route.Waypoints.Count, route.Origin.X, route.Origin.Y, route.Destination.X, route.Destination.Y,
            record.Id, record.VehicleId, record.Type, record.PerformedAt.ToString("O"), record.MileageAtServiceKm);

    [Fact]
    public void ANewKindOfAnalysis_NeedsOnlyANewVisitor_NotAChangeToTheElementsOrTheStructure()
    {
        // Abierto/cerrado: este visitante se escribe aquí, en la prueba, y funciona con los elementos y la estructura tal como están.
        var structure = Structure(new[] { Vehicle("A", mileageKm: 100), Vehicle("B", mileageKm: 250.5) });

        var totalKm = structure.Analyze(new TotalMileageVisitor());

        Assert.Equal(350.5, totalKm);
    }

    private sealed class CallRecorder : IFleetVisitor
    {
        public List<string> Calls { get; } = new();

        public void VisitVehicle(VehicleElement vehicle) => Calls.Add($"vehicle:{vehicle.LicensePlate}");
        public void VisitRoute(RouteElement route) => Calls.Add($"route:{route.Name}");
        public void VisitMaintenanceRecord(MaintenanceRecordElement record) => Calls.Add($"record:{record.Type}");
    }

    private sealed class RoutesOnlyVisitor : FleetVisitor<int>
    {
        public List<string> Seen { get; } = new();

        public override void VisitRoute(RouteElement route) => Seen.Add(route.Name);

        public override int Result => Seen.Count;
    }

    private sealed class TotalMileageVisitor : FleetVisitor<double>
    {
        private double _total;

        public override void VisitVehicle(VehicleElement vehicle) => _total += vehicle.MileageKm;

        public override double Result => _total;
    }
}
