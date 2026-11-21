using FleetManagement.Application.Assignment;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Mediators;
using FleetManagement.Application.Tests.Decorator;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Assignment;

/// <summary>
/// Asignar conductor y/o vehículo a una ruta que ya existe y no los tiene. Se usan los repositorios en memoria reales, el
/// validador REAL de asignaciones y el participante de rutas REAL (Mediator); solo la consulta de la ruta (DTO) y el
/// mantenimiento vencido son dobles sencillos.
/// </summary>
public class RouteAssignmentServiceTests
{
    private readonly AssignmentKit _kit = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly RouteAssignmentService _service;

    public RouteAssignmentServiceTests()
    {
        _service = new RouteAssignmentService(
            _kit.Routes, _kit.Vehicles, _kit.Drivers, _kit.Validator,
            FleetMediators.CreateRouteColleague(_kit.Routes, _kit.Vehicles),
            new RouteQueryStub(_kit.Routes), _audit);
    }

    private static AssignRouteRequest ForDriver(Driver driver) => new(driver.Id, null);
    private static AssignRouteRequest ForVehicle(Vehicle vehicle) => new(null, vehicle.Id);

    // ---------------- Camino feliz ----------------

    [Fact]
    public async Task AssignsAnAvailableDriver_ToAPlannedRouteThatHasNone()
    {
        var driver = await _kit.AddDriverAsync();
        var route = await _kit.AddRouteAsync("Ruta duplicada", DeliveryRouteStatus.Planned);

        var result = await _service.AssignAsync(route.Id, ForDriver(driver));

        Assert.Equal(driver.Id, result.AssignedDriverId);
        Assert.Equal(driver.Id, (await _kit.Routes.GetByIdAsync(route.Id))!.AssignedDriverId);
        Assert.Null(result.AssignedVehicleId);
    }

    [Fact]
    public async Task AssignsDriverAndVehicleTogether_AndRecalculatesTheEstimatedDurationForTheVehicle()
    {
        var driver = await _kit.AddDriverAsync();
        var vehicle = await _kit.AddVehicleAsync();
        var route = await _kit.AddRouteAsync("Ruta larga", DeliveryRouteStatus.Planned);
        route.EstimatedDistanceKm = 55;      // sin perfiles de vehículo se usa la velocidad por defecto: 55 km/h
        route.EstimatedDurationMinutes = 1;

        await _service.AssignAsync(route.Id, new AssignRouteRequest(driver.Id, vehicle.Id));

        var stored = (await _kit.Routes.GetByIdAsync(route.Id))!;
        Assert.Equal(vehicle.Id, stored.AssignedVehicleId);
        Assert.Equal(driver.Id, stored.AssignedDriverId);
        Assert.Equal(60, stored.EstimatedDurationMinutes);
    }

    [Fact]
    public async Task AnAssignmentIsAudited()
    {
        var driver = await _kit.AddDriverAsync();
        var route = await _kit.AddRouteAsync("Ruta auditada", DeliveryRouteStatus.Planned);

        await _service.AssignAsync(route.Id, ForDriver(driver));

        var entry = Assert.Single(_audit.Entries.Where(e => e.Category == "Ruta"));
        Assert.Contains("Ruta auditada", entry.Message);
    }

