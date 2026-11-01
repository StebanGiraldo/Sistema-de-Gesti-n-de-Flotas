using FleetManagement.Application.Builders;
using FleetManagement.Application.Services;
using FleetManagement.Application.Tests.Decorator;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Persistence;
using Xunit;

namespace FleetManagement.Application.Tests.Assignment;

/// <summary>
/// Terminar o cancelar una ruta solo libera al vehículo si no sigue ocupado en OTRA ruta activa. Se descubrió al hacer que
/// deshacer una ruta creada la cancele: cancelar una planificada con un vehículo que va en camino con otra lo dejaba
/// "disponible" estando en la carretera.
/// </summary>
public class DeliveryRouteServiceVehicleReleaseTests
{
    private readonly InMemoryVehicleRepository _vehicles = new();
    private readonly InMemoryDeliveryRouteRepository _routes = new();
    private readonly InMemoryDriverRepository _drivers = new();

    private DeliveryRouteService Service()
        => new(_routes, _vehicles, _drivers, new DeliveryRouteBuilder(), new DeliveryRouteDirector(), new RecordingAuditLogger());

    private async Task<Vehicle> EnRouteVanAsync()
        => await _vehicles.AddAsync(new Vehicle { LicensePlate = "VAN-001", Type = VehicleType.Van, Status = VehicleStatus.EnRoute });

    private async Task<DeliveryRoute> RouteAsync(string name, DeliveryRouteStatus status, Vehicle vehicle)
        => await _routes.AddAsync(new DeliveryRoute { Name = name, Status = status, AssignedVehicleId = vehicle.Id });

    private async Task<VehicleStatus> StatusOfAsync(Vehicle vehicle) => (await _vehicles.GetByIdAsync(vehicle.Id))!.Status;

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Completed")]
    public async Task EndingARoute_KeepsTheVehicleEnRoute_WhileAnotherActiveRouteUsesIt(string newStatus)
    {
        var van = await EnRouteVanAsync();
        var onTheRoad = await RouteAsync("Va en camino", DeliveryRouteStatus.InProgress, van);
        var waiting = await RouteAsync("Espera", DeliveryRouteStatus.Planned, van);

        await Service().UpdateRouteStatusAsync(waiting.Id, newStatus);

        Assert.Equal(VehicleStatus.EnRoute, await StatusOfAsync(van));
        Assert.Equal(DeliveryRouteStatus.InProgress, (await _routes.GetByIdAsync(onTheRoad.Id))!.Status);
    }

    [Fact]
    public async Task ADelayedRoute_AlsoCountsAsAnActiveRoute()
    {
        var van = await EnRouteVanAsync();
        await RouteAsync("Retrasada", DeliveryRouteStatus.Delayed, van);
        var waiting = await RouteAsync("Espera", DeliveryRouteStatus.Planned, van);

        await Service().UpdateRouteStatusAsync(waiting.Id, "Cancelled");

        Assert.Equal(VehicleStatus.EnRoute, await StatusOfAsync(van));
    }

    [Fact]
    public async Task EndingTheOnlyActiveRoute_StillReleasesTheVehicle()
    {
        var van = await EnRouteVanAsync();
        var onTheRoad = await RouteAsync("Va en camino", DeliveryRouteStatus.InProgress, van);

        await Service().UpdateRouteStatusAsync(onTheRoad.Id, "Completed");

        Assert.Equal(VehicleStatus.Available, await StatusOfAsync(van));
    }

    [Fact]
    public async Task OtherRoutesThatAreNotActive_DoNotKeepTheVehicleBusy()
    {
        var van = await EnRouteVanAsync();
        await RouteAsync("Ya terminó", DeliveryRouteStatus.Completed, van);
        await RouteAsync("Se canceló", DeliveryRouteStatus.Cancelled, van);
        await RouteAsync("Pendiente", DeliveryRouteStatus.Planned, van);
        var onTheRoad = await RouteAsync("Va en camino", DeliveryRouteStatus.InProgress, van);

        await Service().UpdateRouteStatusAsync(onTheRoad.Id, "Completed");

        Assert.Equal(VehicleStatus.Available, await StatusOfAsync(van));
    }

    [Fact]
    public async Task ARouteOfAnotherVehicle_DoesNotKeepThisOneBusy()
    {
        var van = await EnRouteVanAsync();
        var other = await _vehicles.AddAsync(new Vehicle { LicensePlate = "VAN-002", Type = VehicleType.Van, Status = VehicleStatus.EnRoute });
        await RouteAsync("De la otra", DeliveryRouteStatus.InProgress, other);
        var onTheRoad = await RouteAsync("Va en camino", DeliveryRouteStatus.InProgress, van);

        await Service().UpdateRouteStatusAsync(onTheRoad.Id, "Completed");

        Assert.Equal(VehicleStatus.Available, await StatusOfAsync(van));
        Assert.Equal(VehicleStatus.EnRoute, await StatusOfAsync(other));
    }
}
