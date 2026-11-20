using FleetManagement.Application.DTOs;
using FleetManagement.Application.Services;
using FleetManagement.Application.Tests.Bridge;
using FleetManagement.Application.Tests.Decorator;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Domain.States;
using FleetManagement.Infrastructure.Persistence;
using Xunit;

namespace FleetManagement.Application.Tests.State;

/// <summary>
/// STATE en el REGISTRO DE UN MANTENIMIENTO: la regla de siempre —un vehículo que estaba en el taller sale de él al registrarse su
/// mantenimiento— la aplica ahora el estado del vehículo (<see cref="VehicleStateContext.CompleteMaintenance"/>) y no un
/// <c>if</c> escrito en <see cref="MaintenanceService"/>. Aquí se comprueba (1) lo que hace cada estado, (2) que el servicio
/// REAL sigue haciendo exactamente lo de antes con cada estado inicial, y (3) que consulta al estado en vez de decidir por su cuenta.
/// </summary>
public class VehicleStateMaintenanceRegistrationTests
{
    private const string Plate = "TST-001";
    private static readonly DateTime PerformedAt = new(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly CopyingVehicleRepository _vehicles = new();
    private readonly InMemoryMaintenanceRepository _records = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly RecordingNotificationChannel _channel = new();

    private MaintenanceService Service(IVehicleStateResolver? states = null)
        => new(_records, _vehicles, new InMemoryDriverRepository(), _audit, _channel, states);

    private async Task<Vehicle> VehicleAsync(VehicleStatus status)
        => await _vehicles.AddAsync(new Vehicle { LicensePlate = Plate, Brand = "Renault", Model = "Kangoo", Year = 2022, Type = VehicleType.Van, Status = status });

    private static CreateMaintenanceRecordRequest Request(Guid vehicleId, string type = "OilChange")
        => new(vehicleId, type, PerformedAt, null, 60000, "Cambio de aceite", 50000);

    private async Task<Vehicle> StoredAsync(Vehicle vehicle) => (await _vehicles.GetByIdAsync(vehicle.Id))!;

    // ---------------- Lo que hace cada estado ----------------

    [Theory]
    [InlineData(VehicleStatus.Available,    VehicleStatus.Available)]      // ya estaba libre: nada que liberar
    [InlineData(VehicleStatus.EnRoute,      VehicleStatus.EnRoute)]        // un mantenimiento no lo saca de su viaje
    [InlineData(VehicleStatus.Maintenance,  VehicleStatus.Available)]      // la única que cambia: sale del taller
    [InlineData(VehicleStatus.OutOfService, VehicleStatus.OutOfService)]   // registrar un mantenimiento no lo devuelve al servicio
    public void EachState_DecidesWhatARegisteredMaintenanceDoesToTheVehicle(VehicleStatus from, VehicleStatus expected)
    {
        var vehicle = new Vehicle { LicensePlate = Plate, Status = from };

        new VehicleStateContext(vehicle).CompleteMaintenance();

        Assert.Equal(expected, vehicle.Status);
    }

    [Theory]
    [InlineData(VehicleStatus.Available)]
    [InlineData(VehicleStatus.EnRoute)]
    [InlineData(VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService)]
    public void EachState_RejectsANullContext(VehicleStatus status)
    {
        var state = VehicleStateResolver.Default.For(status);

        Assert.Throws<ArgumentNullException>(() => state.CompleteMaintenance(null!));
    }

    [Fact]
    public void OnlyTheWorkshopStateOverridesIt_TheOthersInheritTheDoNothingDefault()
    {
        var overriding = new[] { typeof(AvailableState), typeof(EnRouteState), typeof(MaintenanceState), typeof(OutOfServiceState) }
            .Where(type => type.GetMethod(nameof(IVehicleStateBehavior.CompleteMaintenance))!.DeclaringType == type)
            .ToList();

        Assert.Equal(new[] { typeof(MaintenanceState) }, overriding);
    }

    [Fact]
    public void ReleasingFromTheWorkshop_IsOnlyPossibleByRegisteringTheMaintenance_NotByHand()
    {
        // Un vehículo en el taller no pasa a Disponible a mano: solo sale de él cuando se registra su mantenimiento.
        var byHand = new Vehicle { LicensePlate = Plate, Status = VehicleStatus.Maintenance };
        var byRecord = new Vehicle { LicensePlate = Plate, Status = VehicleStatus.Maintenance };

        Assert.Throws<InvalidVehicleTransitionException>(() => new VehicleStateContext(byHand).ChangeTo(VehicleStatus.Available));
        new VehicleStateContext(byRecord).CompleteMaintenance();

        Assert.Equal(VehicleStatus.Maintenance, byHand.Status);
        Assert.Equal(VehicleStatus.Available, byRecord.Status);
    }

    // ---------------- El servicio real hace lo mismo que antes con cada estado inicial ----------------

    [Theory]
    [InlineData(VehicleStatus.Available,    VehicleStatus.Available)]
    [InlineData(VehicleStatus.EnRoute,      VehicleStatus.EnRoute)]
    [InlineData(VehicleStatus.Maintenance,  VehicleStatus.Available)]
    [InlineData(VehicleStatus.OutOfService, VehicleStatus.OutOfService)]
    public async Task RegisteringAMaintenance_LeavesTheVehicleInTheStateThatItsStateDecides(VehicleStatus from, VehicleStatus expected)
    {
        var vehicle = await VehicleAsync(from);

        await Service().CreateAsync(Request(vehicle.Id));

        Assert.Equal(expected, (await StoredAsync(vehicle)).Status);
    }

    [Theory]
    [InlineData(VehicleStatus.Available)]
    [InlineData(VehicleStatus.EnRoute)]
    [InlineData(VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService)]
    public async Task RegisteringAMaintenance_StillStoresTheRecord_TheDate_TheAuditAndTheNotification_WhateverTheState(VehicleStatus from)
    {
        var vehicle = await VehicleAsync(from);

        var created = await Service().CreateAsync(Request(vehicle.Id));

        Assert.Equal(vehicle.Id, created.VehicleId);
        Assert.Single(await _records.GetByVehicleIdAsync(vehicle.Id));
        Assert.Equal(PerformedAt, (await StoredAsync(vehicle)).LastMaintenanceDate);
        var audited = Assert.Single(_audit.Entries.Where(entry => entry.Category == "Mantenimiento"));
        Assert.Equal($"Registrado mantenimiento 'OilChange' para el vehículo {Plate}.", audited.Message);
        Assert.Single(_channel.SentMessages);
        Assert.DoesNotContain(_audit.Entries, entry => entry.Category == "Vehículo");   // salir del taller así nunca se audita como cambio manual
    }

    [Fact]
    public async Task AnUnknownVehicle_StillThrowsNotFound_BeforeAnyStateIsConsulted()
    {
        var spy = new RecordingResolver();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(spy).CreateAsync(Request(Guid.NewGuid())));

        Assert.Empty(spy.Requested);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task AnInvalidMaintenanceType_StillThrows_AndTheVehicleStaysInTheWorkshop()
    {
        var vehicle = await VehicleAsync(VehicleStatus.Maintenance);
        var spy = new RecordingResolver();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service(spy).CreateAsync(Request(vehicle.Id, "Volando")));

        Assert.Equal("Tipo de mantenimiento inválido: 'Volando'.", error.Message);
        Assert.Equal(VehicleStatus.Maintenance, (await StoredAsync(vehicle)).Status);   // no se libera por una petición que se rechazó
        Assert.Empty(spy.Requested);
        Assert.Empty(await _records.GetAllAsync());
    }

