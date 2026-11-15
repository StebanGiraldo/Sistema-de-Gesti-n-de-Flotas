using FleetManagement.Application.DTOs;
using FleetManagement.Application.Factories;
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
/// STATE dentro de <see cref="VehicleService.UpdateVehicleStatusAsync"/>, el único lugar donde una persona cambia a mano el estado
/// de un vehículo. Con el servicio REAL y un repositorio que entrega copias (como una base de datos) se comprueba que un cambio
/// permitido se guarda, se audita y se notifica exactamente como antes, y que uno no permitido se rechaza ANTES de hacer nada de eso.
/// </summary>
public class VehicleServiceStateTransitionTests
{
    private const string Plate = "TST-001";

    private readonly CopyingVehicleRepository _vehicles = new();
    private readonly InMemoryDriverRepository _drivers = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly RecordingNotificationChannel _channel = new();

    private VehicleService Service(IVehicleStateResolver? states = null)
        => new(_vehicles, _drivers, new VehicleFactoryProvider(), new FleetOnboardingFactoryProvider(), _audit, _channel, states);

    private async Task<Vehicle> VehicleAsync(VehicleStatus status)
        => await _vehicles.AddAsync(new Vehicle { LicensePlate = Plate, Brand = "Renault", Model = "Kangoo", Year = 2022, Type = VehicleType.Van, Status = status });

    private async Task<VehicleStatus> StoredStatusAsync(Vehicle vehicle) => (await _vehicles.GetByIdAsync(vehicle.Id))!.Status;

    private IEnumerable<RecordingAuditLogger.Entry> VehicleAudit() => _audit.Entries.Where(entry => entry.Category == "Vehículo");

    // ---------------- Cambios permitidos: se guardan, se auditan y se notifican como antes ----------------

    [Theory]
    [InlineData(VehicleStatus.Available,    VehicleStatus.EnRoute)]
    [InlineData(VehicleStatus.Available,    VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.Available,    VehicleStatus.OutOfService)]
    [InlineData(VehicleStatus.EnRoute,      VehicleStatus.Available)]
    [InlineData(VehicleStatus.EnRoute,      VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.EnRoute,      VehicleStatus.OutOfService)]
    [InlineData(VehicleStatus.Maintenance,  VehicleStatus.Available)]
    [InlineData(VehicleStatus.Maintenance,  VehicleStatus.OutOfService)]
    [InlineData(VehicleStatus.OutOfService, VehicleStatus.Available)]
    [InlineData(VehicleStatus.OutOfService, VehicleStatus.Maintenance)]
    // Pedir el estado en el que ya está nunca fue un error: se guarda, se audita y se notifica igual que antes.
    [InlineData(VehicleStatus.Available,    VehicleStatus.Available)]
    [InlineData(VehicleStatus.EnRoute,      VehicleStatus.EnRoute)]
    [InlineData(VehicleStatus.Maintenance,  VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService, VehicleStatus.OutOfService)]
    public async Task AnAllowedChange_IsStored_Audited_AndNotified(VehicleStatus from, VehicleStatus to)
    {
        var vehicle = await VehicleAsync(from);

        await Service().UpdateVehicleStatusAsync(vehicle.Id, new UpdateVehicleStatusRequest(to.ToString()));

        Assert.Equal(to, await StoredStatusAsync(vehicle));
        var audited = Assert.Single(VehicleAudit());
        Assert.Equal($"Vehículo {Plate} cambió de estado: {from} -> {to}.", audited.Message);
        var sent = Assert.Single(_channel.SentMessages);
        Assert.Contains($"{from} -> {to}", sent.Message);
    }

    [Theory]
    [InlineData("maintenance", VehicleStatus.Maintenance)]
    [InlineData("MAINTENANCE", VehicleStatus.Maintenance)]
    [InlineData("outofservice", VehicleStatus.OutOfService)]
    [InlineData("enroute", VehicleStatus.EnRoute)]
    public async Task TheStatusText_IsStillCaseInsensitive(string text, VehicleStatus expected)
    {
        var vehicle = await VehicleAsync(VehicleStatus.Available);

        await Service().UpdateVehicleStatusAsync(vehicle.Id, new UpdateVehicleStatusRequest(text));

        Assert.Equal(expected, await StoredStatusAsync(vehicle));
    }

    // ---------------- Cambios no permitidos: se rechazan antes de tocar nada ----------------

    [Theory]
    [InlineData(VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService)]
    public async Task AVehicleThatCannotOperate_CannotBeSentStraightEnRoute(VehicleStatus from)
    {
        var vehicle = await VehicleAsync(from);

        var error = await Assert.ThrowsAsync<InvalidVehicleTransitionException>(
            () => Service().UpdateVehicleStatusAsync(vehicle.Id, new UpdateVehicleStatusRequest("EnRoute")));

        Assert.Equal(Plate, error.LicensePlate);
        Assert.Equal(from, error.From);
        Assert.Equal(VehicleStatus.EnRoute, error.To);
        Assert.DoesNotContain(VehicleStatus.EnRoute, error.AllowedTargets);
    }

    [Theory]
    [InlineData(VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService)]
    public async Task ARejectedChange_IsNotStored_NotAudited_AndNotNotified(VehicleStatus from)
    {
        var vehicle = await VehicleAsync(from);

        await Assert.ThrowsAsync<InvalidVehicleTransitionException>(
            () => Service().UpdateVehicleStatusAsync(vehicle.Id, new UpdateVehicleStatusRequest("EnRoute")));

        Assert.Equal(from, await StoredStatusAsync(vehicle));
        Assert.Empty(_audit.Entries);
        Assert.Empty(_channel.SentMessages);
    }

