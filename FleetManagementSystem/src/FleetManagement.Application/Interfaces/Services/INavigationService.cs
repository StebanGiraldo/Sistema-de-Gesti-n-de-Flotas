using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Interfaces.Services;

/// <summary>
/// Punto de integración con sistemas de navegación externos (requerimiento
/// #4 del sistema). En este prototipo genera un enlace de Google Maps con la
/// ruta ya cargada (origen, paradas y destino) sin requerir credenciales de
/// API; queda preparado para sustituirse por una integración más profunda
/// (Google Directions API, HERE, Mapbox, etc.) sin tocar el resto de la app.
/// </summary>
public interface INavigationService
{
    Task<NavigationLinkDto> GenerateExternalNavigationLinkAsync(Guid routeId);
}
