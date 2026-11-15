namespace FleetManagement.Application.Analysis;

/// <summary>
/// Regla única para saber cuánto ha recorrido un vehículo desde su último mantenimiento. La usan el análisis del historial de
/// mantenimiento y el de hallazgos operativos: así ambos dan la misma cifra y la regla no se escribe dos veces.
/// </summary>
internal static class MaintenanceMileage
{
    /// <summary>El mantenimiento más reciente por fecha (a igual fecha, el de mayor kilometraje), o <c>null</c> si no hay ninguno.</summary>
    public static MaintenanceRecordElement? Latest(IEnumerable<MaintenanceRecordElement> records)
        => records.OrderByDescending(record => record.PerformedAt).ThenByDescending(record => record.MileageAtServiceKm).FirstOrDefault();

    /// <summary>Kilómetros desde ese mantenimiento hasta el kilometraje actual (nunca negativo); <c>null</c> si nunca tuvo uno.</summary>
    public static double? KmSince(VehicleElement vehicle, MaintenanceRecordElement? latest)
        => latest is null ? null : Math.Max(0, vehicle.MileageKm - latest.MileageAtServiceKm);
}
