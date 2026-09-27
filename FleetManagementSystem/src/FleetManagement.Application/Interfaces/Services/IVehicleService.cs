using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Interfaces.Services;

public interface IVehicleService
{
    Task<IReadOnlyList<VehicleDto>> GetAllVehiclesAsync();
    Task<VehicleDto?> GetVehicleByIdAsync(Guid id);
    Task<VehicleDto> CreateVehicleAsync(CreateVehicleRequest request);
    Task<VehicleDto> CloneVehicleAsync(Guid templateVehicleId, CloneVehicleRequest request);
    Task UpdateVehicleStatusAsync(Guid id, UpdateVehicleStatusRequest request);
}
