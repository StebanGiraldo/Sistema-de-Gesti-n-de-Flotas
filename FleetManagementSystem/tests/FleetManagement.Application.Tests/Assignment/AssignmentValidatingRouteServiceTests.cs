using FleetManagement.Application.Assignment;
using FleetManagement.Application.Builders;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Tests.Support;
using Xunit;

namespace FleetManagement.Application.Tests.Assignment;

/// <summary>
/// PATRÓN DECORATOR sobre el servicio de rutas: valida ANTES de delegar, delega todo lo demás sin tocarlo y nunca
/// bloquea terminar o cancelar un viaje. Se prueba con un servicio y un validador de mentira que anotan cada llamada.
/// </summary>
public class AssignmentValidatingRouteServiceTests
{
    private readonly List<string> _calls = new();
    private readonly RecordingRouteService _inner;
    private readonly RecordingValidator _validator;
    private readonly AssignmentValidatingRouteService _service;

    public AssignmentValidatingRouteServiceTests()
    {
        _inner = new RecordingRouteService(_calls);
        _validator = new RecordingValidator(_calls);
        _service = new AssignmentValidatingRouteService(_inner, _validator);
    }

    [Fact]
    public async Task CreateRoute_ValidatesBeforeDelegating_WithTheVehicleDriverAndCargoOfTheRequest()
    {
        var vehicleId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var cargo = new List<CargoItemDto> { new("Caja", 12, 0.5, "Standard") };
        var request = new CreateDeliveryRouteRequest("Ruta", 1, 2, 3, 4, null, cargo, vehicleId, driverId, null);

        var created = await _service.CreateRouteAsync(request);

        Assert.Equal(new[] { "validar-nueva", "crear" }, _calls);
        Assert.Equal(vehicleId, _validator.NewRouteVehicleId);
        Assert.Equal(driverId, _validator.NewRouteDriverId);
        Assert.Same(cargo, _validator.NewRouteCargo);
        Assert.Same(_inner.Created, created);
    }

    [Fact]
    public async Task CreateRoute_WhenAnAssignmentRuleFails_NeverReachesTheRealService()
    {
        _validator.Failure = new AssignmentRuleException(new[] { "no cabe" });
        var request = new CreateDeliveryRouteRequest("Ruta", 1, 2, 3, 4, null, null, Guid.NewGuid(), null, null);

        var error = await Assert.ThrowsAsync<AssignmentRuleException>(() => _service.CreateRouteAsync(request));

        Assert.Equal("no cabe", error.Message);
        Assert.Equal(new[] { "validar-nueva" }, _calls);
    }

    [Fact]
    public async Task CreateExpressRoute_IsValidatedWithTheFixedCargoOfTheExpressRecipe()
    {
        var vehicleId = Guid.NewGuid();
        var driverId = Guid.NewGuid();

        await _service.CreateExpressRouteAsync(new CreateExpressRouteRequest("Express", 1, 2, 3, 4, vehicleId, driverId));

        Assert.Equal(new[] { "validar-nueva", "crear-express" }, _calls);
        Assert.Equal(vehicleId, _validator.NewRouteVehicleId);
        Assert.Equal(driverId, _validator.NewRouteDriverId);
        var cargo = Assert.Single(_validator.NewRouteCargo!);
        Assert.Equal(DeliveryRouteDirector.ExpressCargoWeightKg, cargo.WeightKg);
        Assert.Equal(DeliveryRouteDirector.ExpressCargoVolumeM3, cargo.VolumeM3);
    }

    [Fact]
    public void ExpressCargoConstants_MatchWhatTheDirectorReallyLoads()
    {
        var route = new DeliveryRouteDirector().BuildExpressRoute(new DeliveryRouteBuilder(), "Express", 1, 2, 3, 4, Guid.NewGuid(), Guid.NewGuid(), 10, 20);

        var item = Assert.Single(route.CargoItems);
        Assert.Equal(DeliveryRouteDirector.ExpressCargoDescription, item.Description);
        Assert.Equal(DeliveryRouteDirector.ExpressCargoWeightKg, item.WeightKg);
        Assert.Equal(DeliveryRouteDirector.ExpressCargoVolumeM3, item.VolumeM3);
    }

    [Fact]
    public async Task CreateExpressRoute_WhenAnAssignmentRuleFails_NeverReachesTheRealService()
    {
        _validator.Failure = new AssignmentRuleException(new[] { "no cabe" });

        await Assert.ThrowsAsync<AssignmentRuleException>(() => _service.CreateExpressRouteAsync(new CreateExpressRouteRequest("Express", 1, 2, 3, 4, Guid.NewGuid(), Guid.NewGuid())));

        Assert.Equal(new[] { "validar-nueva" }, _calls);
    }

