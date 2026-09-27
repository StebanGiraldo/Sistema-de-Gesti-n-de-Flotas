using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Interfaces.Services;

public interface IDeliveryRouteService
{
    Task<IReadOnlyList<DeliveryRouteDto>> GetAllRoutesAsync();
    Task<DeliveryRouteDto?> GetRouteByIdAsync(Guid id);
    Task<IReadOnlyList<DeliveryRouteDto>> GetRoutesByDriverAsync(Guid driverId);
    Task<DeliveryRouteDto> CreateRouteAsync(CreateDeliveryRouteRequest request);
    Task<DeliveryRouteDto> CreateExpressRouteAsync(CreateExpressRouteRequest request);
    Task<DeliveryRouteDto> DuplicateRouteAsync(Guid routeId, DuplicateRouteRequest request);
    Task UpdateRouteStatusAsync(Guid id, string status);
}
