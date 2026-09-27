using FleetManagement.Domain.Entities;

namespace FleetManagement.Application.Interfaces.Repositories;

public interface IDriverRepository
{
    Task<IReadOnlyList<Driver>> GetAllAsync();
    Task<Driver?> GetByIdAsync(Guid id);
    Task<Driver?> GetByUserIdAsync(Guid userId);
    Task<Driver> AddAsync(Driver driver);
    Task UpdateAsync(Driver driver);
}
