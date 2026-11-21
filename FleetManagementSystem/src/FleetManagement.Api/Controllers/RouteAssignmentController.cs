using FleetManagement.Api.Filters;
using FleetManagement.Application.Assignment;
using FleetManagement.Application.DTOs;
using FleetManagement.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Api.Controllers;

/// <summary>
/// Asignación de conductor y/o vehículo a una ruta ya creada. Va en un controlador propio (misma ruta base
/// <c>api/routes</c>) para no cambiar el de rutas ni su servicio.
/// </summary>
[ApiController]
[Route("api/routes/{id:guid}")]
[RequireRole(UserRole.Admin)]
public class RouteAssignmentController : ControllerBase
{
    private readonly IRouteAssignmentService _assignment;

    public RouteAssignmentController(IRouteAssignmentService assignment)
    {
        _assignment = assignment;
    }

    /// <summary>Conductores y vehículos que hoy se pueden asignar a esta ruta.</summary>
    [HttpGet("assignment-options")]
    public async Task<ActionResult<RouteAssignmentOptionsDto>> GetOptions(Guid id)
    {
        try
        {
            return Ok(await _assignment.GetOptionsAsync(id));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Asigna a la ruta el conductor y/o el vehículo que le faltan.</summary>
    [HttpPatch("assignment")]
    public async Task<ActionResult<DeliveryRouteDto>> Assign(Guid id, [FromBody] AssignRouteRequest request)
    {
        try
        {
            return Ok(await _assignment.AssignAsync(id, request));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) // regla de asignación incumplida (AssignmentRuleException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