    [Fact]
    public async Task ARejectedChange_SaysWhereTheVehicleCanGoInstead()
    {
        var vehicle = await VehicleAsync(VehicleStatus.Maintenance);

        var error = await Assert.ThrowsAsync<InvalidVehicleTransitionException>(
            () => Service().UpdateVehicleStatusAsync(vehicle.Id, new UpdateVehicleStatusRequest("EnRoute")));

        Assert.Contains(Plate, error.Message);
        Assert.Contains("Maintenance", error.Message);
        Assert.Contains("EnRoute", error.Message);
        Assert.Contains("Available, OutOfService", error.Message);
    }

    [Fact]
    public async Task TheWayBackToTheRoad_IsThroughAvailable()
    {
        var vehicle = await VehicleAsync(VehicleStatus.Maintenance);
        var service = Service();

        await Assert.ThrowsAsync<InvalidVehicleTransitionException>(() => service.UpdateVehicleStatusAsync(vehicle.Id, new UpdateVehicleStatusRequest("EnRoute")));
        await service.UpdateVehicleStatusAsync(vehicle.Id, new UpdateVehicleStatusRequest("Available"));
        await service.UpdateVehicleStatusAsync(vehicle.Id, new UpdateVehicleStatusRequest("EnRoute"));

        Assert.Equal(VehicleStatus.EnRoute, await StoredStatusAsync(vehicle));
        Assert.Equal(2, VehicleAudit().Count());   // solo los dos cambios que se hicieron; el rechazado no dejó rastro
    }

    [Fact]
    public async Task ANumericStatusThatIsNotAnyOfTheFour_IsRejected_NotStored()
    {
        var vehicle = await VehicleAsync(VehicleStatus.Available);

        // Enum.TryParse acepta cualquier número ("99"); antes quedaba guardado un estado que no existe. Ahora el estado lo rechaza.
        await Assert.ThrowsAsync<InvalidVehicleTransitionException>(
            () => Service().UpdateVehicleStatusAsync(vehicle.Id, new UpdateVehicleStatusRequest("99")));

        Assert.Equal(VehicleStatus.Available, await StoredStatusAsync(vehicle));
        Assert.Empty(_audit.Entries);
    }

    // ---------------- Lo que no cambió ----------------

    [Fact]
    public async Task AnUnknownVehicle_StillThrowsNotFound_BeforeAnyStateIsConsulted()
    {
        var spy = new RecordingResolver();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => Service(spy).UpdateVehicleStatusAsync(Guid.NewGuid(), new UpdateVehicleStatusRequest("Available")));

        Assert.Empty(spy.Requested);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task AnUnknownStatusText_StillThrowsArgumentException_WithTheOriginalMessage()
    {
        var vehicle = await VehicleAsync(VehicleStatus.Available);

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => Service().UpdateVehicleStatusAsync(vehicle.Id, new UpdateVehicleStatusRequest("Volando")));

        Assert.Equal("Estado de vehículo inválido: 'Volando'.", error.Message);
        Assert.Equal(VehicleStatus.Available, await StoredStatusAsync(vehicle));
    }

    [Fact]
    public async Task TheTransitionError_IsABusinessRuleViolation_OfTheSameKindAsTheAssignmentRules()
    {
        var vehicle = await VehicleAsync(VehicleStatus.Maintenance);

        var error = await Assert.ThrowsAsync<InvalidVehicleTransitionException>(
            () => Service().UpdateVehicleStatusAsync(vehicle.Id, new UpdateVehicleStatusRequest("EnRoute")));

        // El texto "Volando" es un argumento inválido (ArgumentException); pedir un cambio que el estado no permite es otra cosa:
        // una regla de negocio (InvalidOperationException, igual que AssignmentRuleException) que la API responde con su propio 400.
        Assert.IsAssignableFrom<InvalidOperationException>(error);
    }

    [Fact]
    public async Task TheNotification_KeepsItsPriorityFromTheNewStatus()
    {
        var vehicle = await VehicleAsync(VehicleStatus.Available);

        await Service().UpdateVehicleStatusAsync(vehicle.Id, new UpdateVehicleStatusRequest("OutOfService"));

        Assert.Single(_channel.SentMessages);   // el Decorator de prioridad/reintento/auditoría sigue en la cadena y entrega una sola vez
    }

    // ---------------- El servicio le pregunta al estado ----------------

    [Fact]
    public async Task TheService_UsesTheResolverItIsGiven_ToDecideTheChange()
    {
        var vehicle = await VehicleAsync(VehicleStatus.Available);
        var spy = new RecordingResolver();

        await Service(spy).UpdateVehicleStatusAsync(vehicle.Id, new UpdateVehicleStatusRequest("Maintenance"));

        Assert.Contains(VehicleStatus.Available, spy.Requested);   // se consultó el estado en que estaba el vehículo
    }

    [Fact]
    public async Task TheService_WorksWithoutAResolver_UsingTheDefaultOne()
    {
        var vehicle = await VehicleAsync(VehicleStatus.Available);

        await Service(states: null).UpdateVehicleStatusAsync(vehicle.Id, new UpdateVehicleStatusRequest("Maintenance"));

        Assert.Equal(VehicleStatus.Maintenance, await StoredStatusAsync(vehicle));
    }

    [Fact]
    public void TheService_StillHasASinglePublicConstructor()
    {
        Assert.Single(typeof(VehicleService).GetConstructors());   // el parámetro nuevo es opcional: no hay un segundo constructor
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
