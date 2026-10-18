namespace FleetManagement.Application.DTOs;

/// <summary>Plantilla de una tarea de mantenimiento preventivo de un tipo de vehículo.</summary>
public record MaintenanceTaskTemplateDto(string TaskName, string Type, int IntervalDays, double IntervalKm);

/// <summary>Perfil compartido por todos los vehículos de un tipo (ver el patrón Flyweight en Application/Flyweights).</summary>
public record VehicleTypeProfileDto(
    string Type,
    string DisplayName,
    double MaxSpeedKmh,
    bool AvoidHighways,
    bool AvoidTolls,
    bool AvoidLowBridges,
    IReadOnlyList<MaintenanceTaskTemplateDto> MaintenanceTasks);