    // ---------------- El servicio le pregunta al estado ----------------

    [Fact]
    public async Task TheService_UsesTheResolverItIsGiven_ToDecideWhatHappensToTheVehicle()
    {
        var vehicle = await VehicleAsync(VehicleStatus.Maintenance);
        var spy = new RecordingResolver();

        await Service(spy).CreateAsync(Request(vehicle.Id));

        Assert.Contains(VehicleStatus.Maintenance, spy.Requested);   // se consultó el estado en que estaba el vehículo
    }

    [Fact]
    public async Task TheService_FollowsTheStateItIsGiven_NotARuleOfItsOwn()
    {
        // Un estado de prueba que NO libera al vehículo del taller: si el servicio siguiera con su propio "if", lo liberaría igual.
        var vehicle = await VehicleAsync(VehicleStatus.Maintenance);
        var stubborn = new ConfigurableState(VehicleStatus.Maintenance, canOperate: false, canTrackTrip: false);

        await Service(new ConfigurableResolver().Override(stubborn)).CreateAsync(Request(vehicle.Id));

        Assert.Equal(1, stubborn.MaintenanceCompletedCount);                              // el servicio le pidió al estado que decidiera
        Assert.Equal(VehicleStatus.Maintenance, (await StoredAsync(vehicle)).Status);     // y el estado decidió no liberarlo
    }

