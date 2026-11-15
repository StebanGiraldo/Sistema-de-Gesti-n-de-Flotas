using FleetManagement.Application.DTOs;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Analysis;

/// <summary>
/// CONCRETE VISITOR #3 (patrón VISITOR): análisis de los registros de mantenimiento. Visita vehículos y mantenimientos y
/// responde cuántos mantenimientos hay de cada tipo, qué vehículos nunca tuvieron uno, cuándo fue el último de cada vehículo,
/// cuántos kilómetros ha recorrido desde entonces y cada cuántos kilómetros se le hace mantenimiento en promedio.
///
/// Describe el HISTORIAL. No decide qué mantenimiento está vencido: eso es del módulo predictivo (<c>IMaintenanceService</c>) y no
/// se duplica aquí. Un mantenimiento de un vehículo que no está en la estructura cuenta en los totales y por tipo, pero no
/// aparece en el detalle por vehículo (no se sabe de quién es la placa).
/// </summary>
public sealed class MaintenanceHistoryVisitor : FleetVisitor<MaintenanceHistoryAnalysisDto>
{
    private const int DistanceDecimals = 3;

    private readonly Dictionary<Guid, VehicleElement> _vehicles = new();
    private readonly Dictionary<Guid, MaintenanceRecordElement> _records = new();

    public override void VisitVehicle(VehicleElement vehicle) => _vehicles[vehicle.Id] = vehicle;

    public override void VisitMaintenanceRecord(MaintenanceRecordElement record) => _records[record.Id] = record;

    public override MaintenanceHistoryAnalysisDto Result
    {
        get
        {
            var recordsByVehicle = _records.Values.ToLookup(record => record.VehicleId);
            var vehicles = _vehicles.Values.OrderBy(vehicle => vehicle.LicensePlate, StringComparer.Ordinal).ToList();

            var withRecords = vehicles.Where(vehicle => recordsByVehicle[vehicle.Id].Any()).ToList();
            var withoutRecords = vehicles.Where(vehicle => !recordsByVehicle[vehicle.Id].Any()).Select(vehicle => vehicle.LicensePlate).ToList();

            return new MaintenanceHistoryAnalysisDto(
                _records.Count,
                ByType(),
                withRecords.Count,
                withoutRecords,
                AverageKmBetweenServices(withRecords.Select(vehicle => recordsByVehicle[vehicle.Id])),
                withRecords.Select(vehicle => ToSummary(vehicle, recordsByVehicle[vehicle.Id].ToList())).ToList());
        }
    }

    private IReadOnlyList<MaintenanceTypeCountDto> ByType()
        => Enum.GetValues<MaintenanceType>()
            .Select(type => new MaintenanceTypeCountDto(type.ToString(), _records.Values.Count(record => record.Type == type)))
            .ToList();

    private static VehicleMaintenanceSummaryDto ToSummary(VehicleElement vehicle, IReadOnlyCollection<MaintenanceRecordElement> records)
    {
        var latest = MaintenanceMileage.Latest(records)!;
        return new VehicleMaintenanceSummaryDto(
            vehicle.Id,
            vehicle.LicensePlate,
            records.Count,
            latest.PerformedAt,
            latest.Type.ToString(),
            Math.Round(vehicle.MileageKm, DistanceDecimals),
            Math.Round(MaintenanceMileage.KmSince(vehicle, latest)!.Value, DistanceDecimals));
    }

    /// <summary>
    /// Promedio de TODOS los intervalos entre mantenimientos consecutivos (por fecha) de cada vehículo. Un intervalo negativo
    /// (kilometraje que baja entre dos registros: dato mal cargado) no es un intervalo real y se ignora.
    /// </summary>
    private static double? AverageKmBetweenServices(IEnumerable<IEnumerable<MaintenanceRecordElement>> recordsPerVehicle)
    {
        var intervals = recordsPerVehicle
            .SelectMany(records =>
            {
                var ordered = records.OrderBy(record => record.PerformedAt).ThenBy(record => record.MileageAtServiceKm).ToList();
                return ordered.Skip(1).Select((record, index) => record.MileageAtServiceKm - ordered[index].MileageAtServiceKm);
            })
            .Where(interval => interval >= 0)
            .ToList();

        return intervals.Count == 0 ? null : Math.Round(intervals.Average(), DistanceDecimals);
    }
}
