using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;

namespace FleetManagement.Application.Commands;

/// <summary>Crear una ruta express (Builder + Director). Deshacer = eliminarla; rehacer = crearla de nuevo con los mismos datos.</summary>
public sealed class CreateExpressRouteCommand : RouteCreationCommand
{
    private readonly IDeliveryRouteService _routes;
    private readonly CreateExpressRouteRequest _request;

    public CreateExpressRouteCommand(IDeliveryRouteService routes, IPlannedRouteDeleter deleter, CreateExpressRouteRequest request)
        : base(deleter)
    {
        _routes = routes;
        _request = request;
    }

    public override string Description => $"Crear la ruta express '{_request.Name}'";

    protected override Task<DeliveryRouteDto> CreateAsync() => _routes.CreateExpressRouteAsync(_request);
}
