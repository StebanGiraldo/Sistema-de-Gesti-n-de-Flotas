using FleetManagement.Application.Interpreter;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Interpreter;

/// <summary>
/// El servicio de punta a punta con los repositorios reales en memoria (<see cref="RealFleet"/>) y un reloj manual: cuatro
/// vehículos con un historial conocido, así que cada regla tiene un resultado exacto.
/// <code>
/// TRK-100  camión  Available   50000 km  8000 kg  2018  con conductor  último mantenimiento: hace 200 días a 41000 km
/// VAN-200  furgón  EnRoute     20000 km  1500 kg  2023  sin conductor  último mantenimiento: hace  10 días a 19000 km
/// CAR-300  carro   Maintenance 60000 km   400 kg  2021  sin conductor  último mantenimiento: hace 100 días a 59500 km
/// TRK-400  camión  Available   10000 km  9000 kg  2022  sin conductor  sin mantenimientos
/// </code>
/// </summary>
public class RuleEvaluationServiceTests
{
    private sealed class World
    {
        public RealFleet Fleet { get; } = new();
        public ManualTimeProvider Clock { get; } = new();
        public RuleEvaluationService Service { get; private set; } = null!;

        public static async Task<World> CreateAsync()
        {
            var world = new World();
            await world.AddVehicleAsync(RuleTestData.Vehicle("TRK-100", VehicleType.Truck, VehicleStatus.Available, 50000, 8000, 2018, Guid.NewGuid()), daysAgo: 200, mileageAtService: 41000);
            await world.AddVehicleAsync(RuleTestData.Vehicle("VAN-200", VehicleType.Van, VehicleStatus.EnRoute, 20000, 1500, 2023), daysAgo: 10, mileageAtService: 19000);
            await world.AddVehicleAsync(RuleTestData.Vehicle("CAR-300", VehicleType.Car, VehicleStatus.Maintenance, 60000, 400, 2021), daysAgo: 100, mileageAtService: 59500);
            await world.AddVehicleAsync(RuleTestData.Vehicle("TRK-400", VehicleType.Truck, VehicleStatus.Available, 10000, 9000, 2022));
            world.Service = new RuleEvaluationService(
                new RuleParser(),
                new MaintenanceRuleScope(world.Fleet.VehicleRepository, world.Fleet.MaintenanceRepository, world.Clock),
                new MonitoringRuleScope(world.Fleet.VehicleRepository));
            return world;
        }

        private async Task AddVehicleAsync(Vehicle vehicle, int? daysAgo = null, double mileageAtService = 0)
        {
            await Fleet.VehicleRepository.AddAsync(vehicle);
            if (daysAgo is null) return;

            await Fleet.MaintenanceRepository.AddAsync(new MaintenanceRecord
            {
                VehicleId = vehicle.Id,
                Type = MaintenanceType.GeneralInspection,
                PerformedAt = Clock.GetUtcNow().UtcDateTime.AddDays(-daysAgo.Value),
                MileageAtServiceKm = mileageAtService
            });
        }
    }

    private static string[] Plates(RuleEvaluationDto result) => result.Vehicles.Select(vehicle => vehicle.LicensePlate).Order(StringComparer.Ordinal).ToArray();

    private static string[] Expected(string plates) => plates.Split(',', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).ToArray();

    // ---------- Mantenimiento predictivo ----------

    [Theory]
    [InlineData("kmSinceLastService >= 8000 OR daysSinceLastService > 180", "TRK-100")]
    [InlineData("kmSinceLastService >= 8000 AND daysSinceLastService > 180", "TRK-100")]
    [InlineData("daysSinceLastService > 90", "CAR-300,TRK-100")]
    [InlineData("daysSinceLastService >= 0", "CAR-300,TRK-100,VAN-200")]     // TRK-400 no tiene mantenimientos: no entra, no se le inventa un 0
    [InlineData("type = \"Truck\" AND NOT status = \"Maintenance\"", "TRK-100,TRK-400")]
    [InlineData("vehicleAgeYears >= 8", "TRK-100")]
    [InlineData("mileageKm > 55000", "CAR-300")]
    [InlineData("NOT daysSinceLastService > 90", "TRK-400,VAN-200")]          // sin dato la comparación es falsa y NOT la invierte (documentado)
    public async Task AMaintenanceRule_ListsTheVehiclesThatMeetIt(string rule, string plates)
    {
        var world = await World.CreateAsync();

        var result = await world.Service.EvaluateMaintenanceRuleAsync(rule);

        Assert.Equal(Expected(plates), Plates(result));
        Assert.Equal(rule, result.Rule);
        Assert.Equal(4, result.EvaluatedVehicles);
        Assert.Equal(result.Vehicles.Count, result.MatchedVehicles);
    }

