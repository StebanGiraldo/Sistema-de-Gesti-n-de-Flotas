using FleetManagement.Domain.Entities;

namespace FleetManagement.Application.Interfaces.Repositories;

public interface ITripAlertRepository
{
    Task<IReadOnlyList<TripAlert>> GetAllAsync();
    Task<TripAlert?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<TripAlert>> GetByRouteIdAsync(Guid routeId);
    Task<TripAlert> AddAsync(TripAlert alert);
    Task UpdateAsync(TripAlert alert);
}
