using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;

namespace FleetManagement.Infrastructure.Persistence;

/// <summary>
/// Carga datos de demostración al iniciar la aplicación para que el
/// prototipo se pueda evaluar de inmediato sin tener que dar de alta cada
/// entidad manualmente por la API. Las coordenadas usadas corresponden a
/// Santander y otras ciudades de Colombia.
/// </summary>
public static class InMemoryDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var vehicleRepo = services.GetRequiredService<IVehicleRepository>();
        var driverRepo = services.GetRequiredService<IDriverRepository>();
        var routeRepo = services.GetRequiredService<IDeliveryRouteRepository>();
        var userRepo = services.GetRequiredService<IUserRepository>();
        var maintenanceRepo = services.GetRequiredService<IMaintenanceRepository>();
        var hasher = services.GetRequiredService<IPasswordHasher>();

        // --- Usuarios ---
        var (adminHash, adminSalt) = hasher.HashPassword("Admin123!");
        var adminUser = new AppUser { Username = "admin", FullName = "Administrador del Sistema", Role = UserRole.Admin, PasswordHash = adminHash, PasswordSalt = adminSalt };
        await userRepo.AddAsync(adminUser);

        var (op1Hash, op1Salt) = hasher.HashPassword("Operador123!");
        var operatorUser1 = new AppUser { Username = "operador1", FullName = "Carlos Ramírez", Role = UserRole.Operator, PasswordHash = op1Hash, PasswordSalt = op1Salt };
        await userRepo.AddAsync(operatorUser1);

        var (op2Hash, op2Salt) = hasher.HashPassword("Operador123!");
        var operatorUser2 = new AppUser { Username = "operador2", FullName = "Laura Gómez", Role = UserRole.Operator, PasswordHash = op2Hash, PasswordSalt = op2Salt };
        await userRepo.AddAsync(operatorUser2);

        // --- Conductores ---
        var driver1 = new Driver { FullName = "Carlos Ramírez", LicenseNumber = "SAN-88231", Phone = "+57 300 555 0101", UserId = operatorUser1.Id };
        var driver2 = new Driver { FullName = "Laura Gómez", LicenseNumber = "SAN-77410", Phone = "+57 300 555 0102", UserId = operatorUser2.Id };
        await driverRepo.AddAsync(driver1);
        await driverRepo.AddAsync(driver2);

        // --- Vehículos (coordenadas de Santander y otras ciudades de Colombia) ---
        var truck = new Vehicle { LicensePlate = "TRK-001", Brand = "Volvo", Model = "FH 460", Year = 2022, Type = VehicleType.Truck, CapacityKg = 8000, Status = VehicleStatus.Available, MileageKm = 45210 };
        truck.UpdateLocation(7.1193, -73.1227); // Bucaramanga

        var van = new Vehicle { LicensePlate = "VAN-002", Brand = "Renault", Model = "Master", Year = 2023, Type = VehicleType.Van, CapacityKg = 1500, Status = VehicleStatus.EnRoute, MileageKm = 18340, AssignedDriverId = driver1.Id };
        van.UpdateLocation(7.0806, -73.1716); // Girón

        var car = new Vehicle { LicensePlate = "CAR-003", Brand = "Chevrolet", Model = "N300", Year = 2021, Type = VehicleType.Car, CapacityKg = 400, Status = VehicleStatus.Maintenance, MileageKm = 62870 };
        car.UpdateLocation(4.7110, -74.0721); // Bogotá

        var moto = new Vehicle { LicensePlate = "MOT-004", Brand = "AKT", Model = "NKD 125", Year = 2023, Type = VehicleType.Motorcycle, CapacityKg = 30, Status = VehicleStatus.Available, MileageKm = 9120, AssignedDriverId = driver2.Id };
        moto.UpdateLocation(6.2442, -75.5812); // Medellín

        var van2 = new Vehicle { LicensePlate = "VAN-005", Brand = "Renault", Model = "Kangoo", Year = 2020, Type = VehicleType.Van, CapacityKg = 1200, Status = VehicleStatus.OutOfService, MileageKm = 98450 };
        van2.UpdateLocation(3.4516, -76.5320); // Cali

        await vehicleRepo.AddAsync(truck);
        await vehicleRepo.AddAsync(van);
        await vehicleRepo.AddAsync(car);
        await vehicleRepo.AddAsync(moto);
        await vehicleRepo.AddAsync(van2);

        // --- Mantenimiento (uno vencido, para demostrar el módulo predictivo) ---
        await maintenanceRepo.AddAsync(new MaintenanceRecord
        {
            VehicleId = car.Id,
            Type = MaintenanceType.OilChange,
            PerformedAt = DateTime.UtcNow.AddMonths(-4),
            NextDueDate = DateTime.UtcNow.AddDays(-10), // vencido intencionalmente
            NextDueMileageKm = car.MileageKm - 500,
            Notes = "Cambio de aceite y filtro realizado en taller autorizado Chevrolet.",
            MileageAtServiceKm = 60000
        });

        await maintenanceRepo.AddAsync(new MaintenanceRecord
        {
            VehicleId = truck.Id,
            Type = MaintenanceType.BrakeInspection,
            PerformedAt = DateTime.UtcNow.AddMonths(-1),
            NextDueDate = DateTime.UtcNow.AddMonths(2),
            NextDueMileageKm = truck.MileageKm + 15000,
            Notes = "Inspección de frenos sin novedad.",
            MileageAtServiceKm = 45210
        });

        // --- Ruta activa de ejemplo: Girón -> Bucaramanga ---
        var route = new DeliveryRoute
        {
            Name = "Ruta Girón - Bucaramanga Centro",
            Origin = new Point(-73.1716, 7.0806) { SRID = 4326 },
            Destination = new Point(-73.1227, 7.1193) { SRID = 4326 },
            AssignedVehicleId = van.Id,
            AssignedDriverId = driver1.Id,
            Status = DeliveryRouteStatus.InProgress,
            EstimatedDistanceKm = 9.5,
            EstimatedDurationMinutes = 22,
            ScheduledDate = DateTime.UtcNow
        };
        route.Waypoints.Add(new Waypoint { Order = 0, Label = "Terminal de carga Girón", Location = new Point(-73.1690, 7.0850) { SRID = 4326 } });
        route.Waypoints.Add(new Waypoint { Order = 1, Label = "Puente La Flora", Location = new Point(-73.1450, 7.1050) { SRID = 4326 } });
        route.CargoItems.Add(new CargoItem { Description = "Repuestos industriales", WeightKg = 320, VolumeM3 = 1.8, Priority = CargoPriority.High });
        route.CargoItems.Add(new CargoItem { Description = "Insumos médicos", WeightKg = 45, VolumeM3 = 0.4, Priority = CargoPriority.Urgent });
        await routeRepo.AddAsync(route);

        // --- Ruta planificada de ejemplo: Bucaramanga -> Bogotá (para la motocicleta/Laura) ---
        var route2 = new DeliveryRoute
        {
            Name = "Ruta Bucaramanga - Bogotá",
            Origin = new Point(-73.1227, 7.1193) { SRID = 4326 },
            Destination = new Point(-74.0721, 4.7110) { SRID = 4326 },
            AssignedDriverId = driver2.Id,
            Status = DeliveryRouteStatus.Planned,
            EstimatedDistanceKm = 385,
            EstimatedDurationMinutes = 480,
            ScheduledDate = DateTime.UtcNow.AddDays(1)
        };
        route2.Waypoints.Add(new Waypoint { Order = 0, Label = "Tunja (parada técnica)", Location = new Point(-73.3567, 5.5353) { SRID = 4326 } });
        route2.CargoItems.Add(new CargoItem { Description = "Documentación legal", WeightKg = 5, VolumeM3 = 0.05, Priority = CargoPriority.Standard });
        await routeRepo.AddAsync(route2);
    }
}
