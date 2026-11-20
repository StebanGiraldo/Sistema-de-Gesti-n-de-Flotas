using FleetManagement.Api.Filters;
using FleetManagement.Application.Interpreter;
using FleetManagement.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Api.Controllers;

/// <summary>
/// Reglas escritas como texto (patrón Interpreter, <c>Application/Interpreter</c>). Son consultas de SOLO LECTURA: se usa
/// <c>POST</c> únicamente porque la regla viaja en el cuerpo. Quien escribe reglas es un administrador, así que el controlador
/// exige ese rol, igual que <c>AuditController</c> con su consulta. Una regla inválida responde 400 con
/// <c>{ "message": ... }</c> y la posición del error.
/// </summary>
[ApiController]
[Route("api/rules")]
[RequireRole(UserRole.Admin)]
public class RulesController : ControllerBase
{
    private readonly IRuleEvaluationService _rules;

    public RulesController(IRuleEvaluationService rules)
    {
        _rules = rules;
    }

    /// <summary>
    /// Vehículos que cumplen una regla de mantenimiento predictivo. Variables: <c>mileageKm</c>, <c>vehicleAgeYears</c>,
    /// <c>daysSinceLastService</c>, <c>kmSinceLastService</c>, <c>type</c>, <c>status</c>. Ejemplo:
    /// <c>{ "rule": "kmSinceLastService &gt;= 8000 OR daysSinceLastService &gt; 180" }</c>.
    /// </summary>
    [HttpPost("maintenance/evaluate")]
    public Task<IActionResult> EvaluateMaintenance([FromBody] RuleRequest request)
        => RunAsync(() => _rules.EvaluateMaintenanceRuleAsync(request.Rule ?? string.Empty));

    /// <summary>
    /// Vehículos que cumplen un filtro de monitoreo. Variables: <c>status</c>, <c>type</c>, <c>mileageKm</c>, <c>capacityKg</c>,
    /// <c>hasDriver</c> (1 con conductor, 0 sin él). Ejemplo: <c>{ "rule": "status = \"Available\" AND type = \"Truck\"" }</c>.
    /// </summary>
    [HttpPost("vehicles/filter")]
    public Task<IActionResult> FilterVehicles([FromBody] RuleRequest request)
        => RunAsync(() => _rules.FilterVehiclesAsync(request.Rule ?? string.Empty));

    private async Task<IActionResult> RunAsync(Func<Task<RuleEvaluationDto>> evaluate)
    {
        try
        {
            return Ok(await evaluate());
        }
        catch (RuleSyntaxException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
