using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Services;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Commands;

/// <summary>
/// Elimina las rutas que creó una orden. Primero comprueba todas: si alguna ya no existe, salió o se cerró, no elimina
/// ninguna y lo explica (una ruta que ya salió tiene historia y no se borra). Una ruta planificada no tiene nada que
/// liberar —crearla no tocó al vehículo ni al conductor—, así que eliminarla tampoco los toca. La eliminación queda en la
/// auditoría, que es un registro aparte y se conserva.
/// </summary>
public sealed class PlannedRouteDeleter : IPlannedRouteDeleter
{
    private const string AuditCategory = "Ruta";

    private readonly IDeliveryRouteRepository _routes;
    private readonly IFleetAuditLogger _audit;

    public PlannedRouteDeleter(IDeliveryRouteRepository routes, IFleetAuditLogger audit)
    {
        _routes = routes;
        _audit = audit;
    }

    public async Task DeleteAsync(IReadOnlyCollection<Guid> routeIds)
    {
        var routes = new List<DeliveryRoute>();
        foreach (var id in routeIds)
            routes.Add(await _routes.GetByIdAsync(id) ?? throw new CommandNotUndoableException("Una de las rutas creadas ya no existe."));

        var moved = routes.Where(route => route.Status != DeliveryRouteStatus.Planned).ToList();
        if (moved.Count > 0)
            throw new CommandNotUndoableException(
                $"No se puede deshacer: {string.Join(", ", moved.Select(route => $"la ruta '{route.Name}' ya no está planificada (está {RouteStatusText.Describe(route.Status)})"))}. No se eliminó ninguna.");

        foreach (var route in routes)
        {
            await _routes.DeleteAsync(route.Id);
            _audit.LogEvent(AuditCategory, $"Ruta '{route.Name}' eliminada al deshacer su creación.");
        }
    }
}
