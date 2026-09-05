using System.Collections.Concurrent;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Domain.Entities;

namespace FleetManagement.Infrastructure.Persistence;

public class InMemoryTripAlertRepository : ITripAlertRepository
{
    private readonly ConcurrentDictionary<Guid, TripAlert> _alerts = new();

    public Task<IReadOnlyList<TripAlert>> GetAllAsync()
        => Task.FromResult((IReadOnlyList<TripAlert>)_alerts.Values.ToList());

    public Task<TripAlert?> GetByIdAsync(Guid id)
        => Task.FromResult(_alerts.TryGetValue(id, out var alert) ? alert : null);

    public Task<IReadOnlyList<TripAlert>> GetByRouteIdAsync(Guid routeId)
        => Task.FromResult((IReadOnlyList<TripAlert>)_alerts.Values.Where(a => a.RouteId == routeId).ToList());

    public Task<TripAlert> AddAsync(TripAlert alert)
    {
        _alerts[alert.Id] = alert;
        return Task.FromResult(alert);
    }

    public Task UpdateAsync(TripAlert alert)
    {
        _alerts[alert.Id] = alert;
        return Task.CompletedTask;
    }
}