    [Fact]
    public async Task TheMatches_CarryTheDataToIdentifyTheVehicle()
    {
        var world = await World.CreateAsync();

        var result = await world.Service.EvaluateMaintenanceRuleAsync("kmSinceLastService >= 8000");

        var match = Assert.Single(result.Vehicles);
        var truck = (await world.Fleet.VehicleRepository.GetByLicensePlateAsync("TRK-100"))!;
        Assert.Equal(new RuleMatchedVehicleDto(truck.Id, "TRK-100", "Truck", "Available"), match);
    }

    [Fact]
    public async Task TheMaintenanceRules_UseTheClockOfTheApplication()
    {
        var world = await World.CreateAsync();
        var before = await world.Service.EvaluateMaintenanceRuleAsync("daysSinceLastService > 180");

        world.Clock.Advance(TimeSpan.FromDays(100));
        var after = await world.Service.EvaluateMaintenanceRuleAsync("daysSinceLastService > 180");

        Assert.Equal(new[] { "TRK-100" }, Plates(before));
        Assert.Equal(new[] { "CAR-300", "TRK-100" }, Plates(after));      // CAR-300: 100 + 100 días
    }

    // ---------- Filtros de monitoreo ----------

    [Theory]
    [InlineData("status = \"Available\" AND type = \"Truck\"", "TRK-100,TRK-400")]
    [InlineData("status = \"available\" AND type = \"truck\"", "TRK-100,TRK-400")]
    [InlineData("hasDriver = 0", "CAR-300,TRK-400,VAN-200")]
    [InlineData("hasDriver = 1 AND capacityKg >= 8000", "TRK-100")]
    [InlineData("mileageKm > 15000 AND mileageKm <= 50000", "TRK-100,VAN-200")]
    [InlineData("status != \"Available\"", "CAR-300,VAN-200")]
    [InlineData("capacityKg < 1000 OR type = \"Van\"", "CAR-300,VAN-200")]
    [InlineData("status = \"OutOfService\"", "")]
    public async Task AMonitoringFilter_ListsTheVehiclesThatMeetIt(string rule, string plates)
    {
        var world = await World.CreateAsync();

        var result = await world.Service.FilterVehiclesAsync(rule);

        Assert.Equal(Expected(plates), Plates(result));
        Assert.Equal(4, result.EvaluatedVehicles);
        Assert.Equal(result.Vehicles.Count, result.MatchedVehicles);
    }

    // ---------- Reglas inválidas ----------

    [Theory]
    [InlineData("")]
    [InlineData("kmSinceLastService >=")]
    [InlineData("hasDriver = 0")]            // variable del filtro de monitoreo, no del mantenimiento
    [InlineData("kmSinceLastService > 1 OR fuel > 2")]
    [InlineData("status = \"Available")]
    public async Task AnInvalidMaintenanceRule_ThrowsASyntaxError(string rule)
    {
        var world = await World.CreateAsync();

        await Assert.ThrowsAsync<RuleSyntaxException>(() => world.Service.EvaluateMaintenanceRuleAsync(rule));
    }

    [Theory]
    [InlineData("")]
    [InlineData("status =")]
    [InlineData("kmSinceLastService > 1")]   // variable del mantenimiento, no del monitoreo
    [InlineData("vehicleAgeYears > 1")]
    public async Task AnInvalidMonitoringFilter_ThrowsASyntaxError(string rule)
    {
        var world = await World.CreateAsync();

        await Assert.ThrowsAsync<RuleSyntaxException>(() => world.Service.FilterVehiclesAsync(rule));
    }

    [Fact]
    public async Task AnInvalidRule_IsRejectedBeforeReadingAnyData()
    {
        var scope = new CountingScope();
        var service = new RuleEvaluationService(new RuleParser(), scope, scope);

        await Assert.ThrowsAsync<RuleSyntaxException>(() => service.EvaluateMaintenanceRuleAsync("a >"));
        Assert.Equal(0, scope.Loads);

        await service.FilterVehiclesAsync("a = 1");
        Assert.Equal(1, scope.Loads);
    }

    // ---------- Solo lectura ----------

    [Fact]
    public async Task EvaluatingRules_ChangesNothing_NotTheVehicles_NotTheHistory_NotTheAudit_NotTheNotifications()
    {
        var world = await World.CreateAsync();
        var before = await SnapshotAsync(world);

        await world.Service.EvaluateMaintenanceRuleAsync("daysSinceLastService >= 0 OR NOT status = \"Maintenance\"");
        await world.Service.FilterVehiclesAsync("mileageKm >= 0");

        Assert.Equal(before, await SnapshotAsync(world));
        Assert.Empty(world.Fleet.Audit.Entries);
        Assert.Empty(world.Fleet.Channel.SentMessages);
    }

