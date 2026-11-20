using FleetManagement.Domain.Entities;

namespace FleetManagement.Application.Interpreter;

/// <summary>Cuerpo de las peticiones: <c>{ "rule": "..." }</c>.</summary>
public sealed record RuleRequest(string? Rule);

public sealed record RuleMatchedVehicleDto(Guid VehicleId, string LicensePlate, string Type, string Status);

/// <summary>Cuántos vehículos se evaluaron y cuáles cumplen la regla.</summary>
public sealed record RuleEvaluationDto(string Rule, int EvaluatedVehicles, int MatchedVehicles, IReadOnlyList<RuleMatchedVehicleDto> Vehicles);

public interface IRuleEvaluationService
{
    /// <summary>Vehículos que cumplen una regla de mantenimiento predictivo (p. ej. <c>kmSinceLastService &gt;= 8000</c>).</summary>
    Task<RuleEvaluationDto> EvaluateMaintenanceRuleAsync(string rule);

    /// <summary>Vehículos que cumplen un filtro de monitoreo (p. ej. <c>status = "Available" AND type = "Truck"</c>).</summary>
    Task<RuleEvaluationDto> FilterVehiclesAsync(string rule);
}

/// <summary>
/// Caso de uso de solo lectura: interpreta la regla sobre los vehículos del caso de uso pedido. No modifica ningún dato, no
/// audita ni notifica. La regla se analiza ANTES de leer los repositorios, así que una regla inválida no cuesta una lectura.
/// Lanza <see cref="RuleSyntaxException"/> si la regla no es válida.
/// </summary>
public sealed class RuleEvaluationService : IRuleEvaluationService
{
    private readonly IRuleParser _parser;
    private readonly IRuleScope _maintenanceScope;
    private readonly IRuleScope _monitoringScope;

    public RuleEvaluationService(IRuleParser parser, IRuleScope maintenanceScope, IRuleScope monitoringScope)
    {
        _parser = parser;
        _maintenanceScope = maintenanceScope;
        _monitoringScope = monitoringScope;
    }

    public Task<RuleEvaluationDto> EvaluateMaintenanceRuleAsync(string rule) => EvaluateAsync(_maintenanceScope, rule);

    public Task<RuleEvaluationDto> FilterVehiclesAsync(string rule) => EvaluateAsync(_monitoringScope, rule);

    private async Task<RuleEvaluationDto> EvaluateAsync(IRuleScope scope, string rule)
    {
        var expression = _parser.Parse(rule, scope.Vocabulary);
        var subjects = await scope.LoadSubjectsAsync();
        var matches = subjects
            .Where(subject => expression.Interpret(subject.Context))
            .Select(subject => ToDto(subject.Vehicle))
            .ToList();

        return new RuleEvaluationDto(rule, subjects.Count, matches.Count, matches);
    }

    private static RuleMatchedVehicleDto ToDto(Vehicle vehicle)
        => new(vehicle.Id, vehicle.LicensePlate, vehicle.Type.ToString(), vehicle.Status.ToString());
}
