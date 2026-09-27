using System.Collections.Concurrent;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Domain.Entities;

namespace FleetManagement.Infrastructure.Persistence;

public class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, AppUser> _users = new();

    public Task<AppUser?> GetByUsernameAsync(string username)
        => Task.FromResult(_users.Values.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));

    public Task<AppUser?> GetByIdAsync(Guid id)
        => Task.FromResult(_users.TryGetValue(id, out var user) ? user : null);

    public Task<AppUser> AddAsync(AppUser user)
    {
        _users[user.Id] = user;
        return Task.FromResult(user);
    }
}
