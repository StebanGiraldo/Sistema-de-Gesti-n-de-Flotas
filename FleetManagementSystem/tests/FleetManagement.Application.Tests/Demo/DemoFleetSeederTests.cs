using FleetManagement.Application.Assignment;
using FleetManagement.Application.DTOs;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Persistence.Demo;
using Xunit;

namespace FleetManagement.Application.Tests.Demo;

/// <summary>
/// Se siembran los datos de demostración reales y se comprueba, con los repositorios y servicios reales, lo que el documento
/// de cumplimiento afirma: cuentas que sirven, 35 vehículos, al menos 20 con ruta y carga, mantenimiento vencido solo donde se
/// quiere, y reglas de asignación que aceptan y rechazan lo esperado.
/// </summary>
public class DemoFleetSeederTests
{
    private static bool IsActive(DeliveryRoute route) => route.Status is DeliveryRouteStatus.InProgress or DeliveryRouteStatus.Delayed;

    private static CargoItemDto Cargo(double kg, double m3 = 0.1) => new("Carga de prueba", kg, m3, "Standard");

    private static async Task<Vehicle> VehicleAsync(SeededDemoWorld world, string plate)
        => await world.VehicleRepository.GetByLicensePlateAsync(plate) ?? throw new InvalidOperationException($"Falta {plate}");

    // ---------- Cuentas ----------

    [Fact]
    public async Task TwelveOperatorAccounts_CanLogInWithTheSamePassword_AndEachOneHasADriver()
    {
        var world = await SeededDemoWorld.CreateAsync();
        var usernames = Enumerable.Range(1, 12).Select(n => $"operador{n}").ToList();

        foreach (var username in usernames)
        {
            var user = await world.UserRepository.GetByUsernameAsync(username);

            Assert.NotNull(user);
            Assert.Equal(UserRole.Operator, user!.Role);
            Assert.True(world.Hasher.VerifyPassword(DemoOperators.Password, user.PasswordHash, user.PasswordSalt), username);
            Assert.NotNull(await world.DriverRepository.GetByUserIdAsync(user.Id));
        }
    }

    [Fact]
    public async Task TheOriginalAccountsAndDataAreUntouched()
    {
        var world = await SeededDemoWorld.CreateAsync();

        Assert.NotNull(await world.UserRepository.GetByUsernameAsync("admin"));
        Assert.Equal(VehicleStatus.EnRoute, (await VehicleAsync(world, "VAN-002")).Status);
        Assert.Equal(VehicleStatus.Maintenance, (await VehicleAsync(world, "CAR-003")).Status);
        Assert.Contains(await world.RouteRepository.GetAllAsync(), route => route.Name == "Ruta Girón - Bucaramanga Centro" && IsActive(route));
    }

    // ---------- Vehículos y rutas con carga ----------

    [Fact]
    public async Task ThirtyFiveVehicles_AndTwentyFourRoutes()
    {
        var world = await SeededDemoWorld.CreateAsync();

        Assert.Equal(35, (await world.VehicleRepository.GetAllAsync()).Count);
        Assert.Equal(24, (await world.RouteRepository.GetAllAsync()).Count);
    }

    [Fact]
    public async Task AtLeastTwentyVehicles_HaveARouteWithCargo()
    {
        var world = await SeededDemoWorld.CreateAsync();
        var vehicleIds = (await world.VehicleRepository.GetAllAsync()).Select(vehicle => vehicle.Id).ToHashSet();

        var withCargo = (await world.RouteRepository.GetAllAsync())
            .Where(route => route.AssignedVehicleId is { } id && vehicleIds.Contains(id) && route.CargoItems.Count > 0)
            .Select(route => route.AssignedVehicleId)
            .Distinct()
            .Count();

        Assert.True(withCargo >= 20, $"Solo {withCargo} vehículos tienen ruta con carga.");
    }

    [Fact]
    public async Task ElevenTripsAreActive_EachWithItsOwnVehicleEnRouteAndItsOwnDriver()
    {
        var world = await SeededDemoWorld.CreateAsync();
        var active = (await world.RouteRepository.GetAllAsync()).Where(IsActive).ToList();
        var vehiclesEnRoute = (await world.VehicleRepository.GetAllAsync()).Where(vehicle => vehicle.Status == VehicleStatus.EnRoute).ToList();

        Assert.Equal(11, active.Count);
        Assert.Equal(11, active.Select(route => route.AssignedVehicleId).Distinct().Count());
        Assert.Equal(11, active.Select(route => route.AssignedDriverId).Distinct().Count());
        Assert.Equal(11, vehiclesEnRoute.Count);
        Assert.All(vehiclesEnRoute, vehicle => Assert.Contains(active, route => route.AssignedVehicleId == vehicle.Id));
    }

