using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Interfaces.Services;

public interface ITripAlertService
{
    Task<IReadOnlyList<TripAlertDto>> GetAllAsync();
    Task<IReadOnlyList<TripAlertDto>> GetByRouteAsync(Guid routeId);
    Task<TripAlertDto> CreateAlertAsync(CreateTripAlertRequest request);
    Task ResolveAlertAsync(Guid id);
}
