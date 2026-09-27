using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;

namespace FleetManagement.Application.Services;

/// <summary>
/// Caso de uso de Integración con sistemas de navegación (requerimiento #4
/// del sistema). Genera un enlace de navegación externa con el origen, las
/// paradas intermedias (en orden) y el destino de la ruta ya cargados, para
/// que el conductor pueda abrirlo directamente en su teléfono y seguir la
/// navegación turn-by-turn con una app real.
///
/// PATRÓN ADAPTER (estructural): este servicio es el CLIENTE del patrón.
/// Antes construía aquí mismo la URL de Google Maps (formato específico de
/// esa API, acoplado directamente). Ahora depende únicamente de
/// <see cref="INavigationProvider"/> (el TARGET) y no conoce si el proveedor
/// real es Google Maps, Waze o HERE Maps: esa traducción vive en
/// <c>FleetManagement.Infrastructure.Navigation.GoogleMapsAdapter</c>, que es
/// intercambiable sin modificar esta clase (Open/Closed + Dependency
/// Inversion). La funcionalidad para el usuario final no cambió: se sigue
/// devolviendo un enlace de Google Maps con las mismas paradas.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IDeliveryRouteRepository _routeRepository;
    private readonly INavigationProvider _navigationProvider;

    public NavigationService(IDeliveryRouteRepository routeRepository, INavigationProvider navigationProvider)
    {
        _routeRepository = routeRepository;
        _navigationProvider = navigationProvider;
    }

    public async Task<NavigationLinkDto> GenerateExternalNavigationLinkAsync(Guid routeId)
    {
        var route = await _routeRepository.GetByIdAsync(routeId)
            ?? throw new KeyNotFoundException("Ruta no encontrada.");

        var orderedWaypoints = route.Waypoints.OrderBy(w => w.Order).ToList();
        var stops = orderedWaypoints
            .Select(w => new WaypointDto(w.Location.Y, w.Location.X, w.Label, w.Order))
            .ToList();

        var externalUrl = await _navigationProvider.GenerateRouteUrlAsync(
            route.Origin.Y, route.Origin.X,
            route.Destination.Y, route.Destination.X,
            stops);

        return new NavigationLinkDto(route.Id, externalUrl, stops);
    }
}
