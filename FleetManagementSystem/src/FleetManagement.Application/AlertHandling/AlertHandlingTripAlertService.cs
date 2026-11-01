using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;

namespace FleetManagement.Application.AlertHandling;

/// <summary>
/// Pone la cadena de manejadores delante del servicio de alertas: reportar una alerta pasa por la cadena (validar, registrar,
/// clasificar, escalar) y todo lo demás se delega tal cual. Se registra en <c>Program.cs</c> como la implementación de
/// <see cref="ITripAlertService"/>, igual que se hizo con las reglas de asignación: <c>TripAlertService</c>, su Bridge y sus
/// Decorators no cambian.
/// </summary>
public sealed class AlertHandlingTripAlertService : ITripAlertService
{
    private readonly ITripAlertService _inner;
    private readonly IAlertHandler _firstHandler;

    public AlertHandlingTripAlertService(ITripAlertService inner, IAlertHandler firstHandler)
    {
        _inner = inner;
        _firstHandler = firstHandler;
    }

    public async Task<TripAlertDto> CreateAlertAsync(CreateTripAlertRequest request)
    {
        var context = new AlertReportContext(request);
        await _firstHandler.HandleAsync(context);
        return context.Alert;
    }

    public Task<IReadOnlyList<TripAlertDto>> GetAllAsync() => _inner.GetAllAsync();

    public Task<IReadOnlyList<TripAlertDto>> GetByRouteAsync(Guid routeId) => _inner.GetByRouteAsync(routeId);

    public Task ResolveAlertAsync(Guid id) => _inner.ResolveAlertAsync(id);
}