    [Fact]
    public async Task AskingForWhatTheRouteAlreadyHas_ChangesNothingAndAuditsNothing()
    {
        var driver = await _kit.AddDriverAsync();
        var route = await _kit.AddRouteAsync("Ya asignada", DeliveryRouteStatus.Planned, driver: driver);

        await _service.AssignAsync(route.Id, ForDriver(driver));

        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task ARouteAlreadyInProgress_ThatGetsAVehicle_LeavesItEnRoute()
    {
        var driver = await _kit.AddDriverAsync();
        var vehicle = await _kit.AddVehicleAsync();
        var route = await _kit.AddRouteAsync("En curso sin vehículo", DeliveryRouteStatus.InProgress, driver: driver);

        await _service.AssignAsync(route.Id, ForVehicle(vehicle));

        Assert.Equal(VehicleStatus.EnRoute, (await _kit.Vehicles.GetByIdAsync(vehicle.Id))!.Status);
    }

    [Fact]
    public async Task APlannedRoute_ThatGetsAVehicle_DoesNotTouchTheVehicleState()
    {
        var vehicle = await _kit.AddVehicleAsync();
        var route = await _kit.AddRouteAsync("Planificada", DeliveryRouteStatus.Planned);

        await _service.AssignAsync(route.Id, ForVehicle(vehicle));

        Assert.Equal(VehicleStatus.Available, (await _kit.Vehicles.GetByIdAsync(vehicle.Id))!.Status);
    }

    // ---------------- Petición inválida ----------------

    [Fact]
    public async Task UnknownRoute_IsNotFound()
        => await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.AssignAsync(Guid.NewGuid(), new AssignRouteRequest(Guid.NewGuid(), null)));

