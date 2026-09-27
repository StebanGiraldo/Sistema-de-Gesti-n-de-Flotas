using FleetManagement.Api.Filters;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Api.Controllers;

/// <summary>
/// Monitoreo y administración de vehículos de la flota (requerimiento #1:
/// monitoreo en tiempo real). Expone el endpoint principal que el frontend
/// consulta periódicamente para pintar los marcadores en el mapa de Leaflet.
/// </summary>
[ApiController]
[Route("api/fleet")]
public class FleetController : ControllerBase
{
    private readonly IVehicleService _vehicleService;

    public FleetController(IVehicleService vehicleService)
    {
        _vehicleService = vehicleService;
    }

    /// <summary>
    /// Devuelve la ubicación y el estado actual de todos los vehículos de la
    /// flota. El frontend lo consume periódicamente (fetch + polling) para
    /// saber cuáles vehículos están disponibles y cuáles en recorrido.
    /// </summary>
    [HttpGet("vehicles")]
    public async Task<ActionResult<IReadOnlyList<VehicleDto>>> GetVehicles()
    {
        var vehicles = await _vehicleService.GetAllVehiclesAsync();
        return Ok(vehicles);
    }

    [HttpGet("vehicles/{id:guid}")]
    public async Task<ActionResult<VehicleDto>> GetVehicleById(Guid id)
    {
        var vehicle = await _vehicleService.GetVehicleByIdAsync(id);
        return vehicle is null ? NotFound(new { message = "Vehículo no encontrado." }) : Ok(vehicle);
    }

    /// <summary>Registra un vehículo nuevo (patrones Factory Method + Abstract Factory).</summary>
    [HttpPost("vehicles")]
    [RequireRole(UserRole.Admin)]
    public async Task<ActionResult<VehicleDto>> CreateVehicle([FromBody] CreateVehicleRequest request)
    {
        try
        {
            var vehicle = await _vehicleService.CreateVehicleAsync(request);
            return CreatedAtAction(nameof(GetVehicleById), new { id = vehicle.Id }, vehicle);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Clona un vehículo plantilla para altas rápidas de flota (patrón Prototype).</summary>
    [HttpPost("vehicles/{id:guid}/clone")]
    [RequireRole(UserRole.Admin)]
    public async Task<ActionResult<VehicleDto>> CloneVehicle(Guid id, [FromBody] CloneVehicleRequest request)
    {
        try
        {
            var clone = await _vehicleService.CloneVehicleAsync(id, request);
            return CreatedAtAction(nameof(GetVehicleById), new { id = clone.Id }, clone);
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

    /// <summary>Actualiza el estado operativo de un vehículo (Disponible, En Ruta, Mantenimiento, Fuera de servicio).</summary>
    [HttpPatch("vehicles/{id:guid}/status")]
    [RequireRole(UserRole.Admin)]
    public async Task<IActionResult> UpdateVehicleStatus(Guid id, [FromBody] UpdateVehicleStatusRequest request)
    {
        try
        {
            await _vehicleService.UpdateVehicleStatusAsync(id, request);
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
