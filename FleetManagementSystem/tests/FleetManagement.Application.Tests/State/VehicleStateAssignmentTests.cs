using FleetManagement.Application.Assignment;
using FleetManagement.Application.Tests.Assignment;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Enums;
using FleetManagement.Domain.States;
using Xunit;

namespace FleetManagement.Application.Tests.State;

/// <summary>
/// STATE dentro del validador de asignación: "¿puede este vehículo usarse en una ruta?" ya no se resuelve comparando el estado con
/// el enum sino preguntándole al estado, y el motivo del rechazo es el mensaje que da cada estado. Los textos son EXACTAMENTE los de
/// antes (las pruebas del validador original siguen pasando); aquí se fija la matriz completa y que de verdad se pregunta al estado.
/// </summary>
public class VehicleStateAssignmentTests
{
    private const string MaintenanceMessage = "El vehículo VAN-001 está en mantenimiento; no se puede usar en una ruta.";
    private const string OutOfServiceMessage = "El vehículo VAN-001 está fuera de servicio; no se puede usar en una ruta.";

    private static RouteAssignmentValidator ValidatorWith(AssignmentKit kit, IVehicleStateResolver states)
        => new(kit.Vehicles, kit.Drivers, kit.Routes, kit.Maintenance, states);

    // ---------------- Ruta nueva ----------------

    [Theory]
    [InlineData(VehicleStatus.Available, null)]
    [InlineData(VehicleStatus.EnRoute, null)]
    [InlineData(VehicleStatus.Maintenance, MaintenanceMessage)]
    [InlineData(VehicleStatus.OutOfService, OutOfServiceMessage)]
    public async Task NewRoute_IsAcceptedOrRejected_AccordingToWhatTheStateOfTheVehicleAllows(VehicleStatus status, string? rejection)
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync("VAN-001", status);

        if (rejection is null)
        {
            await kit.Validator.ValidateNewRouteAsync(vehicle.Id, null, new[] { AssignmentKit.Cargo(10) });
            return;
        }

        var error = await kit.RejectedNewRouteAsync(vehicle.Id, null, AssignmentKit.Cargo(10));
        Assert.Equal(new[] { rejection }, error.Violations);
    }

    [Fact]
    public async Task TheReasonOfTheState_IsReportedTogetherWithTheOtherViolations_StateFirst()
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync("VAN-001", VehicleStatus.Maintenance, capacityKg: 100);

        var error = await kit.RejectedNewRouteAsync(vehicle.Id, null, AssignmentKit.Cargo(500));

        Assert.Equal(2, error.Violations.Count);
        Assert.Equal(MaintenanceMessage, error.Violations[0]);
        Assert.Contains("supera la capacidad", error.Violations[1]);
    }

    // ---------------- Iniciar una ruta ----------------

    [Theory]
    [InlineData(VehicleStatus.Available, null)]
    [InlineData(VehicleStatus.EnRoute, null)]
    [InlineData(VehicleStatus.Maintenance, MaintenanceMessage)]
    [InlineData(VehicleStatus.OutOfService, OutOfServiceMessage)]
    public async Task StartingARoute_IsAcceptedOrRejected_AccordingToWhatTheStateOfTheVehicleAllows(VehicleStatus status, string? rejection)
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync("VAN-001", status);
        var route = await kit.AddRouteAsync("Ruta", DeliveryRouteStatus.Planned, vehicle);

        if (rejection is null)
        {
            await kit.Validator.ValidateStartAsync(route.Id);
            return;
        }

        var error = await kit.RejectedStartAsync(route.Id);
        Assert.Equal(new[] { rejection }, error.Violations);
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.InProgress)]
    [InlineData(DeliveryRouteStatus.Delayed)]
    public async Task RepeatingARouteThatIsAlreadyActive_IsStillHarmless_WhateverTheStateOfItsVehicle(DeliveryRouteStatus active)
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync("VAN-001", VehicleStatus.Maintenance);
        var route = await kit.AddRouteAsync("Ruta", active, vehicle);

        await kit.Validator.ValidateStartAsync(route.Id);   // no hay nada nuevo que validar: no debe lanzar
    }

    // ---------------- El validador le pregunta al estado ----------------

    [Fact]
    public async Task TheValidator_AsksTheStateWhetherTheVehicleCanOperate_AndUsesItsReason()
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync("VAN-001", VehicleStatus.Available);   // el enum diría que sí puede...
        var blocking = new ConfigurableState(VehicleStatus.Available, canOperate: false, canTrackTrip: false, blockReason: "Bloqueado por el estado de prueba");

        var validator = ValidatorWith(kit, new ConfigurableResolver().Override(blocking));   // ...pero el estado dice que no

        var error = await Assert.ThrowsAsync<AssignmentRuleException>(() => validator.ValidateNewRouteAsync(vehicle.Id, null, new[] { AssignmentKit.Cargo(10) }));
        Assert.Equal(new[] { "Bloqueado por el estado de prueba (VAN-001)" }, error.Violations);
        Assert.Equal(new[] { "VAN-001" }, blocking.BlockReasonAskedFor);
    }

    [Fact]
    public async Task IfTheStateAllowsIt_AVehicleThatWasBlocked_IsAcceptedWithoutChangingTheValidator()
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync("VAN-001", VehicleStatus.Maintenance);
        var permissive = new ConfigurableState(VehicleStatus.Maintenance, canOperate: true, canTrackTrip: false);

        await ValidatorWith(kit, new ConfigurableResolver().Override(permissive)).ValidateNewRouteAsync(vehicle.Id, null, new[] { AssignmentKit.Cargo(10) });
    }

    [Fact]
    public async Task TheValidator_AsksTheStateOnlyForTheVehicleItIsValidating()
    {
        var kit = new AssignmentKit();
        var assigned = await kit.AddVehicleAsync("VAN-001", VehicleStatus.Available);
        await kit.AddVehicleAsync("VAN-002", VehicleStatus.Maintenance);   // otro vehículo de la flota: no debe preguntarse por él
        var spy = new ConfigurableState(VehicleStatus.Available, canOperate: true, canTrackTrip: false);

        await ValidatorWith(kit, new ConfigurableResolver().Override(spy)).ValidateNewRouteAsync(assigned.Id, null, new[] { AssignmentKit.Cargo(10) });

        Assert.Equal(new[] { "VAN-001" }, spy.BlockReasonAskedFor);
    }

    [Fact]
    public async Task WithoutAResolver_TheValidatorUsesTheRealStates()
    {
        var kit = new AssignmentKit();   // el kit construye el validador con los cuatro parámetros de siempre
        var vehicle = await kit.AddVehicleAsync("VAN-001", VehicleStatus.OutOfService);

        var error = await kit.RejectedNewRouteAsync(vehicle.Id, null);

        Assert.Equal(new[] { OutOfServiceMessage }, error.Violations);
    }
}
