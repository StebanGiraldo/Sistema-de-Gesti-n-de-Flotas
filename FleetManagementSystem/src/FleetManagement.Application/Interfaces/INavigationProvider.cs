using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Interfaces;

/// <summary>
/// TARGET (patrón estructural ADAPTER).
///
/// Interfaz que <see cref="Services.NavigationService"/> (el Cliente) espera
/// para generar un enlace de navegación externo, sin conocer qué proveedor
/// concreto la implementa (Google Maps, Waze, HERE Maps, ...).
///
/// Esta interfaz no impone ninguna dependencia hacia un SDK externo: eso es
/// exactamente lo que permite que <c>NavigationService</c> dependa de esta
/// abstracción (DIP) en lugar de depender de Google Maps directamente.
/// Hoy la única implementación es
/// <c>FleetManagement.Infrastructure.Navigation.GoogleMapsAdapter</c>, que
/// traduce esta interfaz a la de <c>GoogleMapsApi</c> (el Adaptee). Agregar
/// un proveedor nuevo (WazeAdapter, HereMapsAdapter) sólo requiere una clase
/// más que implemente <see cref="INavigationProvider"/> y un cambio de una
/// línea en el registro de Dependency Injection: nunca hay que tocar
/// <c>NavigationService</c> (Principio Abierto/Cerrado).
/// </summary>
public interface INavigationProvider
{
    /// <summary>
    /// Genera la URL de navegación externa para una ruta, dados el origen,
    /// el destino y las paradas intermedias ya en el orden en que deben
    /// visitarse (<see cref="WaypointDto.Order"/>).
    /// </summary>
    Task<string> GenerateRouteUrlAsync(
        double originLat,
        double originLng,
        double destinationLat,
        double destinationLng,
        IReadOnlyList<WaypointDto> waypoints);
}
