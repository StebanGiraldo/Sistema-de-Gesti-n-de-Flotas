using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Facades;

/// <summary>
/// FACADE (patrón estructural): interfaz simplificada para obtener el resumen general de la flota sin que el
/// cliente tenga que conocer ni coordinar los servicios de vehículos, rutas, mantenimiento y alertas.
/// </summary>
public interface IFleetDashboardFacade
{
    Task<FleetSummaryDto> GetSummaryAsync();
}
