using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Services;

/// <summary>
/// Caso de uso de Mantenimiento predictivo (requerimiento #3 del sistema):
/// registra el historial de mantenimiento y calcula qué vehículos tienen
/// tareas vencidas por fecha o por kilometraje.
/// </summary>
public class MaintenanceService : IMaintenanceService
{
    private readonly IMaintenanceRepository _maintenanceRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IFleetAuditLogger _auditLogger;

    public MaintenanceService(IMaintenanceRepository maintenanceRepository, IVehicleRepository vehicleRepository, IFleetAuditLogger auditLogger)
    {
        _maintenanceRepository = maintenanceRepository;
        _vehicleRepository = vehicleRepository;
        _auditLogger = auditLogger;
    }

    public async Task<IReadOnlyList<MaintenanceRecordDto>> GetAllAsync()
    {
        var records = await _maintenanceRepository.GetAllAsync();
        var vehicles = await _vehicleRepository.GetAllAsync();
        return records.OrderByDescending(r => r.PerformedAt).Select(r => MapToDto(r, vehicles)).ToList();
    }

    public async Task<IReadOnlyList<MaintenanceRecordDto>> GetByVehicleAsync(Guid vehicleId)
    {
        var records = await _maintenanceRepository.GetByVehicleIdAsync(vehicleId);
        var vehicles = await _vehicleRepository.GetAllAsync();
        return records.OrderByDescending(r => r.PerformedAt).Select(r => MapToDto(r, vehicles)).ToList();
    }

    public async Task<MaintenanceRecordDto> CreateAsync(CreateMaintenanceRecordRequest request)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId)
            ?? throw new KeyNotFoundException("Vehículo no encontrado.");

        if (!Enum.TryParse<MaintenanceType>(request.Type, true, out var type))
            throw new ArgumentException($"Tipo de mantenimiento inválido: '{request.Type}'.");

        var record = new MaintenanceRecord
        {
            VehicleId = request.VehicleId,
            Type = type,
            PerformedAt = request.PerformedAt,
            NextDueDate = request.NextDueDate,
            NextDueMileageKm = request.NextDueMileageKm,
            Notes = request.Notes,
            MileageAtServiceKm = request.MileageAtServiceKm
        };
        var saved = await _maintenanceRepository.AddAsync(record);

        vehicle.LastMaintenanceDate = request.PerformedAt;
        if (vehicle.Status == VehicleStatus.Maintenance)
            vehicle.Status = VehicleStatus.Available;
        await _vehicleRepository.UpdateAsync(vehicle);

        _auditLogger.LogEvent("Mantenimiento", $"Registrado mantenimiento '{type}' para el vehículo {vehicle.LicensePlate}.");

        return MapToDto(saved, new List<Vehicle> { vehicle });
    }

    /// <summary>
    /// Módulo predictivo: para cada vehículo, toma el registro más reciente
    /// de cada tipo de tarea y evalúa si ya venció por fecha o por
    /// kilometraje recorrido desde el último servicio.
    /// </summary>
    public async Task<IReadOnlyList<MaintenanceDueDto>> GetVehiclesDueForMaintenanceAsync()
    {
        var vehicles = await _vehicleRepository.GetAllAsync();
        var records = await _maintenanceRepository.GetAllAsync();
        var today = DateTime.UtcNow;
        var result = new List<MaintenanceDueDto>();

        foreach (var vehicle in vehicles)
        {
            var latestPerType = records
                .Where(r => r.VehicleId == vehicle.Id)
                .GroupBy(r => r.Type)
                .Select(g => g.OrderByDescending(r => r.PerformedAt).First());

            foreach (var record in latestPerType)
            {
                var overdueByDate = record.NextDueDate.HasValue && record.NextDueDate.Value <= today;
                var overdueByKm = record.NextDueMileageKm.HasValue && vehicle.MileageKm >= record.NextDueMileageKm.Value;

                if (overdueByDate || overdueByKm)
                {
                    result.Add(new MaintenanceDueDto(vehicle.Id, vehicle.LicensePlate, record.Type.ToString(), record.NextDueDate, record.NextDueMileageKm, true));
                }
            }
        }

        return result;
    }

    private static MaintenanceRecordDto MapToDto(MaintenanceRecord r, IReadOnlyList<Vehicle> vehicles)
    {
        var plate = vehicles.FirstOrDefault(v => v.Id == r.VehicleId)?.LicensePlate ?? "(desconocido)";
        return new MaintenanceRecordDto(r.Id, r.VehicleId, plate, r.Type.ToString(), r.PerformedAt, r.NextDueDate, r.NextDueMileageKm, r.Notes, r.MileageAtServiceKm);
    }
}
