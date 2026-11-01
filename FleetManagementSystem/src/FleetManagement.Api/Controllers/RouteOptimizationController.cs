using FleetManagement.Api.Filters;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Api.Controllers;

/// <summary>
/// Optimización de rutas (requerimiento #2): busca el mejor orden de las paradas de una ruta. GET propone el orden sin
/// cambiar nada; POST lo aplica (solo administradores y solo si la ruta sigue planificada).
/// </summary>
[ApiController]
[Route("api/routes/{routeId:guid}/optimization")]
public class RouteOptimizationController : ControllerBase
{
    private readonly IRouteOptimizationService _optimizationService;

    public RouteOptimizationController(IRouteOptimizationService optimizationService)
    {
        _optimizationService = optimizationService;
    }

    [HttpGet]
    public async Task<ActionResult<RouteOptimizationDto>> Propose(Guid routeId)
    {
        try
        {
            return Ok(await _optimizationService.ProposeAsync(routeId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    [RequireRole(UserRole.Admin)]
    public async Task<ActionResult<RouteOptimizationDto>> Apply(Guid routeId)
    {
        try
        {
            return Ok(await _optimizationService.ApplyAsync(routeId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) // la ruta ya no está planificada
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
