using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;

namespace FleetManagement.Application.Commands;

/// <summary>Crear una ruta. Deshacer = cancelarla (mientras siga planificada). Rehacer = crearla de nuevo con los mismos datos y las mismas reglas.</summary>
public sealed class CreateRouteCommand : IUndoableCommand<DeliveryRouteDto>
{
    private readonly IDeliveryRouteService _routes;
    private readonly IPlannedRouteCanceller _canceller;
    private readonly CreateDeliveryRouteRequest _request;
    private Guid? _createdRouteId;

    public CreateRouteCommand(IDeliveryRouteService routes, IPlannedRouteCanceller canceller, CreateDeliveryRouteRequest request)
    {
        _routes = routes;
        _canceller = canceller;
        _request = request;
    }

    public string Description => $"Crear la ruta '{_request.Name}'";

    public bool HasEffect => _createdRouteId is not null;

    public async Task<DeliveryRouteDto> ExecuteAsync()
    {
        var created = await _routes.CreateRouteAsync(_request);
        _createdRouteId = created.Id;
        return created;
    }

    public Task UndoAsync() => _canceller.CancelAsync(new[] { _createdRouteId ?? throw new InvalidOperationException("La orden todavía no se ejecutó.") });

    public Task RedoAsync() => ExecuteAsync();
}
