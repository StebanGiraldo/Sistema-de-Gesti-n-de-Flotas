namespace FleetManagement.Application.DTOs;

public record MaintenanceRecordDto(
    Guid Id,
    Guid VehicleId,
    string VehiclePlate,
    string Type,
    DateTime PerformedAt,
    DateTime? NextDueDate,
    double? NextDueMileageKm,
    string Notes,
    double MileageAtServiceKm
);

public record CreateMaintenanceRecordRequest(
    Guid VehicleId,
    string Type,
    DateTime PerformedAt,
    DateTime? NextDueDate,
    double? NextDueMileageKm,
    string Notes,
    double MileageAtServiceKm
);

/// <summary>Resultado del módulo de mantenimiento predictivo: qué vehículo necesita qué tarea y si ya está vencida.</summary>
public record MaintenanceDueDto(
    Guid VehicleId,
    string VehiclePlate,
    string TaskName,
    DateTime? DueDate,
    double? DueMileageKm,
    bool IsOverdue
);