    [Fact]
    public async Task EveryRoute_CarriesAtMostWhatItsVehicleCanTake()
    {
        var world = await SeededDemoWorld.CreateAsync();

        // La ruta original Bucaramanga - Bogotá aún no tiene vehículo asignado: no hay nada contra qué comparar su carga.
        foreach (var route in (await world.RouteRepository.GetAllAsync()).Where(route => route.AssignedVehicleId is not null))
        {
            var vehicle = await world.VehicleRepository.GetByIdAsync(route.AssignedVehicleId!.Value);

            Assert.True(route.CargoItems.Sum(item => item.WeightKg) <= vehicle!.CapacityKg + 1e-9, route.Name);
            Assert.True(route.CargoItems.Sum(item => item.VolumeM3) <= (vehicle.CapacityM3 ?? double.MaxValue) + 1e-9, route.Name);
        }
    }

    [Fact]
    public async Task AnOperatorWithATripAndANextTrip_SeesBothInThePortal()
    {
        var world = await SeededDemoWorld.CreateAsync();
        var user = await world.UserRepository.GetByUsernameAsync("operador3");
        var driver = await world.DriverRepository.GetByUserIdAsync(user!.Id);

        var routes = await world.RouteRepository.GetByDriverIdAsync(driver!.Id);

        Assert.Equal(new[] { DeliveryRouteStatus.Planned, DeliveryRouteStatus.InProgress }, routes.Select(route => route.Status).Order());
    }

    // ---------- Mantenimiento ----------

    [Fact]
    public async Task OnlyThreeVehiclesAreOverdue_TheOriginalCar_AndTheTwoNewCars()
    {
        var world = await SeededDemoWorld.CreateAsync();

        var overdue = (await world.Maintenance.GetVehiclesDueForMaintenanceAsync()).Where(due => due.IsOverdue).Select(due => due.VehiclePlate).Distinct().Order();

        Assert.Equal(new[] { "CAR-003", "CAR-028", "CAR-029" }, overdue);
    }

    // ---------- Reglas de asignación con la flota ampliada ----------

    [Theory]
    [InlineData("TRK-013", 4000)]
    [InlineData("TRK-014", 4000)]
    [InlineData("VAN-024", 800)]
    [InlineData("MOT-033", 20)]
    [InlineData("MOT-034", 20)]
    public async Task TheSpareAvailableVehicles_AcceptACargoThatFits(string plate, double kg)
    {
        var world = await SeededDemoWorld.CreateAsync();
        var vehicle = await VehicleAsync(world, plate);

        await world.Validator.ValidateNewRouteAsync(vehicle.Id, null, new[] { Cargo(kg) });
    }

    [Fact]
    public async Task ACargoHeavierThanTheSpareMotorcycle_IsRejected()
    {
        var world = await SeededDemoWorld.CreateAsync();
        var moto = await VehicleAsync(world, "MOT-033");

        var error = await Assert.ThrowsAsync<AssignmentRuleException>(() => world.Validator.ValidateNewRouteAsync(moto.Id, null, new[] { Cargo(500) }));

        Assert.Contains("supera la capacidad", error.Message);
    }

    [Fact]
    public async Task TheCarWithOverdueMaintenance_IsRejected_EvenThoughItIsAvailable()
    {
        var world = await SeededDemoWorld.CreateAsync();
        var car = await VehicleAsync(world, "CAR-029");

        var error = await Assert.ThrowsAsync<AssignmentRuleException>(() => world.Validator.ValidateNewRouteAsync(car.Id, null, new[] { Cargo(50) }));

        Assert.Equal(VehicleStatus.Available, car.Status);
        Assert.Contains("mantenimiento vencido", error.Message);
    }

    [Theory]
    [InlineData("CAR-028")]
    [InlineData("MOT-035")]
    public async Task TheVehicleInTheWorkshopAndTheOneOutOfService_AreRejected(string plate)
    {
        var world = await SeededDemoWorld.CreateAsync();
        var vehicle = await VehicleAsync(world, plate);

        await Assert.ThrowsAsync<AssignmentRuleException>(() => world.Validator.ValidateNewRouteAsync(vehicle.Id, null, new[] { Cargo(10, 0.05) }));
    }

    [Fact]
    public async Task EveryPlannedRoute_PassesTheAssignmentRulesWithItsOwnCargo()
    {
        var world = await SeededDemoWorld.CreateAsync();

        foreach (var spec in DemoPlannedRoutes.All)
        {
            var vehicle = await VehicleAsync(world, spec.Plate);
            var cargo = spec.Cargo.Select(item => new CargoItemDto(item.Description, item.WeightKg, item.VolumeM3, item.Priority.ToString())).ToList();

            await world.Validator.ValidateNewRouteAsync(vehicle.Id, null, cargo);
        }
    }
}
