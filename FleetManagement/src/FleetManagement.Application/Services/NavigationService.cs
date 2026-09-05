using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;

namespace FleetManagement.Application.Services;

/// <summary>
/// Caso de uso de Integración con sistemas de navegación (requerimiento #4
/// del sistema). Genera un enlace de Google Maps con el origen, las paradas
/// intermedias (en orden) y el destino de la ruta ya cargados, para que el
/// conductor pueda abrirlo directamente en su teléfono y seguir la
/// navegación turn-by-turn con una app real, sin necesidad de credenciales
/// de API para este prototipo.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IDeliveryRouteRepository _routeRepository;

    public NavigationService(IDeliveryRouteRepository routeRepository)
    {
        _routeRepository = routeRepository;
    }

    public async Task<NavigationLinkDto> GenerateExternalNavigationLinkAsync(Guid routeId)
    {
        var route = await _routeRepository.GetByIdAsync(routeId)
            ?? throw new KeyNotFoundException("Ruta no encontrada.");

        var origin = $"{route.Origin.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)},{route.Origin.X.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        var destination = $"{route.Destination.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)},{route.Destination.X.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        var orderedWaypoints = route.Waypoints.OrderBy(w => w.Order).ToList();
        var waypointsParam = string.Join("|", orderedWaypoints.Select(w =>
            $"{w.Location.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)},{w.Location.X.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));

        var url = $"https://www.google.com/maps/dir/?api=1&origin={Uri.EscapeDataString(origin)}&destination={Uri.EscapeDataString(destination)}&travelmode=driving";
        if (!string.IsNullOrEmpty(waypointsParam))
            url += $"&waypoints={Uri.EscapeDataString(waypointsParam)}";

        var stops = orderedWaypoints.Select(w => new WaypointDto(w.Location.Y, w.Location.X, w.Label, w.Order)).ToList();

        return new NavigationLinkDto(route.Id, url, stops);
    }
}
