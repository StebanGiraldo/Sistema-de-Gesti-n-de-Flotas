using FleetManagement.Api.Filters;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Api.Controllers;

/// <summary>
/// Alertas de viaje. Permite a un operador reportar una complicación
/// durante su recorrido (retraso, avería, tráfico, accidente, clima) desde
/// el portal de operador, tal como pide el enunciado.
/// </summary>
[ApiController]
[Route("api/alerts")]
public class TripAlertsController : ControllerBase
{
    private readonly ITripAlertService _alertService;

    public TripAlertsController(ITripAlertService alertService)
    {
        _alertService = alertService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TripAlertDto>>> GetAll()
        => Ok(await _alertService.GetAllAsync());

    [HttpGet("route/{routeId:guid}")]
    public async Task<ActionResult<IReadOnlyList<TripAlertDto>>> GetByRoute(Guid routeId)
        => Ok(await _alertService.GetByRouteAsync(routeId));

    /// <summary>Reporta una nueva alerta/complicación sobre una ruta en curso. Disponible para operadores.</summary>
    [HttpPost]
    public async Task<ActionResult<TripAlertDto>> Create([FromBody] CreateTripAlertRequest request)
    {
        try
        {
            var alert = await _alertService.CreateAlertAsync(request);
            return Ok(alert);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/resolve")]
    [RequireRole(UserRole.Admin)]
    public async Task<IActionResult> Resolve(Guid id)
    {
        try
        {
            await _alertService.ResolveAlertAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
