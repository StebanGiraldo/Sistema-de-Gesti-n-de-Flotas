using System.Collections.Concurrent;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Domain.Entities;

namespace FleetManagement.Infrastructure.Persistence;

public class InMemoryDeliveryRouteRepository : IDeliveryRouteRepository
{
    private readonly ConcurrentDictionary<Guid, DeliveryRoute> _routes = new();

    public Task<IReadOnlyList<DeliveryRoute>> GetAllAsync()
        => Task.FromResult((IReadOnlyList<DeliveryRoute>)_routes.Values.OrderByDescending(r => r.CreatedAt).ToList());

    public Task<DeliveryRoute?> GetByIdAsync(Guid id)
        => Task.FromResult(_routes.TryGetValue(id, out var route) ? route : null);

    public Task<IReadOnlyList<DeliveryRoute>> GetByDriverIdAsync(Guid driverId)
        => Task.FromResult((IReadOnlyList<DeliveryRoute>)_routes.Values.Where(r => r.AssignedDriverId == driverId).ToList());

    public Task<DeliveryRoute> AddAsync(DeliveryRoute route)
    {
        _routes[route.Id] = route;
        return Task.FromResult(route);
    }

    public Task UpdateAsync(DeliveryRoute route)
    {
        _routes[route.Id] = route;
        return Task.CompletedTask;
    }
}
