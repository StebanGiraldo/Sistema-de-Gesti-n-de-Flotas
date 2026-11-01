using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Interfaces.Services;

/// <summary>Reordena las paradas de una ruta existente para que se recorra lo menos posible.</summary>
public interface IRouteOptimizationService
{
    /// <summary>Calcula el mejor orden de las paradas SIN guardar nada.</summary>
    /// <exception cref="KeyNotFoundException">Si la ruta no existe.</exception>
    Task<RouteOptimizationDto> ProposeAsync(Guid routeId);

    /// <summary>Guarda el mejor orden de las paradas (si mejora el actual) y actualiza la distancia y el tiempo estimados.</summary>
    /// <exception cref="KeyNotFoundException">Si la ruta no existe.</exception>
    /// <exception cref="InvalidOperationException">Si la ruta ya no está planificada: una ruta en curso no se reordena.</exception>
    Task<RouteOptimizationDto> ApplyAsync(Guid routeId);
}
