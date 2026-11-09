using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;

namespace FleetManagement.Application.Commands;

/// <summary>
/// Aplicar el orden optimizado de las paradas de una ruta. Antes de aplicarlo toma una foto del orden actual; deshacer la
/// restaura (solo si la ruta sigue planificada). Rehacer vuelve a optimizar con las mismas reglas; si la ruta ya no existe,
/// no se puede rehacer.
///
/// También es el CARETAKER del patrón MEMENTO: conserva el recuerdo del orden anterior sin mirar dentro de él (solo lo ve
/// como <see cref="IRouteMemento"/>) y se lo devuelve al Originator (<see cref="IRouteSnapshotService"/>), que es quien lo
/// restaura. Cada ejecución (también cada "rehacer") toma un recuerdo nuevo, así que deshacer siempre vuelve al estado
/// inmediatamente anterior a esa ejecución.
/// </summary>
public sealed class ApplyRouteOptimizationCommand : IUndoableCommand<RouteOptimizationDto>
{
    private readonly IRouteOptimizationService _optimization;
    private readonly IRouteSnapshotService _snapshots;
    private readonly Guid _routeId;
    private IRouteMemento? _before;
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
        IRouteMemento before = await _snapshots.CaptureAsync(_routeId);
        var result = await _optimization.ApplyAsync(_routeId);

        _before = before;
        _applied = result.Applied;
        return result;
    }

    public Task UndoAsync() => _snapshots.RestoreStopOrderAsync(_before ?? throw new InvalidOperationException("La orden todavía no se ejecutó."));

    public async Task RedoAsync()
    {
        try
        {
            await ExecuteAsync();
        }
        catch (KeyNotFoundException)
        {
            // La ruta ya no existe (por ejemplo, se eliminó al deshacer su creación) y no va a volver: reintentar no sirve.
            var route = _before is null ? "La ruta" : $"La ruta '{_before.RouteName}'";
            throw new CommandNotRedoableException($"{route} ya no existe, así que no se puede volver a optimizar.");
        }
    }
}
