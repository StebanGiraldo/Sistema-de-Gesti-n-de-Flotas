using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Services;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Commands;

/// <summary>
/// Cancela rutas por el servicio de rutas (el protegido por las reglas de asignación), así que el vehículo, la auditoría y
/// todo lo demás quedan como cuando se cancela una ruta a mano. Primero comprueba todas; si alguna ya salió o se cerró, no
/// cancela ninguna y lo explica.
/// </summary>
public sealed class PlannedRouteCanceller : IPlannedRouteCanceller
{
    private readonly IDeliveryRouteService _routes;

    public PlannedRouteCanceller(IDeliveryRouteService routes)
    {
        _routes = routes;
    }

    public async Task CancelAsync(IReadOnlyCollection<Guid> routeIds)
    {
        var routes = new List<DeliveryRouteDto>();
        foreach (var id in routeIds)
            routes.Add(await _routes.GetRouteByIdAsync(id) ?? throw new CommandNotUndoableException("Una de las rutas creadas ya no existe."));

        var moved = routes.Where(route => route.Status != nameof(DeliveryRouteStatus.Planned)).ToList();
        if (moved.Count > 0)
            throw new CommandNotUndoableException(
                $"No se puede deshacer: {string.Join(", ", moved.Select(route => $"la ruta '{route.Name}' ya no está planificada (está {Describe(route.Status)})"))}. No se canceló ninguna.");

        foreach (var route in routes)
            await _routes.UpdateRouteStatusAsync(route.Id, nameof(DeliveryRouteStatus.Cancelled));
    }

    // El DTO trae el estado como texto del enum; al usuario se le habla en español, como en el resto de los mensajes.
    private static string Describe(string status)
        => Enum.TryParse<DeliveryRouteStatus>(status, out var parsed) ? RouteStatusText.Describe(parsed) : status;
}
