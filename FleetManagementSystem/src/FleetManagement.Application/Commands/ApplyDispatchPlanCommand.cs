using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;

namespace FleetManagement.Application.Commands;

/// <summary>
/// Aplicar un plan de reparto (crea una ruta planificada por viaje). Deshacer ELIMINA las rutas que creó, todas o ninguna,
/// y solo si siguen planificadas. Rehacer vuelve a aplicar el plan con la flota de ese momento: puede dar otro reparto o,
/// si ya no hay vehículos, fallar con el motivo.
/// </summary>
public sealed class ApplyDispatchPlanCommand : IUndoableCommand<DispatchApplyResultDto>
{
    private readonly IDispatchPlanningService _dispatch;
    private readonly IPlannedRouteDeleter _deleter;
    private readonly DispatchPlanRequest _request;
    private IReadOnlyList<Guid> _createdRouteIds = Array.Empty<Guid>();

    public ApplyDispatchPlanCommand(IDispatchPlanningService dispatch, IPlannedRouteDeleter deleter, DispatchPlanRequest request)
    {
        _dispatch = dispatch;
        _deleter = deleter;
        _request = request;
    }

    public string Description => _createdRouteIds.Count == 0
        ? "Aplicar el plan de reparto"
        : $"Aplicar el plan de reparto ({_createdRouteIds.Count} ruta(s) creada(s))";

    public bool HasEffect => _createdRouteIds.Count > 0;

    public async Task<DispatchApplyResultDto> ExecuteAsync()
    {
        // Igual que en las demás órdenes: si falla, lo que la orden recuerda sigue siendo lo de la última vez que salió bien.
        var result = await _dispatch.ApplyAsync(_request);

        _createdRouteIds = result.CreatedRoutes.Select(route => route.Id).ToList();
        return result;
    }

    public Task UndoAsync() => _deleter.DeleteAsync(_createdRouteIds.ToList());

    public Task RedoAsync() => ExecuteAsync();
}
