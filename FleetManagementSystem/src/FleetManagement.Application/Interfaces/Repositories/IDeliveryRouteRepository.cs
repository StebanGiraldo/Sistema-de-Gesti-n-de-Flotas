using FleetManagement.Domain.Entities;

namespace FleetManagement.Application.Interfaces.Repositories;

public interface IDeliveryRouteRepository
{
    Task<IReadOnlyList<DeliveryRoute>> GetAllAsync();
    Task<DeliveryRoute?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<DeliveryRoute>> GetByDriverIdAsync(Guid driverId);
    Task<DeliveryRoute> AddAsync(DeliveryRoute route);
    Task UpdateAsync(DeliveryRoute route);
}
