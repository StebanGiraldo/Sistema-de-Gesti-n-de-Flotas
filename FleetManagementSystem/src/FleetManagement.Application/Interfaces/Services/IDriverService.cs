using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Interfaces.Services;

public interface IDriverService
{
    Task<IReadOnlyList<DriverDto>> GetAllDriversAsync();
}
