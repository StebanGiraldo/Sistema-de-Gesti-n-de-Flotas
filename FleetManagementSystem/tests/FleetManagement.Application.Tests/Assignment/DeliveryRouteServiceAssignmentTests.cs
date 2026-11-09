using FleetManagement.Application.Builders;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Factories;
using FleetManagement.Application.Flyweights;
using FleetManagement.Application.Geo;
using FleetManagement.Application.Services;
using FleetManagement.Application.Tests.Decorator;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Persistence;
using Xunit;

namespace FleetManagement.Application.Tests.Assignment;

/// <summary>
/// Los dos ajustes que recibió el servicio de rutas real: terminar o cancelar un viaje ya no pisa el estado de un vehículo
/// en mantenimiento, y el tiempo estimado usa la velocidad del tipo de vehículo asignado (la misma de la simulación).
/// </summary>
public class DeliveryRouteServiceAssignmentTests
{
    private readonly InMemoryVehicleRepository _vehicles = new();
    private readonly InMemoryDeliveryRouteRepository _routes = new();
    private readonly InMemoryDriverRepository _drivers = new();
    private readonly IVehicleTypeProfileFactory _profiles = new VehicleTypeProfileFactory(new FleetOnboardingFactoryProvider());

    private DeliveryRouteService ServiceWith(IVehicleTypeProfileFactory? profiles)
        => new(_routes, _vehicles, _drivers, new DeliveryRouteBuilder(), new DeliveryRouteDirector(), new RecordingAuditLogger(), FleetMediators.CreateRouteColleague(_routes, _vehicles), profiles);

    private async Task<(DeliveryRoute Route, Vehicle Vehicle)> RouteWithVehicleAsync(VehicleStatus vehicleStatus, DeliveryRouteStatus routeStatus)
    {
        var vehicle = await _vehicles.AddAsync(new Vehicle { LicensePlate = "VAN-001", Type = VehicleType.Van, Status = vehicleStatus });
        var route = await _routes.AddAsync(new DeliveryRoute { Name = "Ruta A", Status = routeStatus, AssignedVehicleId = vehicle.Id });
        return (route, vehicle);
    }

    // ------------------------------------------------------------------ liberar al vehículo

    [Theory]
    [InlineData("Completed", VehicleStatus.EnRoute, VehicleStatus.Available)]
    [InlineData("Cancelled", VehicleStatus.EnRoute, VehicleStatus.Available)]
    [InlineData("Completed", VehicleStatus.Maintenance, VehicleStatus.Maintenance)]
    [InlineData("Cancelled", VehicleStatus.Maintenance, VehicleStatus.Maintenance)]
    [InlineData("Completed", VehicleStatus.OutOfService, VehicleStatus.OutOfService)]
    [InlineData("Cancelled", VehicleStatus.OutOfService, VehicleStatus.OutOfService)]
    [InlineData("Completed", VehicleStatus.Available, VehicleStatus.Available)]
    public async Task FinishingOrCancelling_OnlyReleasesAVehicleThatWasEnRoute(string newStatus, VehicleStatus before, VehicleStatus expected)
    {
        var (route, vehicle) = await RouteWithVehicleAsync(before, DeliveryRouteStatus.InProgress);

        await ServiceWith(null).UpdateRouteStatusAsync(route.Id, newStatus);

        Assert.Equal(expected, (await _vehicles.GetByIdAsync(vehicle.Id))!.Status);
    }

    [Theory]
    [InlineData("InProgress")]
    [InlineData("Delayed")]
    public async Task StartingATrip_StillMarksTheVehicleEnRoute(string newStatus)
    {
        var (route, vehicle) = await RouteWithVehicleAsync(VehicleStatus.Available, DeliveryRouteStatus.Planned);

        await ServiceWith(null).UpdateRouteStatusAsync(route.Id, newStatus);

        Assert.Equal(VehicleStatus.EnRoute, (await _vehicles.GetByIdAsync(vehicle.Id))!.Status);
    }

    // ------------------------------------------------------------------ velocidad del estimado

    private static CreateDeliveryRouteRequest LongRoute(Guid? vehicleId)
        => new("Ruta del ecuador", 0, 0, 0, 0.6, null, null, vehicleId, null, null);

    private static double EquatorKm() => GeoMath.DistanceKm(new GeoCoordinate(0, 0), new GeoCoordinate(0, 0.6));

    [Theory]
    [InlineData(VehicleType.Truck, 80)]
    [InlineData(VehicleType.Van, 100)]
    [InlineData(VehicleType.Car, 120)]
    [InlineData(VehicleType.Motorcycle, 80)]
    public async Task CreateRoute_EstimatesTheTimeWithTheSpeedOfTheAssignedVehicleType(VehicleType type, double speedKmh)
    {
        var vehicle = await _vehicles.AddAsync(new Vehicle { LicensePlate = "XXX-001", Type = type });

        var route = await ServiceWith(_profiles).CreateRouteAsync(LongRoute(vehicle.Id));

        Assert.Equal(Math.Round(EquatorKm() / speedKmh * 60, 1), route.EstimatedDurationMinutes);
        Assert.Equal(Math.Round(EquatorKm(), 2), route.EstimatedDistanceKm);
    }

    [Fact]
    public async Task CreateRoute_WithoutVehicle_UsesTheDefaultAverageSpeed()
    {
        var route = await ServiceWith(_profiles).CreateRouteAsync(LongRoute(null));

        Assert.Equal(Math.Round(EquatorKm() / TripEstimate.DefaultSpeedKmh * 60, 1), route.EstimatedDurationMinutes);
    }

    [Fact]
    public async Task CreateRoute_WithAnUnknownVehicle_UsesTheDefaultAverageSpeed()
    {
        var route = await ServiceWith(_profiles).CreateRouteAsync(LongRoute(Guid.NewGuid()));

        Assert.Equal(Math.Round(EquatorKm() / TripEstimate.DefaultSpeedKmh * 60, 1), route.EstimatedDurationMinutes);
    }

    [Fact]
    public async Task CreateRoute_WhenTheServiceIsBuiltWithoutProfiles_KeepsTheOriginalBehavior()
    {
        var vehicle = await _vehicles.AddAsync(new Vehicle { LicensePlate = "CAR-001", Type = VehicleType.Car });

        var route = await ServiceWith(null).CreateRouteAsync(LongRoute(vehicle.Id));

        Assert.Equal(Math.Round(EquatorKm() / TripEstimate.DefaultSpeedKmh * 60, 1), route.EstimatedDurationMinutes);
    }

    [Fact]
    public async Task CreateExpressRoute_AlsoUsesTheSpeedOfTheAssignedVehicleType()
    {
        var vehicle = await _vehicles.AddAsync(new Vehicle { LicensePlate = "TRK-001", Type = VehicleType.Truck });
        var driver = await _drivers.AddAsync(new Driver { FullName = "Ana" });

        var route = await ServiceWith(_profiles).CreateExpressRouteAsync(new CreateExpressRouteRequest("Express", 0, 0, 0, 0.6, vehicle.Id, driver.Id));

        Assert.Equal(Math.Round(EquatorKm() / 80 * 60, 1), route.EstimatedDurationMinutes);
    }
}
