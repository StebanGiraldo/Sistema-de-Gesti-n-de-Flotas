using FleetManagement.Application.Metrics;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Metrics;

/// <summary>
/// Pruebas de los cálculos puros que comparten el reporte (Decorator) y el resumen del dashboard (Facade).
/// Al no depender de servicios ni de datos, se prueban directamente con DTO armados a mano.
/// </summary>
public class FleetMetricsTests
{
    [Fact]
    public void Statistics_CountsVehiclesAndRoutesByStatus_ListingEveryStatusEvenWithZero()
    {
        var vehicles = new[]
        {
            TestData.Vehicle("A-1", "Available"), TestData.Vehicle("A-2", "Available"),
            TestData.Vehicle("E-1", "EnRoute"), TestData.Vehicle("O-1", "OutOfService")
        };
        var routes = new[] { TestData.Route("R1", "InProgress"), TestData.Route("R2", "Planned"), TestData.Route("R3", "Planned") };

        var stats = FleetMetrics.Statistics(vehicles, routes);

        Assert.Equal(4, stats.TotalVehicles);
        Assert.Equal(new[] { "Available", "EnRoute", "Maintenance", "OutOfService" }, stats.VehiclesByStatus.Keys.ToArray());
        Assert.Equal(new[] { 2, 1, 0, 1 }, stats.VehiclesByStatus.Values.ToArray());
        Assert.Equal(3, stats.TotalRoutes);
        Assert.Equal(new[] { "Planned", "InProgress", "Delayed", "Completed", "Cancelled" }, stats.RoutesByStatus.Keys.ToArray());
        Assert.Equal(new[] { 2, 1, 0, 0, 0 }, stats.RoutesByStatus.Values.ToArray());
    }

    [Fact]
    public void Statistics_AvailabilityPercent_IsAvailableOverTotal_RoundedToOneDecimal()
    {
        var vehicles = new[] { TestData.Vehicle("A-1", "Available"), TestData.Vehicle("E-1", "EnRoute"), TestData.Vehicle("M-1", "Maintenance") };

        var stats = FleetMetrics.Statistics(vehicles, Array.Empty<Application.DTOs.DeliveryRouteDto>());

        Assert.Equal(33.3, stats.AvailabilityPercent);
    }

    [Fact]
    public void Statistics_AverageMileageAndTotalCapacity_SumOverTheVehicles()
    {
        var vehicles = new[]
        {
            TestData.Vehicle("A-1", mileageKm: 1000, capacityKg: 8000),
            TestData.Vehicle("A-2", mileageKm: 2000.5, capacityKg: 1500),
            TestData.Vehicle("A-3", mileageKm: 0, capacityKg: 30.25)
        };

        var stats = FleetMetrics.Statistics(vehicles, Array.Empty<Application.DTOs.DeliveryRouteDto>());

        Assert.Equal(1000.167, stats.AverageMileageKm);
        Assert.Equal(9530.25, stats.TotalCapacityKg);
    }

    [Fact]
    public void Statistics_TotalDelayMinutes_SumsTheDelayOfAllRoutes()
    {
        var routes = new[] { TestData.Route("R1", "Delayed", delayMinutes: 25), TestData.Route("R2", "Delayed", delayMinutes: 10), TestData.Route("R3") };

        var stats = FleetMetrics.Statistics(Array.Empty<Application.DTOs.VehicleDto>(), routes);

        Assert.Equal(35, stats.TotalDelayMinutes);
    }

