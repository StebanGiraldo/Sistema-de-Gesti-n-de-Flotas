using FleetManagement.Api.Filters;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Api.Controllers;

/// <summary>
/// Asignación de cargas a vehículos (requerimiento #2): propone qué vehículo lleva cada entrega y en qué orden las visita,
/// comparado con el reparto sin optimizar, y permite convertir el plan en rutas planificadas. Solo para administradores.
/// </summary>
[ApiController]
[Route("api/dispatch")]
public class DispatchController : ControllerBase
{
    private readonly IDispatchPlanningService _planningService;

    public DispatchController(IDispatchPlanningService planningService)
    {
        _planningService = planningService;
    }

    /// <summary>Propone el reparto. No guarda nada: sirve para revisar el plan antes de aplicarlo.</summary>
    [HttpPost("plan")]
    [RequireRole(UserRole.Admin)]
    public async Task<ActionResult<DispatchPlanDto>> Plan([FromBody] DispatchPlanRequest request)
    {
        try
        {
            return Ok(await _planningService.PlanAsync(request));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Crea una ruta planificada por cada viaje del plan (todo o nada).</summary>
    [HttpPost("apply")]
    [RequireRole(UserRole.Admin)]
    public async Task<ActionResult<DispatchApplyResultDto>> Apply([FromBody] DispatchPlanRequest request)
    {
        try
        {
            return Ok(await _planningService.ApplyAsync(request));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) // datos inválidos o regla de asignación incumplida
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
