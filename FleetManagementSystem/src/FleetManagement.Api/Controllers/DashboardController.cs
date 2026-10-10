using FleetManagement.Application.DTOs;
using FleetManagement.Application.Facades;
using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Api.Controllers;

/// <summary>
/// Resumen general de la flota para el dashboard (patrón Facade). El controlador no coordina servicios: le pide
/// el resumen a la fachada, que es quien conoce los subsistemas.
/// </summary>
[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IFleetDashboardFacade _dashboardFacade;

    public DashboardController(IFleetDashboardFacade dashboardFacade)
    {
        _dashboardFacade = dashboardFacade;
    }

    /// <summary>Estadísticas de vehículos y rutas, carga registrada, mantenimientos vencidos y alertas por estado.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<FleetSummaryDto>> GetSummary()
        => Ok(await _dashboardFacade.GetSummaryAsync());
}
