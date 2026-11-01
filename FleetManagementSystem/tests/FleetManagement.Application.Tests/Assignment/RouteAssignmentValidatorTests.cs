using FleetManagement.Application.DTOs;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Assignment;

/// <summary>Reglas de asignación (bloque 1): qué rutas nuevas se aceptan y cuándo se puede sacar una ruta a la calle.</summary>
public class RouteAssignmentValidatorTests
{
    // ------------------------------------------------------------------ ruta nueva

    [Fact]
    public async Task NewRoute_WithoutVehicleOrDriver_IsAccepted()
    {
        var kit = new AssignmentKit();

        await kit.Validator.ValidateNewRouteAsync(null, null, new[] { AssignmentKit.Cargo(99999) });
    }

    [Fact]
    public async Task NewRoute_NullCargo_IsTreatedAsEmpty()
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync(capacityKg: 10);

        await kit.Validator.ValidateNewRouteAsync(vehicle.Id, null, null);
    }

    [Fact]
    public async Task NewRoute_CargoThatFits_IsAccepted()
    {
        var kit = new AssignmentKit();
        var van = await kit.AddVehicleAsync("VAN-002", capacityKg: 1500, capacityM3: 10);

        await kit.Validator.ValidateNewRouteAsync(van.Id, null, new[] { AssignmentKit.Cargo(320, 1.8), AssignmentKit.Cargo(45, 0.4) });
    }

    [Fact]
    public async Task NewRoute_CargoExactlyAtCapacity_IsAccepted()
    {
        var kit = new AssignmentKit();
        var van = await kit.AddVehicleAsync(capacityKg: 100, capacityM3: 2);

        await kit.Validator.ValidateNewRouteAsync(van.Id, null, new[] { AssignmentKit.Cargo(60, 1), AssignmentKit.Cargo(40, 1) });
    }

    [Fact]
    public async Task NewRoute_DecimalWeightsAtCapacity_AreNotRejectedByFloatingPointRounding()
    {
        var kit = new AssignmentKit();
        var van = await kit.AddVehicleAsync(capacityKg: 0.3, capacityM3: 0.3);

        // 0.1 + 0.2 vale 0.30000000000000004 en coma flotante: sin tolerancia esta carga "justa" se rechazaría.
        await kit.Validator.ValidateNewRouteAsync(van.Id, null, new[] { AssignmentKit.Cargo(0.1, 0.1), AssignmentKit.Cargo(0.2, 0.2) });
    }

    [Fact]
    public async Task NewRoute_CargoOverWeightCapacity_IsRejectedNamingThePlateAndTheNumbers()
    {
        var kit = new AssignmentKit();
        var moto = await kit.AddVehicleAsync("MOT-004", capacityKg: 30);

        var error = await kit.RejectedNewRouteAsync(moto.Id, null, AssignmentKit.Cargo(5000));

        Assert.Single(error.Violations);
        Assert.Contains("MOT-004", error.Message);
        Assert.Contains("5000 kg", error.Message);
        Assert.Contains("30 kg", error.Message);
    }

    [Fact]
    public async Task NewRoute_WeightIsTheSumOfAllItems()
    {
        var kit = new AssignmentKit();
        var van = await kit.AddVehicleAsync(capacityKg: 100);

        await kit.RejectedNewRouteAsync(van.Id, null, AssignmentKit.Cargo(60), AssignmentKit.Cargo(60));
    }

    [Fact]
    public async Task NewRoute_CargoOverVolumeCapacity_IsRejected()
    {
        var kit = new AssignmentKit();
        var van = await kit.AddVehicleAsync("VAN-002", capacityKg: 1500, capacityM3: 2);

        var error = await kit.RejectedNewRouteAsync(van.Id, null, AssignmentKit.Cargo(10, 2.5));

        Assert.Single(error.Violations);
        Assert.Contains("2.5 m³", error.Message);
        Assert.Contains("2 m³", error.Message);
    }

    [Fact]
    public async Task NewRoute_VolumeIsNotChecked_WhenTheVehicleDeclaresNoVolume()
    {
        var kit = new AssignmentKit();
        var van = await kit.AddVehicleAsync(capacityKg: 1500, capacityM3: null);

        await kit.Validator.ValidateNewRouteAsync(van.Id, null, new[] { AssignmentKit.Cargo(10, 9999) });
    }

    [Fact]
    public async Task NewRoute_UnknownVehicle_IsRejected()
    {
        var kit = new AssignmentKit();

        var error = await kit.RejectedNewRouteAsync(Guid.NewGuid(), null);

        Assert.Contains("vehículo asignado no existe", error.Message);
    }

    [Fact]
    public async Task NewRoute_UnknownDriver_IsRejected()
    {
        var kit = new AssignmentKit();

        var error = await kit.RejectedNewRouteAsync(null, Guid.NewGuid());

        Assert.Contains("conductor asignado no existe", error.Message);
    }

    [Fact]
    public async Task NewRoute_KnownDriver_IsAccepted()
    {
        var kit = new AssignmentKit();
        var driver = await kit.AddDriverAsync();

        await kit.Validator.ValidateNewRouteAsync(null, driver.Id, null);
    }

    [Theory]
    [InlineData(VehicleStatus.Maintenance, "mantenimiento")]
    [InlineData(VehicleStatus.OutOfService, "fuera de servicio")]
    public async Task NewRoute_VehicleThatIsNotOperational_IsRejected(VehicleStatus status, string expectedText)
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync("CAR-003", status);

        var error = await kit.RejectedNewRouteAsync(vehicle.Id, null);

        Assert.Contains("CAR-003", error.Message);
        Assert.Contains(expectedText, error.Message);
    }

    [Theory]
    [InlineData(VehicleStatus.Available)]
    [InlineData(VehicleStatus.EnRoute)]   // se puede planificar el siguiente viaje de un vehículo que todavía está en ruta
    public async Task NewRoute_OperationalVehicle_IsAccepted(VehicleStatus status)
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync(status: status);

        await kit.Validator.ValidateNewRouteAsync(vehicle.Id, null, null);
    }

    [Fact]
    public async Task NewRoute_VehicleWithOverdueMaintenance_IsRejectedNamingTheTask()
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync("TRK-001");
        kit.MarkOverdue(vehicle, "BrakeInspection");

        var error = await kit.RejectedNewRouteAsync(vehicle.Id, null);

        Assert.Contains("TRK-001", error.Message);
        Assert.Contains("mantenimiento vencido", error.Message);
        Assert.Contains("BrakeInspection", error.Message);
    }

    [Fact]
    public async Task NewRoute_MaintenanceThatIsNotOverdueOrBelongsToAnotherVehicle_IsIgnored()
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync("TRK-001");
        var other = await kit.AddVehicleAsync("VAN-002");
        kit.Maintenance.Due.Add(new MaintenanceDueDto(vehicle.Id, "TRK-001", "OilChange", null, null, false));
        kit.MarkOverdue(other);

        await kit.Validator.ValidateNewRouteAsync(vehicle.Id, null, null);
    }

    [Fact]
    public async Task NewRoute_ReportsEveryBrokenRuleAtOnce()
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync("CAR-003", VehicleStatus.Maintenance, capacityKg: 400);

        var error = await kit.RejectedNewRouteAsync(vehicle.Id, Guid.NewGuid(), AssignmentKit.Cargo(900));

        Assert.Equal(3, error.Violations.Count);   // en mantenimiento + no cabe + conductor inexistente
    }

    // ------------------------------------------------------------------ salida a ruta

    [Fact]
    public async Task Start_UnknownRoute_IsAccepted_SoTheServiceCanAnswerNotFound()
    {
        var kit = new AssignmentKit();

        await kit.Validator.ValidateStartAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task Start_PlannedRouteWithAvailableVehicleAndFreeDriver_IsAccepted()
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync();
        var driver = await kit.AddDriverAsync();
        var route = await kit.AddRouteAsync("Ruta A", DeliveryRouteStatus.Planned, vehicle, driver);

        await kit.Validator.ValidateStartAsync(route.Id);
    }

    [Fact]
    public async Task Start_RouteWithoutVehicleOrDriver_IsAccepted()
    {
        var kit = new AssignmentKit();
        var route = await kit.AddRouteAsync("Sin asignar", DeliveryRouteStatus.Planned);

        await kit.Validator.ValidateStartAsync(route.Id);
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.InProgress)]
    [InlineData(DeliveryRouteStatus.Delayed)]
    public async Task Start_RouteThatIsAlreadyActive_IsAcceptedWithoutCheckingAgain(DeliveryRouteStatus active)
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync(status: VehicleStatus.Maintenance);
        var route = await kit.AddRouteAsync("Ya en curso", active, vehicle);

        await kit.Validator.ValidateStartAsync(route.Id);
    }

    [Theory]
    [InlineData(VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService)]
    public async Task Start_VehicleThatIsNotOperational_IsRejected(VehicleStatus status)
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync("CAR-003", status);
        var route = await kit.AddRouteAsync("Ruta A", DeliveryRouteStatus.Planned, vehicle);

        var error = await kit.RejectedStartAsync(route.Id);

        Assert.Contains("CAR-003", error.Message);
    }

    [Fact]
    public async Task Start_VehicleWithOverdueMaintenance_IsRejected()
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync("TRK-001");
        kit.MarkOverdue(vehicle);
        var route = await kit.AddRouteAsync("Ruta A", DeliveryRouteStatus.Planned, vehicle);

        var error = await kit.RejectedStartAsync(route.Id);

        Assert.Contains("mantenimiento vencido", error.Message);
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.InProgress)]
    [InlineData(DeliveryRouteStatus.Delayed)]
    public async Task Start_VehicleAlreadyInAnotherActiveRoute_IsRejectedNamingThatRoute(DeliveryRouteStatus otherStatus)
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync("TRK-001", VehicleStatus.EnRoute);
        await kit.AddRouteAsync("Ruta en curso", otherStatus, vehicle);
        var route = await kit.AddRouteAsync("Ruta nueva", DeliveryRouteStatus.Planned, vehicle);

        var error = await kit.RejectedStartAsync(route.Id);

        Assert.Contains("TRK-001", error.Message);
        Assert.Contains("Ruta en curso", error.Message);
    }

    [Theory]
    [InlineData(DeliveryRouteStatus.Planned)]
    [InlineData(DeliveryRouteStatus.Completed)]
    [InlineData(DeliveryRouteStatus.Cancelled)]
    public async Task Start_OtherRouteOfTheSameVehicleThatIsNotActive_IsNotAConflict(DeliveryRouteStatus otherStatus)
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync();
        await kit.AddRouteAsync("Otra ruta", otherStatus, vehicle);
        var route = await kit.AddRouteAsync("Ruta nueva", DeliveryRouteStatus.Planned, vehicle);

        await kit.Validator.ValidateStartAsync(route.Id);
    }

    [Fact]
    public async Task Start_VehicleMarkedEnRouteButWithoutAnyActiveRoute_IsAccepted()
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync(status: VehicleStatus.EnRoute);   // estado puesto a mano, sin viaje real
        var route = await kit.AddRouteAsync("Ruta A", DeliveryRouteStatus.Planned, vehicle);

        await kit.Validator.ValidateStartAsync(route.Id);
    }

    [Fact]
    public async Task Start_DriverAlreadyInAnotherActiveRoute_IsRejectedNamingDriverAndRoute()
    {
        var kit = new AssignmentKit();
        var driver = await kit.AddDriverAsync("Carlos Ramírez");
        await kit.AddRouteAsync("Ruta en curso", DeliveryRouteStatus.InProgress, driver: driver);
        var route = await kit.AddRouteAsync("Ruta nueva", DeliveryRouteStatus.Planned, driver: driver);

        var error = await kit.RejectedStartAsync(route.Id);

        Assert.Contains("Carlos Ramírez", error.Message);
        Assert.Contains("Ruta en curso", error.Message);
    }

    [Fact]
    public async Task Start_DriverWhoseOtherRoutesAreNotActive_IsAccepted()
    {
        var kit = new AssignmentKit();
        var driver = await kit.AddDriverAsync();
        await kit.AddRouteAsync("Terminada", DeliveryRouteStatus.Completed, driver: driver);
        await kit.AddRouteAsync("Planificada", DeliveryRouteStatus.Planned, driver: driver);
        var route = await kit.AddRouteAsync("Ruta nueva", DeliveryRouteStatus.Planned, driver: driver);

        await kit.Validator.ValidateStartAsync(route.Id);
    }

    [Fact]
    public async Task Start_VehicleAndDriverBothBusy_ReportsBothViolations()
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync();
        var driver = await kit.AddDriverAsync();
        await kit.AddRouteAsync("Ruta en curso", DeliveryRouteStatus.InProgress, vehicle, driver);
        var route = await kit.AddRouteAsync("Ruta nueva", DeliveryRouteStatus.Planned, vehicle, driver);

        var error = await kit.RejectedStartAsync(route.Id);

        Assert.Equal(2, error.Violations.Count);
    }

    [Fact]
    public async Task Start_VehicleThatNoLongerExists_IsRejected()
    {
        var kit = new AssignmentKit();
        var vehicle = await kit.AddVehicleAsync();
        var route = await kit.AddRouteAsync("Ruta A", DeliveryRouteStatus.Planned, vehicle);
        await kit.Vehicles.DeleteAsync(vehicle.Id);

        var error = await kit.RejectedStartAsync(route.Id);

        Assert.Contains("ya no existe", error.Message);
    }
}
