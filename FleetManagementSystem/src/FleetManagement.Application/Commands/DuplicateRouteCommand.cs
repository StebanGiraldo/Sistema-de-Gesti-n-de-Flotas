using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;

namespace FleetManagement.Application.Commands;

/// <summary>
/// Duplicar una ruta (Prototype). Deshacer = eliminar la copia (la ruta original no se toca); rehacer = duplicar otra vez la
/// ruta original, lo que produce una copia NUEVA. Si la original ya no existe, no se puede rehacer: se explica y la orden
/// se quita del historial.
/// </summary>
public sealed class DuplicateRouteCommand : RouteCreationCommand
{
    private readonly IDeliveryRouteService _routes;
    private readonly Guid _sourceRouteId;
    private readonly DuplicateRouteRequest _request;
    private string? _copyName;

    public DuplicateRouteCommand(IDeliveryRouteService routes, IPlannedRouteDeleter deleter, Guid sourceRouteId, DuplicateRouteRequest request)
        : base(deleter)
    {
        _routes = routes;
        _sourceRouteId = sourceRouteId;
        _request = request;
    }

    public override string Description => _copyName is null ? "Duplicar una ruta" : $"Duplicar la ruta como '{_copyName}'";

    public override async Task RedoAsync()
    {
        try
        {
            await base.RedoAsync();
        }
        catch (KeyNotFoundException)
        {
            // La original ya no existe (por ejemplo, se eliminó al deshacer su creación) y no va a volver: reintentar no sirve.
            throw new CommandNotRedoableException("La ruta que se había duplicado ya no existe, así que no se puede volver a duplicar.");
        }
    }

    protected override async Task<DeliveryRouteDto> CreateAsync()
    {
        var copy = await _routes.DuplicateRouteAsync(_sourceRouteId, _request);
        _copyName = copy.Name;
        return copy;
    }
}
