using FleetManagement.Domain.Entities;

namespace FleetManagement.Application.Interfaces.Repositories;

public interface IMaintenanceRepository
{
    Task<IReadOnlyList<MaintenanceRecord>> GetAllAsync();
    Task<IReadOnlyList<MaintenanceRecord>> GetByVehicleIdAsync(Guid vehicleId);
    Task<MaintenanceRecord> AddAsync(MaintenanceRecord record);
}
