using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Api.Controllers;

/// <summary>
/// Reportes de la flota (patrón Decorator). El controlador solo recibe las secciones pedidas y delega: la
/// cadena de decoradores se arma en <see cref="IFleetReportService"/>.
/// </summary>
[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IFleetReportService _reportService;

    public ReportsController(IFleetReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// Reporte de la flota. Sin parámetros devuelve el reporte base (metadatos, vehículos y rutas). El parámetro
    /// <c>sections</c> agrega secciones, repetido o separado por comas: <c>Statistics</c>, <c>MaintenanceAlerts</c>,
    /// <c>CargoSummary</c>. Ejemplo: <c>GET /api/reports/fleet?sections=Statistics,CargoSummary</c>.
    /// </summary>
    [HttpGet("fleet")]
    public async Task<ActionResult<FleetReportDto>> GetFleetReport([FromQuery] string[]? sections)
    {
        try
        {
            return Ok(await _reportService.GenerateFleetReportAsync(sections));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
