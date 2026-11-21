using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Persistence.Demo;
using Xunit;

namespace FleetManagement.Application.Tests.Demo;

/// <summary>
/// Las tablas de datos de demostración (sin sembrar nada): comprueban que lo que se promete en el documento de cumplimiento
/// —más operadores, más vehículos, al menos 20 con ruta y carga— está de verdad en los datos y que ninguna fila rompe las reglas.
/// </summary>
public class DemoFleetCatalogTests
{
    private static readonly string[] OriginalPlates = { "TRK-001", "VAN-002", "CAR-003", "MOT-004", "VAN-005" };
    private static readonly string[] OriginalUsers = { "admin", "operador1", "operador2" };
    private const double Tolerance = 1e-9;

    private static DemoVehicle VehicleOf(string plate) => DemoVehicles.All.Single(vehicle => vehicle.Plate == plate);

    private static bool IsActive(DemoRoute route) => route.Status is DeliveryRouteStatus.InProgress or DeliveryRouteStatus.Delayed;

    // ---------- Operadores ----------

    [Fact]
    public void TenNewOperators_WithDistinctUsernamesAndLicenses()
    {
        Assert.Equal(10, DemoOperators.All.Count);
        Assert.Equal(10, DemoOperators.All.Select(op => op.Username).Distinct().Count());
        Assert.Equal(10, DemoOperators.All.Select(op => op.LicenseNumber).Distinct().Count());
        Assert.Empty(DemoOperators.All.Select(op => op.Username).Intersect(OriginalUsers));
    }

    [Fact]
    public void OperatorUsernames_AreOperador3ToOperador12()
        => Assert.Equal(Enumerable.Range(3, 10).Select(n => $"operador{n}"), DemoOperators.All.Select(op => op.Username));

    // ---------- Vehículos ----------

    [Fact]
    public void ThirtyNewVehicles_WithUniquePlates_ThatDoNotClashWithTheOriginalFive()
    {
        var plates = DemoVehicles.All.Select(vehicle => vehicle.Plate).ToList();

        Assert.Equal(30, plates.Count);
        Assert.Equal(30, plates.Distinct().Count());
        Assert.Empty(plates.Intersect(OriginalPlates));
    }

    [Fact]
    public void EveryVehicle_HasPositiveCapacitiesAndMileage()
        => Assert.All(DemoVehicles.All, vehicle =>
        {
            Assert.True(vehicle.CapacityKg > 0, vehicle.Plate);
            Assert.True(vehicle.CapacityM3 > 0, vehicle.Plate);
            Assert.True(vehicle.MileageKm > 0, vehicle.Plate);
        });

    [Fact]
    public void TheFleetCoversEveryVehicleTypeAndEveryStatus()
    {
        Assert.Equal(4, DemoVehicles.All.Select(vehicle => vehicle.Type).Distinct().Count());
        Assert.Equal(4, DemoVehicles.All.Select(vehicle => vehicle.Status).Distinct().Count());
    }

    [Fact]
    public void AtLeastSixAvailableVehiclesHaveNoRoute_SoTheyCanBeAssignedFromThePanel()
    {
        var withRoute = DemoRoutes.All.Select(route => route.Plate).ToHashSet();

        var free = DemoVehicles.All.Where(vehicle => !withRoute.Contains(vehicle.Plate) && vehicle.Status == VehicleStatus.Available).ToList();

        Assert.True(free.Count >= 6, $"Solo hay {free.Count} vehículos libres y disponibles.");
        Assert.Contains(free, vehicle => vehicle.Type == VehicleType.Truck);
        Assert.Contains(free, vehicle => vehicle.Type == VehicleType.Van);
        Assert.Contains(free, vehicle => vehicle.Type == VehicleType.Motorcycle);
    }

    // ---------- Rutas con carga ----------

    [Fact]
    public void TwentyTwoNewRoutes_TenActiveSixPlannedSixCompleted()
    {
        Assert.Equal(22, DemoRoutes.All.Count);
        Assert.Equal(10, DemoRoutes.All.Count(IsActive));
        Assert.Equal(6, DemoRoutes.All.Count(route => route.Status == DeliveryRouteStatus.Planned));
        Assert.Equal(6, DemoRoutes.All.Count(route => route.Status == DeliveryRouteStatus.Completed));
    }

    [Fact]
    public void AtLeastTwentyDifferentVehicles_HaveARouteWithCargo()
    {
        var vehiclesWithCargo = DemoRoutes.All.Where(route => route.Cargo.Count > 0).Select(route => route.Plate).Distinct().Count();

        Assert.True(vehiclesWithCargo >= 20, $"Solo {vehiclesWithCargo} vehículos tienen ruta con carga.");
    }

    [Fact]
    public void EveryRoute_HasAtLeastTwoCargoItemsAWaypointAndAUniqueName()
    {
        Assert.All(DemoRoutes.All, route =>
        {
            Assert.True(route.Cargo.Count >= 2, route.Name);
            Assert.NotEmpty(route.Via);
            Assert.True(route.DistanceKm > 0, route.Name);
            Assert.NotEqual(route.Origin, route.Destination);
        });
        Assert.Equal(DemoRoutes.All.Count, DemoRoutes.All.Select(route => route.Name).Distinct().Count());
    }

