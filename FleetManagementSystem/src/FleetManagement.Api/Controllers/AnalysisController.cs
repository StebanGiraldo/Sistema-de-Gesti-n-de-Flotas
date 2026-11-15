using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Api.Controllers;

/// <summary>
/// Análisis de la flota (patrón Visitor). Es un controlador aparte a propósito: <c>ReportsController</c> y su cadena de
/// decoradores no se modifican y siguen entregando lo mismo que antes. Igual que los reportes, es de lectura y no exige rol.
/// </summary>
[ApiController]
[Route("api/analysis")]
public class AnalysisController : ControllerBase
{
    private readonly IFleetAnalysisService _analysisService;

    public AnalysisController(IFleetAnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    /// <summary>
    /// Análisis de la flota con los datos de este momento: utilización de los vehículos, recorridos y distancias de las rutas,
    /// historial de mantenimiento y los hallazgos operativos que merecen atención, del más al menos urgente.
    /// </summary>
    [HttpGet("fleet")]
    public async Task<ActionResult<FleetAnalysisDto>> GetFleetAnalysis()
        => Ok(await _analysisService.AnalyzeFleetAsync());
}
