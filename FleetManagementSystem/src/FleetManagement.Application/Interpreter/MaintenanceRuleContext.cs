using FleetManagement.Domain.Entities;

namespace FleetManagement.Application.Interpreter;

/// <summary>
/// Contexto del mantenimiento predictivo: lo que una regla puede preguntar de UN vehículo y su historial.
/// <list type="bullet">
/// <item><c>mileageKm</c>, <c>type</c>, <c>status</c>: campos del vehículo.</item>
/// <item><c>vehicleAgeYears</c>: año actual menos el año del vehículo (nunca negativo).</item>
/// <item><c>daysSinceLastService</c> y <c>kmSinceLastService</c>: desde el mantenimiento más reciente (el de mayor fecha y, a igual
/// fecha, el de mayor kilometraje: la misma regla que el Visitor de la flota). <b>Sin mantenimientos no tienen dato</b>: no se
/// inventa un 0, así que una comparación sobre ellas da falso (el vehículo «nunca revisado» no entra en la regla; para eso
/// están los hallazgos del análisis de la flota).</item>
/// </list>
/// La hora sale del <see cref="TimeProvider"/>, así que las pruebas fijan el «hoy» exacto.
/// </summary>
public sealed class MaintenanceRuleContext : VariableContext
{
    public static RuleVocabulary Vocabulary { get; } = new(new[]
    {
        RuleVariables.MileageKm,
        RuleVariables.VehicleAgeYears,
        RuleVariables.DaysSinceLastService,
        RuleVariables.KmSinceLastService,
        RuleVariables.Type,
        RuleVariables.Status,
    });

    public MaintenanceRuleContext(Vehicle vehicle, IEnumerable<MaintenanceRecord> records, TimeProvider clock)
        : base(VariablesOf(vehicle, LatestOf(records), clock.GetUtcNow().UtcDateTime))
    {
    }

    private static IReadOnlyDictionary<string, RuleValue> VariablesOf(Vehicle vehicle, MaintenanceRecord? latest, DateTime now)
        => Table(
            (RuleVariables.MileageKm, RuleValue.FromNumber(vehicle.MileageKm)),
            (RuleVariables.VehicleAgeYears, RuleValue.FromNumber(Math.Max(0, now.Year - vehicle.Year))),
            (RuleVariables.DaysSinceLastService, RuleValue.FromOptional(DaysSince(latest, now))),
            (RuleVariables.KmSinceLastService, RuleValue.FromOptional(KmSince(vehicle, latest))),
            (RuleVariables.Type, RuleValue.FromText(vehicle.Type.ToString())),
            (RuleVariables.Status, RuleValue.FromText(vehicle.Status.ToString())));

    private static MaintenanceRecord? LatestOf(IEnumerable<MaintenanceRecord> records)
        => records.OrderByDescending(record => record.PerformedAt).ThenByDescending(record => record.MileageAtServiceKm).FirstOrDefault();

    private static double? DaysSince(MaintenanceRecord? latest, DateTime now)
        => latest is null ? null : Math.Max(0, Math.Floor((now - latest.PerformedAt).TotalDays));

    private static double? KmSince(Vehicle vehicle, MaintenanceRecord? latest)
        => latest is null ? null : Math.Max(0, vehicle.MileageKm - latest.MileageAtServiceKm);
}
