using FleetManagement.Domain.Entities;

namespace FleetManagement.Application.Interfaces.Repositories;

/// <summary>
/// Abstracción de persistencia para Vehicle (Principio de Inversión de
/// Dependencias - DIP). La capa Application programa contra esta interfaz;
/// la implementación concreta (en memoria hoy, EF Core + PostgreSQL/PostGIS
/// mañana) vive en FleetManagement.Infrastructure y se inyecta vía DI.
/// </summary>
public interface IVehicleRepository
{
    Task<IReadOnlyList<Vehicle>> GetAllAsync();
    Task<Vehicle?> GetByIdAsync(Guid id);
    Task<Vehicle?> GetByLicensePlateAsync(string licensePlate);
    Task<Vehicle> AddAsync(Vehicle vehicle);
    Task UpdateAsync(Vehicle vehicle);
    Task DeleteAsync(Guid id);
}