    [Fact]
    public async Task TheService_WorksWithoutAResolver_UsingTheDefaultOne()
    {
        var vehicle = await VehicleAsync(VehicleStatus.Maintenance);

        await Service(states: null).CreateAsync(Request(vehicle.Id));

        Assert.Equal(VehicleStatus.Available, (await StoredAsync(vehicle)).Status);
    }

    [Fact]
    public void TheService_StillHasASinglePublicConstructor()
    {
        Assert.Single(typeof(MaintenanceService).GetConstructors());   // el parámetro nuevo es opcional: no hay un segundo constructor
    }

    // ---------------- Con los servicios reales de la flota ----------------

    [Fact]
    public async Task AVehicleInTheWorkshop_LeavesItWhenItsMaintenanceIsRegistered_AndThenCanBeSentOnARoute()
    {
        var fleet = new RealFleet();
        var car = (await fleet.SeedAsync())["CAR-003"];                          // en Mantenimiento

        await fleet.Maintenance.CreateAsync(Request(car.Id));
        Assert.Equal(VehicleStatus.Available, car.Status);                       // salió del taller por el estado

        await fleet.Vehicles.UpdateVehicleStatusAsync(car.Id, new UpdateVehicleStatusRequest("EnRoute"));   // y ya puede volver a ruta
        Assert.Equal(VehicleStatus.EnRoute, car.Status);
    }

    [Fact]
    public async Task AVehicleInTheWorkshop_CannotBeMadeAvailableByHand_ItIsOnlyFreedByItsRegisteredMaintenance()
    {
        var fleet = new RealFleet();
        var car = (await fleet.SeedAsync())["CAR-003"];                          // en Mantenimiento

        var error = await Assert.ThrowsAsync<InvalidVehicleTransitionException>(
            () => fleet.Vehicles.UpdateVehicleStatusAsync(car.Id, new UpdateVehicleStatusRequest("Available")));
        Assert.Contains("registre el mantenimiento", error.Message);
        Assert.Equal(VehicleStatus.Maintenance, car.Status);                     // a mano no sale del taller

        await fleet.Maintenance.CreateAsync(Request(car.Id));
        Assert.Equal(VehicleStatus.Available, car.Status);                       // al registrar su mantenimiento, sí
    }

    [Fact]
    public async Task AVehicleOutOfService_IsNotReturnedToServiceByARegisteredMaintenance()
    {
        var fleet = new RealFleet();
        var van = (await fleet.SeedAsync())["VAN-005"];                          // Fuera de servicio

        await fleet.Maintenance.CreateAsync(Request(van.Id));

        Assert.Equal(VehicleStatus.OutOfService, van.Status);
    }

    private sealed class RecordingResolver : IVehicleStateResolver
    {
        public List<VehicleStatus> Requested { get; } = new();

        public IVehicleState For(VehicleStatus status)
        {
            Requested.Add(status);
            return VehicleStateResolver.Default.For(status);
        }
    }
}
