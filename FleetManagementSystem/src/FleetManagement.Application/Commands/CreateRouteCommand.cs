using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;

namespace FleetManagement.Application.Commands;

/// <summary>Crear una ruta por el formulario. Deshacer = eliminarla (mientras siga planificada). Rehacer = crearla de nuevo con los mismos datos y las mismas reglas.</summary>
public sealed class CreateRouteCommand : RouteCreationCommand
{
    private readonly IDeliveryRouteService _routes;
    private readonly CreateDeliveryRouteRequest _request;

    public CreateRouteCommand(IDeliveryRouteService routes, IPlannedRouteDeleter deleter, CreateDeliveryRouteRequest request)
        : base(deleter)
    {
        _routes = routes;
        _request = request;
    }

    public override string Description => $"Crear la ruta '{_request.Name}'";

    protected override Task<DeliveryRouteDto> CreateAsync() => _routes.CreateRouteAsync(_request);
}