    [Fact]
    public async Task AVehicleInMaintenanceThatMeetsARule_StaysInMaintenance_OnlyRegisteringTheMaintenanceFreesIt()
    {
        var world = await World.CreateAsync();

        var result = await world.Service.EvaluateMaintenanceRuleAsync("status = \"Maintenance\" AND daysSinceLastService > 90");

        Assert.Equal(new[] { "CAR-300" }, Plates(result));
        var car = (await world.Fleet.VehicleRepository.GetByLicensePlateAsync("CAR-300"))!;
        Assert.Equal(VehicleStatus.Maintenance, car.Status);
    }

    private static async Task<string> SnapshotAsync(World world)
    {
        var vehicles = await world.Fleet.VehicleRepository.GetAllAsync();
        var records = await world.Fleet.MaintenanceRepository.GetAllAsync();
        return string.Join(";", vehicles.Select(vehicle => $"{vehicle.LicensePlate}|{vehicle.Status}|{vehicle.MileageKm}|{vehicle.LastMaintenanceDate}"))
               + "#" + records.Count;
    }

    // ---------- Con los datos de demostración ----------

    [Fact]
    public async Task WithTheDemoData_OnlyTheVehiclesWithServicesAreListed_AndTheOverdueOneIsTheOneWithOldServices()
    {
        var fleet = new RealFleet();
        await fleet.SeedAsync();
        var service = new RuleEvaluationService(
            new RuleParser(),
            new MaintenanceRuleScope(fleet.VehicleRepository, fleet.MaintenanceRepository, TimeProvider.System),
            new MonitoringRuleScope(fleet.VehicleRepository));

        var withServices = await service.EvaluateMaintenanceRuleAsync("daysSinceLastService >= 0");
        var old = await service.EvaluateMaintenanceRuleAsync("daysSinceLastService > 90");
        var kmDue = await service.EvaluateMaintenanceRuleAsync("kmSinceLastService >= 2000");

        Assert.Equal(5, withServices.EvaluatedVehicles);
        Assert.Equal(new[] { "CAR-003", "TRK-001" }, Plates(withServices));
        Assert.Equal(new[] { "CAR-003" }, Plates(old));
        Assert.Equal(new[] { "CAR-003" }, Plates(kmDue));
    }

    [Fact]
    public async Task WithTheDemoData_TheMonitoringFilterAgreesWithTheRepository()
    {
        var fleet = new RealFleet();
        await fleet.SeedAsync();
        var service = new RuleEvaluationService(
            new RuleParser(),
            new MaintenanceRuleScope(fleet.VehicleRepository, fleet.MaintenanceRepository, TimeProvider.System),
            new MonitoringRuleScope(fleet.VehicleRepository));

        var available = await service.FilterVehiclesAsync("status = \"Available\"");

        var expected = (await fleet.VehicleRepository.GetAllAsync()).Where(vehicle => vehicle.Status == VehicleStatus.Available).Select(vehicle => vehicle.LicensePlate);
        Assert.Equal(expected.Order(StringComparer.Ordinal), Plates(available));
    }

    // ---------- Abierto/Cerrado: un contexto nuevo sin tocar expresiones ni parser ----------

    [Fact]
    public void ANewContextWithItsOwnVariables_WorksWithTheSameParserAndExpressions()
    {
        var delayed = new DeliveryRoute { Name = "R1", Status = DeliveryRouteStatus.Delayed, DelayMinutes = 30 };
        var onTime = new DeliveryRoute { Name = "R2", Status = DeliveryRouteStatus.InProgress, DelayMinutes = 0 };
        var rule = new RuleParser().Parse("delayMinutes >= 15 AND status = \"Delayed\"", RouteDelayContext.Vocabulary);

        Assert.True(rule.Interpret(new RouteDelayContext(delayed)));
        Assert.False(rule.Interpret(new RouteDelayContext(onTime)));
        Assert.Throws<RuleSyntaxException>(() => new RuleParser().Parse("mileageKm > 5", RouteDelayContext.Vocabulary));
    }

    private sealed class RouteDelayContext : VariableContext
    {
        public static RuleVocabulary Vocabulary { get; } = new(new[] { "delayMinutes", "status" });

        public RouteDelayContext(DeliveryRoute route)
            : base(Table(
                ("delayMinutes", RuleValue.FromNumber(route.DelayMinutes)),
                ("status", RuleValue.FromText(route.Status.ToString()))))
        {
        }
    }

    private sealed class CountingScope : IRuleScope
    {
        public int Loads { get; private set; }

        public RuleVocabulary Vocabulary => RuleTestData.Vocabulary;

        public Task<IReadOnlyList<RuleSubject>> LoadSubjectsAsync()
        {
            Loads++;
            var subject = new RuleSubject(RuleTestData.Vehicle(), new FakeRuleContext().With("a", 1));
            return Task.FromResult<IReadOnlyList<RuleSubject>>(new[] { subject });
        }
    }
}