    [Fact]
    public void Statistics_ForAnEmptyFleet_ReturnsZeros_WithoutDividingByZero()
    {
        var stats = FleetMetrics.Statistics(Array.Empty<Application.DTOs.VehicleDto>(), Array.Empty<Application.DTOs.DeliveryRouteDto>());

        Assert.Equal(0, stats.TotalVehicles);
        Assert.Equal(0.0, stats.AvailabilityPercent);
        Assert.Equal(0.0, stats.AverageMileageKm);
        Assert.Equal(0.0, stats.TotalCapacityKg);
        Assert.Equal(0, stats.TotalRoutes);
        Assert.Equal(0, stats.TotalDelayMinutes);
        Assert.All(stats.VehiclesByStatus.Values, count => Assert.Equal(0, count));
        Assert.All(stats.RoutesByStatus.Values, count => Assert.Equal(0, count));
    }

    [Fact]
    public void CargoTotals_CountsTheItems_AndAddsUpTheirWeightAndVolume()
    {
        var totals = FleetMetrics.CargoTotals(new[]
        {
            TestData.Cargo("A", weightKg: 0.1, volumeM3: 0.1),
            TestData.Cargo("B", weightKg: 0.2, volumeM3: 0.2),
            TestData.Cargo("C", weightKg: 320, volumeM3: 1.8)
        });

        Assert.Equal(3, totals.ItemCount);
        Assert.Equal(320.3, totals.TotalWeightKg);
        Assert.Equal(2.1, totals.TotalVolumeM3);
    }

    [Fact]
    public void CargoTotals_RoundsTheWeightAndTheVolumeToThreeDecimals_EachOnItsOwn()
    {
        // Las sumas exactas (3.2344 kg y 0.00061 m3) tienen más de 3 decimales, así que el resultado debe venir
        // redondeado, igual que en el manifiesto Composite. Con estos valores el redondeo SÍ cambia el resultado.
        var totals = FleetMetrics.CargoTotals(new[]
        {
            TestData.Cargo("A", weightKg: 1.2341, volumeM3: 0.00049),
            TestData.Cargo("B", weightKg: 2.0003, volumeM3: 0.00012)
        });

        Assert.Equal(3.234, totals.TotalWeightKg);
        Assert.Equal(0.001, totals.TotalVolumeM3);
    }

    [Fact]
    public void CargoTotals_WithoutItems_IsZero()
    {
        var totals = FleetMetrics.CargoTotals(Array.Empty<Application.DTOs.CargoItemDto>());

        Assert.Equal(0, totals.ItemCount);
        Assert.Equal(0.0, totals.TotalWeightKg);
        Assert.Equal(0.0, totals.TotalVolumeM3);
    }

    [Fact]
    public void OverdueMaintenance_CountsOnlyTheItemsMarkedAsOverdue()
    {
        var due = new[] { TestData.Due("A-1"), TestData.Due("A-2", isOverdue: false), TestData.Due("A-3") };

        Assert.Equal(2, FleetMetrics.OverdueMaintenance(due));
        Assert.Equal(0, FleetMetrics.OverdueMaintenance(Array.Empty<Application.DTOs.MaintenanceDueDto>()));
    }

    [Fact]
    public void CountByStatus_ReturnsEveryEnumNameInDeclarationOrder_WithZeroWhereThereIsNone()
    {
        var counts = FleetMetrics.CountByStatus<AlertStatus>(new[] { "Open", "Resolved", "Open" });

        Assert.Equal(new[] { "Open", "Acknowledged", "Resolved" }, counts.Keys.ToArray());
        Assert.Equal(new[] { 2, 0, 1 }, counts.Values.ToArray());
    }

    [Fact]
    public void Metrics_RejectNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => FleetMetrics.Statistics(null!, Array.Empty<Application.DTOs.DeliveryRouteDto>()));
        Assert.Throws<ArgumentNullException>(() => FleetMetrics.Statistics(Array.Empty<Application.DTOs.VehicleDto>(), null!));
        Assert.Throws<ArgumentNullException>(() => FleetMetrics.CargoTotals(null!));
        Assert.Throws<ArgumentNullException>(() => FleetMetrics.OverdueMaintenance(null!));
        Assert.Throws<ArgumentNullException>(() => FleetMetrics.CountByStatus<AlertStatus>(null!));
    }
}
