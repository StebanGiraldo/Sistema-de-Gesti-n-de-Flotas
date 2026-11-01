using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;

namespace FleetManagement.Application.Commands;

/// <summary>
/// Aplicar el orden optimizado de las paradas de una ruta. Antes de aplicarlo toma una foto del orden actual; deshacer la
/// restaura (solo si la ruta sigue planificada). Rehacer vuelve a optimizar con las mismas reglas.
/// </summary>
public sealed class ApplyRouteOptimizationCommand : IUndoableCommand<RouteOptimizationDto>
{
    private readonly IRouteOptimizationService _optimization;
    private readonly IRouteSnapshotService _snapshots;
    private readonly Guid _routeId;
    private RouteSnapshot? _before;
    private bool _applied;

    public ApplyRouteOptimizationCommand(IRouteOptimizationService optimization, IRouteSnapshotService snapshots, Guid routeId)
    {
        _optimization = optimization;
        _snapshots = snapshots;
        _routeId = routeId;
    }

    public string Description => _before is null
        ? "Aplicar el orden optimizado de paradas"
        : $"Aplicar el orden optimizado de paradas a la ruta '{_before.RouteName}'";

    public bool HasEffect => _applied;

    public async Task<RouteOptimizationDto> ExecuteAsync()
    {
        // El estado de la orden solo cambia si todo salió bien: una falla no deja la orden a medias.
        var before = await _snapshots.CaptureAsync(_routeId);
        var result = await _optimization.ApplyAsync(_routeId);

        _before = before;
        _applied = result.Applied;
        return result;
    }

    public Task UndoAsync() => _snapshots.RestoreStopOrderAsync(_before ?? throw new InvalidOperationException("La orden todavía no se ejecutó."));

    public Task RedoAsync() => ExecuteAsync();
}
