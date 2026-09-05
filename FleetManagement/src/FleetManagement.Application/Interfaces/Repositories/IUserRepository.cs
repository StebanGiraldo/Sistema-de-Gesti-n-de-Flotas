using FleetManagement.Domain.Entities;

namespace FleetManagement.Application.Interfaces.Repositories;

public interface IUserRepository
{
    Task<AppUser?> GetByUsernameAsync(string username);
    Task<AppUser?> GetByIdAsync(Guid id);
    Task<AppUser> AddAsync(AppUser user);
}
