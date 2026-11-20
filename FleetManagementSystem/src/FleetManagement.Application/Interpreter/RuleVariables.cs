namespace FleetManagement.Application.Interpreter;

/// <summary>
/// Nombres de las variables que las reglas pueden usar, escritos una sola vez. Cada contexto ofrece un subconjunto y su
/// <see cref="RuleVocabulary"/> es la lista blanca de ese subconjunto.
/// </summary>
public static class RuleVariables
{
    public const string Status = "status";
    public const string Type = "type";
    public const string MileageKm = "mileageKm";
    public const string CapacityKg = "capacityKg";
    public const string HasDriver = "hasDriver";
    public const string VehicleAgeYears = "vehicleAgeYears";
    public const string DaysSinceLastService = "daysSinceLastService";
    public const string KmSinceLastService = "kmSinceLastService";
}
