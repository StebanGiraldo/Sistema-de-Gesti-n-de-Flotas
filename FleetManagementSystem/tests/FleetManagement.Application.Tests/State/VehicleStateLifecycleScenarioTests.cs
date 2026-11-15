using FleetManagement.Application.DTOs;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Domain.States;
using Xunit;

namespace FleetManagement.Application.Tests.State;

/// <summary>
/// Recorrido completo con los SERVICIOS REALES (rutas con su Mediator, vehículos con su State, bitácora y repositorios en memoria):
/// lo que le pasa a un vehículo desde que sale a un viaje hasta que vuelve a la calle después de pasar por el taller. Es la historia
/// que cuenta el patrón: el Mediator decide CUÁNDO reaccionar, el estado decide CÓMO, y una persona no puede saltarse el orden.
/// </summary>
public class VehicleStateLifecycleScenarioTests
{
    private readonly RealFleet _fleet = new();

    private Task SetStatusAsync(Vehicle vehicle, string status)
        => _fleet.Vehicles.UpdateVehicleStatusAsync(vehicle.Id, new UpdateVehicleStatusRequest(status));

    private IEnumerable<string> VehicleAudit() => _fleet.Audit.Entries.Where(entry => entry.Category == "Vehículo").Select(entry => entry.Message);

    [Fact]
    public async Task AVehicleThatGoesToTheWorkshopMidTrip_StaysThereWhenTheTripEnds_AndComesBackThroughAvailable()
    {
        var truck = (await _fleet.SeedAsync())["TRK-001"];                      // Disponible
        var route = await _fleet.RouteRepository.AddAsync(new DeliveryRoute { Name = "Entrega urgente", Status = DeliveryRouteStatus.Planned, AssignedVehicleId = truck.Id });

        await _fleet.Routes.UpdateRouteStatusAsync(route.Id, "InProgress");     // Mediator → estado: el viaje empieza
        Assert.Equal(VehicleStatus.EnRoute, truck.Status);

        await SetStatusAsync(truck, "Maintenance");                              // una persona lo manda al taller (En ruta → Mantenimiento: permitido)
        await _fleet.Routes.UpdateRouteStatusAsync(route.Id, "Completed");       // el viaje termina
        Assert.Equal(VehicleStatus.Maintenance, truck.Status);                   // regla 3 del Mediator: el estado no libera lo que no estaba En ruta

        var rejected = await Assert.ThrowsAsync<InvalidVehicleTransitionException>(() => SetStatusAsync(truck, "EnRoute"));   // no se puede saltar el taller
        Assert.Equal(VehicleStatus.Maintenance, rejected.From);
        Assert.Equal(VehicleStatus.EnRoute, rejected.To);
        Assert.Equal(VehicleStatus.Maintenance, truck.Status);

        await SetStatusAsync(truck, "Available");                                // sale del taller
        await SetStatusAsync(truck, "EnRoute");                                  // y recién entonces puede volver a ruta
        Assert.Equal(VehicleStatus.EnRoute, truck.Status);

        Assert.Equal(
            new[]
            {
                "Vehículo TRK-001 cambió de estado: EnRoute -> Maintenance.",
                "Vehículo TRK-001 cambió de estado: Maintenance -> Available.",
                "Vehículo TRK-001 cambió de estado: Available -> EnRoute."
            },
            VehicleAudit());                                                     // el cambio rechazado no dejó ningún rastro
    }

    [Fact]
    public async Task ATripThatEndsNormally_ReleasesTheVehicle_ThroughTheStateAndNotThroughAManualChange()
    {
        var truck = (await _fleet.SeedAsync())["TRK-001"];
        var route = await _fleet.RouteRepository.AddAsync(new DeliveryRoute { Name = "Entrega", Status = DeliveryRouteStatus.Planned, AssignedVehicleId = truck.Id });

        await _fleet.Routes.UpdateRouteStatusAsync(route.Id, "InProgress");
        await _fleet.Routes.UpdateRouteStatusAsync(route.Id, "Completed");

        Assert.Equal(VehicleStatus.Available, truck.Status);
        Assert.Empty(VehicleAudit());                                            // lo hizo el Mediator con el estado: no pasó por UpdateVehicleStatusAsync
    }

    [Fact]
    public async Task APlannedRoute_NeverTouchesItsVehicle_WhateverItsState()
    {
        var vehicles = await _fleet.SeedAsync();
        foreach (var vehicle in vehicles.Values)
            await _fleet.RouteRepository.AddAsync(new DeliveryRoute { Name = $"Planificada {vehicle.LicensePlate}", Status = DeliveryRouteStatus.Planned, AssignedVehicleId = vehicle.Id });
        var before = vehicles.Values.ToDictionary(vehicle => vehicle.Id, vehicle => vehicle.Status);

        foreach (var route in await _fleet.Routes.GetAllRoutesAsync())
            await _fleet.Routes.UpdateRouteStatusAsync(route.Id, "Planned");

        Assert.All(vehicles.Values, vehicle => Assert.Equal(before[vehicle.Id], vehicle.Status));
    }

    [Fact]
    public async Task ANotificationIsSentOnlyForAnAcceptedChange()
    {
        var truck = (await _fleet.SeedAsync())["TRK-001"];
        var sentBefore = _fleet.Channel.SentMessages.Count;

        await SetStatusAsync(truck, "Maintenance");
        Assert.Equal(sentBefore + 1, _fleet.Channel.SentMessages.Count);

        await Assert.ThrowsAsync<InvalidVehicleTransitionException>(() => SetStatusAsync(truck, "EnRoute"));
        Assert.Equal(sentBefore + 1, _fleet.Channel.SentMessages.Count);        // el rechazado no avisa a nadie
    }
}
