using System.Collections.Concurrent;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Domain.Entities;

namespace FleetManagement.Infrastructure.Persistence;

public class InMemoryDriverRepository : IDriverRepository
{
    private readonly ConcurrentDictionary<Guid, Driver> _drivers = new();

    public Task<IReadOnlyList<Driver>> GetAllAsync()
        => Task.FromResult((IReadOnlyList<Driver>)_drivers.Values.OrderBy(d => d.FullName).ToList());

    public Task<Driver?> GetByIdAsync(Guid id)
        => Task.FromResult(_drivers.TryGetValue(id, out var driver) ? driver : null);

    public Task<Driver?> GetByUserIdAsync(Guid userId)
        => Task.FromResult(_drivers.Values.FirstOrDefault(d => d.UserId == userId));

    public Task<Driver> AddAsync(Driver driver)
    {
        _drivers[driver.Id] = driver;
        return Task.FromResult(driver);
    }

    public Task UpdateAsync(Driver driver)
    {
        _drivers[driver.Id] = driver;
        return Task.CompletedTask;
    }
}
