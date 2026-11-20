using FleetManagement.Domain.Entities;

namespace FleetManagement.Application.Interpreter;

/// <summary>
/// Contexto del monitoreo: lo que una regla puede preguntar de UN vehículo para filtrar la flota. Todo sale de campos reales:
/// <c>status</c>, <c>type</c>, <c>mileageKm</c>, <c>capacityKg</c> y <c>hasDriver</c>. La gramática no tiene valores
/// booleanos, así que <c>hasDriver</c> vale 1 si el vehículo tiene conductor asignado y 0 si no (<c>hasDriver = 0</c> lista los
/// vehículos sin conductor).
/// </summary>
public sealed class VehicleMonitoringContext : VariableContext
{
    private const double WithDriver = 1;
    private const double WithoutDriver = 0;

    public static RuleVocabulary Vocabulary { get; } = new(new[]
    {
        RuleVariables.Status,
        RuleVariables.Type,
        RuleVariables.MileageKm,
        RuleVariables.CapacityKg,
        RuleVariables.HasDriver,
    });

    public VehicleMonitoringContext(Vehicle vehicle)
        : base(VariablesOf(vehicle))
    {
    }

    private static IReadOnlyDictionary<string, RuleValue> VariablesOf(Vehicle vehicle)
        => Table(
            (RuleVariables.Status, RuleValue.FromText(vehicle.Status.ToString())),
            (RuleVariables.Type, RuleValue.FromText(vehicle.Type.ToString())),
            (RuleVariables.MileageKm, RuleValue.FromNumber(vehicle.MileageKm)),
            (RuleVariables.CapacityKg, RuleValue.FromNumber(vehicle.CapacityKg)),
            (RuleVariables.HasDriver, RuleValue.FromNumber(vehicle.AssignedDriverId.HasValue ? WithDriver : WithoutDriver)));
}
