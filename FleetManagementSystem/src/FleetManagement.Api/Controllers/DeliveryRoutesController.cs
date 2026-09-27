using FleetManagement.Api.Filters;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Api.Controllers;

/// <summary>
/// Optimización de rutas y asignación de cargas (requerimiento #2). Expone
/// la creación de rutas mediante el patrón Builder, la duplicación de rutas
/// recurrentes mediante Prototype, y la transición de estados de una ruta.
/// </summary>
[ApiController]
[Route("api/routes")]
public class DeliveryRoutesController : ControllerBase
{
    private readonly IDeliveryRouteService _routeService;

    public DeliveryRoutesController(IDeliveryRouteService routeService)
    {
        _routeService = routeService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeliveryRouteDto>>> GetAll()
        => Ok(await _routeService.GetAllRoutesAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DeliveryRouteDto>> GetById(Guid id)
    {
        var route = await _routeService.GetRouteByIdAsync(id);
        return route is null ? NotFound(new { message = "Ruta no encontrada." }) : Ok(route);
    }

    /// <summary>Rutas asignadas a un conductor específico (usado por el portal del operador).</summary>
    [HttpGet("driver/{driverId:guid}")]
    public async Task<ActionResult<IReadOnlyList<DeliveryRouteDto>>> GetByDriver(Guid driverId)
        => Ok(await _routeService.GetRoutesByDriverAsync(driverId));

    /// <summary>Crea una ruta completa paso a paso (patrón Builder).</summary>
    [HttpPost]
    [RequireRole(UserRole.Admin)]
    public async Task<ActionResult<DeliveryRouteDto>> Create([FromBody] CreateDeliveryRouteRequest request)
    {
        try
        {
            var route = await _routeService.CreateRouteAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = route.Id }, route);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Crea rápidamente una ruta "express" usando el Director del Builder.</summary>
    [HttpPost("express")]
    [RequireRole(UserRole.Admin)]
    public async Task<ActionResult<DeliveryRouteDto>> CreateExpress([FromBody] CreateExpressRouteRequest request)
    {
        try
        {
            var route = await _routeService.CreateExpressRouteAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = route.Id }, route);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Duplica una ruta existente para reutilizarla como ruta recurrente (patrón Prototype).</summary>
    [HttpPost("{id:guid}/duplicate")]
    [RequireRole(UserRole.Admin)]
    public async Task<ActionResult<DeliveryRouteDto>> Duplicate(Guid id, [FromBody] DuplicateRouteRequest request)
    {
        try
        {
            var clone = await _routeService.DuplicateRouteAsync(id, request);
            return CreatedAtAction(nameof(GetById), new { id = clone.Id }, clone);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Cambia el estado de una ruta (Planificada, En Progreso, Retrasada, Completada, Cancelada).</summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateRouteStatusRequest request)
    {
        try
        {
            await _routeService.UpdateRouteStatusAsync(id, request.Status);
            return NoContent();
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
}