    [Fact]
    public void EveryRoute_UsesAnExistingVehicleAndAnExistingOperator()
    {
        var operators = DemoOperators.All.Select(op => op.Username).Concat(new[] { "operador1", "operador2" }).ToHashSet();

        Assert.All(DemoRoutes.All, route =>
        {
            Assert.Contains(route.Plate, DemoVehicles.All.Select(vehicle => vehicle.Plate));
            Assert.Contains(route.DriverUsername, operators);
        });
    }

    [Fact]
    public void TheCargoOfEveryRoute_FitsInItsVehicle()
    {
        Assert.All(DemoRoutes.All, route =>
        {
            var vehicle = VehicleOf(route.Plate);
            Assert.True(route.TotalKg <= vehicle.CapacityKg + Tolerance, $"{route.Name}: {route.TotalKg} kg > {vehicle.CapacityKg} kg");
            Assert.True(route.TotalM3 <= vehicle.CapacityM3 + Tolerance, $"{route.Name}: {route.TotalM3} m³ > {vehicle.CapacityM3} m³");
        });
    }

    [Fact]
    public void TheCargoPrioritiesAreVaried()
        => Assert.Equal(4, DemoRoutes.All.SelectMany(route => route.Cargo).Select(item => item.Priority).Distinct().Count());

    // ---------- Reglas de viajes activos ----------

    [Fact]
    public void ActiveTrips_NeverShareAVehicleOrADriver()
    {
        var active = DemoRoutes.All.Where(IsActive).ToList();

        Assert.Equal(active.Count, active.Select(route => route.Plate).Distinct().Count());
        Assert.Equal(active.Count, active.Select(route => route.DriverUsername).Distinct().Count());
    }

    [Fact]
    public void ActiveTrips_UseVehiclesEnRouteDrivenByTheSameOperator()
    {
        Assert.All(DemoRoutes.All.Where(IsActive), route =>
        {
            var vehicle = VehicleOf(route.Plate);
            Assert.Equal(VehicleStatus.EnRoute, vehicle.Status);
            Assert.Equal(route.DriverUsername, vehicle.DriverUsername);
        });
    }

    [Fact]
    public void EveryVehicleEnRoute_HasAnActiveTrip()
    {
        var activePlates = DemoRoutes.All.Where(IsActive).Select(route => route.Plate).ToHashSet();

        var enRoute = DemoVehicles.All.Where(vehicle => vehicle.Status == VehicleStatus.EnRoute).Select(vehicle => vehicle.Plate);

        Assert.All(enRoute, plate => Assert.Contains(plate, activePlates));
    }

    [Fact]
    public void PlannedAndCompletedRoutes_UseAvailableVehicles()
    {
        var notActive = DemoRoutes.All.Where(route => !IsActive(route));

        Assert.All(notActive, route => Assert.Equal(VehicleStatus.Available, VehicleOf(route.Plate).Status));
    }

    [Fact]
    public void DelayedRoutes_HaveDelayMinutes_AndTheOthersDoNot()
    {
        Assert.All(DemoRoutes.All, route =>
        {
            if (route.Status == DeliveryRouteStatus.Delayed) Assert.True(route.DelayMinutes > 0, route.Name);
            else Assert.Equal(0, route.DelayMinutes);
        });
    }

    [Fact]
    public void PlannedRoutesAreInTheFuture_AndCompletedOnesInThePast()
    {
        Assert.All(DemoRoutes.All.Where(route => route.Status == DeliveryRouteStatus.Planned), route => Assert.True(route.DaysFromToday > 0, route.Name));
        Assert.All(DemoRoutes.All.Where(route => route.Status == DeliveryRouteStatus.Completed), route => Assert.True(route.DaysFromToday < 0, route.Name));
    }

    // ---------- Mantenimiento ----------

    [Fact]
    public void MaintenanceHistory_ReferencesExistingVehicles_WithOneRecordPerVehicleAndType()
    {
        Assert.All(DemoMaintenanceHistory.All, record => Assert.Contains(record.Plate, DemoVehicles.All.Select(vehicle => vehicle.Plate)));
        Assert.Equal(DemoMaintenanceHistory.All.Count, DemoMaintenanceHistory.All.Select(record => (record.Plate, record.Type)).Distinct().Count());
    }

    [Fact]
    public void OnlyTwoVehiclesHaveOverdueMaintenance_AndNoneOfThemIsOnATrip()
    {
        var overdue = DemoMaintenanceHistory.All.Where(record => record.DueInDays <= 0 || record.KmUntilDue <= 0).Select(record => record.Plate).Order().ToList();
        var onATrip = DemoRoutes.All.Where(route => route.Status != DeliveryRouteStatus.Completed).Select(route => route.Plate).ToHashSet();

        Assert.Equal(new[] { "CAR-028", "CAR-029" }, overdue);
        Assert.All(overdue, plate => Assert.DoesNotContain(plate, onATrip));
    }

    [Fact]
    public void TheVehicleInTheWorkshop_IsTheOneWithOverdueMaintenance()
    {
        Assert.Equal(VehicleStatus.Maintenance, VehicleOf("CAR-028").Status);
        Assert.Equal(VehicleStatus.Available, VehicleOf("CAR-029").Status);
        Assert.Equal(VehicleStatus.OutOfService, VehicleOf("MOT-035").Status);
    }
}
