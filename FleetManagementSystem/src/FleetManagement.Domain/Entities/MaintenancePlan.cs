using FleetManagement.Domain.Enums;

namespace FleetManagement.Domain.Entities;

/// <summary>Una tarea individual dentro de un plan de mantenimiento preventivo.</summary>
public class MaintenanceTaskDefinition
{
    public string TaskName { get; set; } = string.Empty;
    public MaintenanceType Type { get; set; }
    public int IntervalDays { get; set; }
    public double IntervalKm { get; set; }
}

/// <summary>
/// Conjunto de tareas de mantenimiento preventivo apropiadas para un tipo de
/// vehículo. Es uno de los dos productos de la familia creada por
/// IFleetOnboardingAbstractFactory (patrón ABSTRACT FACTORY), junto con
/// NavigationProfile.
/// </summary>
public class MaintenancePlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public VehicleType ApplicableVehicleType { get; set; }
    public List<MaintenanceTaskDefinition> Tasks { get; set; } = new();
}
