using FleetManagement.Application.Mediators;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Mediator;

/// <summary>
/// El participante de vehículos tiene que GUARDAR el estado que decide, no solo cambiarlo en el objeto que leyó. Con el repositorio
/// en memoria del proyecto (que entrega siempre la misma instancia) un guardado olvidado no se nota; por eso estas pruebas usan un
/// repositorio que, como una base de datos real, entrega copias: si el participante no llama a UpdateAsync, el cambio se pierde
/// y la prueba falla.
/// </summary>
public class VehicleColleaguePersistenceTests
{
    private readonly CopyingVehicleRepository _vehicles = new();

    private VehicleColleague Colleague() => new(_vehicles);

    private async Task<Vehicle> VehicleAsync(VehicleStatus status)
        => await _vehicles.AddAsync(new Vehicle { LicensePlate = "VAN-001", Type = VehicleType.Van, Status = status });

    private async Task<VehicleStatus> StoredStatusAsync(Vehicle vehicle) => (await _vehicles.GetByIdAsync(vehicle.Id))!.Status;

    [Fact]
    public async Task MarkEnRoute_LeavesTheVehicleEnRoute_InTheStore()
    {
        var vehicle = await VehicleAsync(VehicleStatus.Available);

        await Colleague().MarkEnRouteAsync(vehicle.Id);

        Assert.Equal(VehicleStatus.EnRoute, await StoredStatusAsync(vehicle));
    }

    [Fact]
    public async Task ReleaseAfterTrip_LeavesTheVehicleAvailable_InTheStore_WhenNothingElseUsesIt()
    {
        var vehicle = await VehicleAsync(VehicleStatus.EnRoute);

        await Colleague().ReleaseAfterTripAsync(vehicle.Id, hasOtherActiveTrip: false);

        Assert.Equal(VehicleStatus.Available, await StoredStatusAsync(vehicle));
    }

    [Theory]
    [InlineData(VehicleStatus.EnRoute, true, VehicleStatus.EnRoute)]
    [InlineData(VehicleStatus.Maintenance, false, VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService, false, VehicleStatus.OutOfService)]
    [InlineData(VehicleStatus.Available, false, VehicleStatus.Available)]
    public async Task ReleaseAfterTrip_KeepsTheStoredStatus_WhenTheVehicleIsNotToBeReleased(VehicleStatus current, bool hasOtherActiveTrip, VehicleStatus expected)
    {
        var vehicle = await VehicleAsync(current);

        await Colleague().ReleaseAfterTripAsync(vehicle.Id, hasOtherActiveTrip);

        Assert.Equal(expected, await StoredStatusAsync(vehicle));
    }
}