    [Theory]
    [InlineData("InProgress")]
    [InlineData("Delayed")]
    [InlineData("inprogress")]
    [InlineData("DELAYED")]
    public async Task UpdateStatus_ThatStartsATrip_ValidatesTheStartBeforeDelegating(string status)
    {
        var routeId = Guid.NewGuid();

        await _service.UpdateRouteStatusAsync(routeId, status);

        Assert.Equal(new[] { "validar-inicio", "estado" }, _calls);
        Assert.Equal(routeId, _validator.StartedRouteId);
        Assert.Equal(status, _inner.LastStatus);
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    [InlineData("Planned")]
    [InlineData("no-es-un-estado")]
    public async Task UpdateStatus_ThatDoesNotStartATrip_IsNeverBlocked(string status)
    {
        await _service.UpdateRouteStatusAsync(Guid.NewGuid(), status);

        Assert.Equal(new[] { "estado" }, _calls);
    }

    [Fact]
    public async Task UpdateStatus_WhenTheStartIsNotAllowed_NeverReachesTheRealService()
    {
        _validator.Failure = new AssignmentRuleException(new[] { "ya está en otro viaje" });

        await Assert.ThrowsAsync<AssignmentRuleException>(() => _service.UpdateRouteStatusAsync(Guid.NewGuid(), "InProgress"));

        Assert.Equal(new[] { "validar-inicio" }, _calls);
    }

    [Fact]
    public async Task QueriesAndDuplicate_AreDelegatedUntouchedAndWithoutValidation()
    {
        var id = Guid.NewGuid();
        var duplicate = new DuplicateRouteRequest(null);

        Assert.Same(_inner.All, await _service.GetAllRoutesAsync());
        Assert.Same(_inner.One, await _service.GetRouteByIdAsync(id));
        Assert.Same(_inner.ByDriver, await _service.GetRoutesByDriverAsync(id));
        Assert.Same(_inner.Created, await _service.DuplicateRouteAsync(id, duplicate));

        Assert.Equal(new[] { "todas", "una", "por-conductor", "duplicar" }, _calls);
    }

    // ------------------------------------------------------------------ dobles de prueba

    private sealed class RecordingRouteService : IDeliveryRouteService
    {
        private readonly List<string> _calls;
        public RecordingRouteService(List<string> calls) => _calls = calls;

        public IReadOnlyList<DeliveryRouteDto> All { get; } = new List<DeliveryRouteDto>();
        public DeliveryRouteDto One { get; } = TestData.Route("Una");
        public IReadOnlyList<DeliveryRouteDto> ByDriver { get; } = new List<DeliveryRouteDto>();
        public DeliveryRouteDto Created { get; } = TestData.Route("Creada");
        public string? LastStatus { get; private set; }

        public Task<IReadOnlyList<DeliveryRouteDto>> GetAllRoutesAsync() { _calls.Add("todas"); return Task.FromResult(All); }
        public Task<DeliveryRouteDto?> GetRouteByIdAsync(Guid id) { _calls.Add("una"); return Task.FromResult<DeliveryRouteDto?>(One); }
        public Task<IReadOnlyList<DeliveryRouteDto>> GetRoutesByDriverAsync(Guid driverId) { _calls.Add("por-conductor"); return Task.FromResult(ByDriver); }
        public Task<DeliveryRouteDto> CreateRouteAsync(CreateDeliveryRouteRequest request) { _calls.Add("crear"); return Task.FromResult(Created); }
        public Task<DeliveryRouteDto> CreateExpressRouteAsync(CreateExpressRouteRequest request) { _calls.Add("crear-express"); return Task.FromResult(Created); }
        public Task<DeliveryRouteDto> DuplicateRouteAsync(Guid routeId, DuplicateRouteRequest request) { _calls.Add("duplicar"); return Task.FromResult(Created); }
        public Task UpdateRouteStatusAsync(Guid id, string status) { _calls.Add("estado"); LastStatus = status; return Task.CompletedTask; }
    }

    private sealed class RecordingValidator : IRouteAssignmentValidator
    {
        private readonly List<string> _calls;
        public RecordingValidator(List<string> calls) => _calls = calls;

        public Exception? Failure { get; set; }
        public Guid? NewRouteVehicleId { get; private set; }
        public Guid? NewRouteDriverId { get; private set; }
        public IEnumerable<CargoItemDto>? NewRouteCargo { get; private set; }
        public Guid? StartedRouteId { get; private set; }

        public Task ValidateNewRouteAsync(Guid? vehicleId, Guid? driverId, IEnumerable<CargoItemDto>? cargo)
        {
            _calls.Add("validar-nueva");
            (NewRouteVehicleId, NewRouteDriverId, NewRouteCargo) = (vehicleId, driverId, cargo);
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }

        public Task ValidateStartAsync(Guid routeId)
        {
            _calls.Add("validar-inicio");
            StartedRouteId = routeId;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }
}
