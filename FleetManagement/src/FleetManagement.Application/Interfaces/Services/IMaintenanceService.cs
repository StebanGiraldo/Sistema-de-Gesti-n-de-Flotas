using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Interfaces.Services;

public interface IMaintenanceService
{
    Task<IReadOnlyList<MaintenanceRecordDto>> GetAllAsync();
    Task<IReadOnlyList<MaintenanceRecordDto>> GetByVehicleAsync(Guid vehicleId);
    Task<MaintenanceRecordDto> CreateAsync(CreateMaintenanceRecordRequest request);
    Task<IReadOnlyList<MaintenanceDueDto>> GetVehiclesDueForMaintenanceAsync();
}