    [Fact]
    public async Task AskingForNothing_IsRejected()
    {
        var route = await _kit.AddRouteAsync("Ruta", DeliveryRouteStatus.Planned);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.AssignAsync(route.Id, new AssignRouteRequest(null, null)));
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.Completed)]
    [InlineData(DeliveryRouteStatus.Cancelled)]
    public async Task ARouteThatIsAlreadyClosed_DoesNotAcceptAssignments(DeliveryRouteStatus closed)
    {
        var driver = await _kit.AddDriverAsync();
        var route = await _kit.AddRouteAsync("Cerrada", closed);

        await Assert.ThrowsAsync<AssignmentRuleException>(() => _service.AssignAsync(route.Id, ForDriver(driver)));

        Assert.Null((await _kit.Routes.GetByIdAsync(route.Id))!.AssignedDriverId);
    }

    [Fact]
    public async Task ItOnlyFillsWhatIsMissing_ItDoesNotReplaceTheCurrentDriver()
    {
        var current = await _kit.AddDriverAsync("Ana");
        var other = await _kit.AddDriverAsync("Luis");
        var route = await _kit.AddRouteAsync("Con conductor", DeliveryRouteStatus.Planned, driver: current);

        await Assert.ThrowsAsync<AssignmentRuleException>(() => _service.AssignAsync(route.Id, ForDriver(other)));

        Assert.Equal(current.Id, (await _kit.Routes.GetByIdAsync(route.Id))!.AssignedDriverId);
    }

    [Fact]
    public async Task UnknownDriver_IsRejected()
    {
        var route = await _kit.AddRouteAsync("Ruta", DeliveryRouteStatus.Planned);

        var error = await Assert.ThrowsAsync<AssignmentRuleException>(() => _service.AssignAsync(route.Id, new AssignRouteRequest(Guid.NewGuid(), null)));

        Assert.Contains("conductor", error.Message);
    }

    // ---------------- Conductor ocupado ----------------

    [Theory]
    [InlineData(DeliveryRouteStatus.InProgress)]
    [InlineData(DeliveryRouteStatus.Delayed)]
    public async Task ADriverInAnotherActiveTrip_IsRejected(DeliveryRouteStatus active)
    {
        var driver = await _kit.AddDriverAsync("Ana");
        await _kit.AddRouteAsync("Viaje en curso", active, driver: driver);
        var route = await _kit.AddRouteAsync("Sin conductor", DeliveryRouteStatus.Planned);

        var error = await Assert.ThrowsAsync<AssignmentRuleException>(() => _service.AssignAsync(route.Id, ForDriver(driver)));

        Assert.Contains("Viaje en curso", error.Message);
        Assert.Null((await _kit.Routes.GetByIdAsync(route.Id))!.AssignedDriverId);
    }

    [Fact]
    public async Task ADriverWhoOnlyHasAPlannedRoute_IsStillAvailable()
    {
        var driver = await _kit.AddDriverAsync();
        await _kit.AddRouteAsync("Planificada", DeliveryRouteStatus.Planned, driver: driver);
        var route = await _kit.AddRouteAsync("Otra", DeliveryRouteStatus.Planned);

        await _service.AssignAsync(route.Id, ForDriver(driver));

        Assert.Equal(driver.Id, (await _kit.Routes.GetByIdAsync(route.Id))!.AssignedDriverId);
    }

    // ---------------- Vehículo ----------------

    [Theory]
    [InlineData(VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService)]
    public async Task AVehicleThatCannotOperate_IsRejected(VehicleStatus status)
    {
        var vehicle = await _kit.AddVehicleAsync(status: status);
        var route = await _kit.AddRouteAsync("Ruta", DeliveryRouteStatus.Planned);

        await Assert.ThrowsAsync<AssignmentRuleException>(() => _service.AssignAsync(route.Id, ForVehicle(vehicle)));

        Assert.Null((await _kit.Routes.GetByIdAsync(route.Id))!.AssignedVehicleId);
    }

    [Fact]
    public async Task AVehicleWithOverdueMaintenance_IsRejected()
    {
        var vehicle = await _kit.AddVehicleAsync();
        _kit.MarkOverdue(vehicle);
        var route = await _kit.AddRouteAsync("Ruta", DeliveryRouteStatus.Planned);

        var error = await Assert.ThrowsAsync<AssignmentRuleException>(() => _service.AssignAsync(route.Id, ForVehicle(vehicle)));

        Assert.Contains("mantenimiento vencido", error.Message);
    }

    [Fact]
    public async Task AVehicleTooSmallForTheRoutesCargo_IsRejected()
    {
        var vehicle = await _kit.AddVehicleAsync(capacityKg: 100);
        var route = await _kit.AddRouteAsync("Carga pesada", DeliveryRouteStatus.Planned);
        route.CargoItems.Add(new CargoItem { Description = "Maquinaria", WeightKg = 500, VolumeM3 = 1 });

        var error = await Assert.ThrowsAsync<AssignmentRuleException>(() => _service.AssignAsync(route.Id, ForVehicle(vehicle)));

        Assert.Contains("supera la capacidad", error.Message);
    }

    [Fact]
    public async Task ARouteInProgress_CannotTakeAVehicleThatIsInAnotherActiveTrip()
    {
        var vehicle = await _kit.AddVehicleAsync();
        await _kit.AddRouteAsync("Otro viaje", DeliveryRouteStatus.InProgress, vehicle);
        var route = await _kit.AddRouteAsync("En curso", DeliveryRouteStatus.InProgress);

        await Assert.ThrowsAsync<AssignmentRuleException>(() => _service.AssignAsync(route.Id, ForVehicle(vehicle)));
    }

    [Fact]
    public async Task APlannedRoute_CanBePlannedAheadWithAVehicleThatIsOnAnotherTripToday()
    {
        var vehicle = await _kit.AddVehicleAsync();
        await _kit.AddRouteAsync("Viaje de hoy", DeliveryRouteStatus.InProgress, vehicle);
        var route = await _kit.AddRouteAsync("Mañana", DeliveryRouteStatus.Planned);

        await _service.AssignAsync(route.Id, ForVehicle(vehicle));

        Assert.Equal(vehicle.Id, (await _kit.Routes.GetByIdAsync(route.Id))!.AssignedVehicleId);
    }

    [Fact]
    public async Task EveryBrokenRuleIsReportedAtOnce()
    {
        var driver = await _kit.AddDriverAsync("Ana");
        await _kit.AddRouteAsync("Viaje de Ana", DeliveryRouteStatus.InProgress, driver: driver);
        var vehicle = await _kit.AddVehicleAsync(status: VehicleStatus.Maintenance);
        var route = await _kit.AddRouteAsync("Ruta", DeliveryRouteStatus.Planned);

        var error = await Assert.ThrowsAsync<AssignmentRuleException>(() => _service.AssignAsync(route.Id, new AssignRouteRequest(driver.Id, vehicle.Id)));

        Assert.True(error.Violations.Count >= 2);
    }

    // ---------------- Opciones del selector ----------------

    [Fact]
    public async Task Options_OfferOnlyDriversAndVehiclesThatPassTheRules()
    {
        var free = await _kit.AddDriverAsync("Libre");
        var busy = await _kit.AddDriverAsync("Ocupado");
        await _kit.AddRouteAsync("Viaje", DeliveryRouteStatus.Delayed, driver: busy);

        var ok = await _kit.AddVehicleAsync("OK-001");
        var workshop = await _kit.AddVehicleAsync("TAL-001", VehicleStatus.Maintenance);
        var overdue = await _kit.AddVehicleAsync("VEN-001");
        _kit.MarkOverdue(overdue);
        var onTrip = await _kit.AddVehicleAsync("RUT-001");
        await _kit.AddRouteAsync("Otro viaje", DeliveryRouteStatus.InProgress, onTrip);
        var small = await _kit.AddVehicleAsync("PEQ-001", capacityKg: 10);

        var route = await _kit.AddRouteAsync("Ruta a asignar", DeliveryRouteStatus.Planned);
        route.CargoItems.Add(new CargoItem { Description = "Cajas", WeightKg = 200, VolumeM3 = 1 });

        var options = await _service.GetOptionsAsync(route.Id);

        Assert.Equal(new[] { free.Id }, options.Drivers.Select(d => d.Id));
        Assert.Equal(new[] { ok.Id }, options.Vehicles.Select(v => v.Id));
        Assert.DoesNotContain(options.Vehicles, v => v.Id == workshop.Id || v.Id == overdue.Id || v.Id == onTrip.Id || v.Id == small.Id);
    }

    [Fact]
    public async Task Options_ForAnUnknownRoute_AreNotFound()
        => await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetOptionsAsync(Guid.NewGuid()));

    /// <summary>Consulta mínima de la ruta (lo que haría IDeliveryRouteService); el resto de sus operaciones no se usan aquí.</summary>
    private sealed class RouteQueryStub : IDeliveryRouteService
    {
        private readonly FleetManagement.Application.Interfaces.Repositories.IDeliveryRouteRepository _routes;

        public RouteQueryStub(FleetManagement.Application.Interfaces.Repositories.IDeliveryRouteRepository routes) => _routes = routes;

        public async Task<DeliveryRouteDto?> GetRouteByIdAsync(Guid id)
        {
            var r = await _routes.GetByIdAsync(id);
            return r is null ? null : new DeliveryRouteDto(
                r.Id, r.Name, 0, 0, 0, 0, new List<WaypointDto>(), new List<CargoItemDto>(),
                r.AssignedVehicleId, null, r.AssignedDriverId, null,
                r.Status.ToString(), r.EstimatedDistanceKm, r.EstimatedDurationMinutes, r.CreatedAt, r.ScheduledDate, r.DelayMinutes);
        }

        public Task<IReadOnlyList<DeliveryRouteDto>> GetAllRoutesAsync() => throw new NotSupportedException();
        public Task<IReadOnlyList<DeliveryRouteDto>> GetRoutesByDriverAsync(Guid driverId) => throw new NotSupportedException();
        public Task<DeliveryRouteDto> CreateRouteAsync(CreateDeliveryRouteRequest request) => throw new NotSupportedException();
        public Task<DeliveryRouteDto> CreateExpressRouteAsync(CreateExpressRouteRequest request) => throw new NotSupportedException();
        public Task<DeliveryRouteDto> DuplicateRouteAsync(Guid routeId, DuplicateRouteRequest request) => throw new NotSupportedException();
        public Task UpdateRouteStatusAsync(Guid id, string status) => throw new NotSupportedException();
    }
}
