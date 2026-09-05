using FleetManagement.Domain.Enums;

namespace FleetManagement.Domain.Entities;

/// <summary>Registro histórico de un evento de mantenimiento ya realizado sobre un vehículo.</summary>
public class MaintenanceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public MaintenanceType Type { get; set; }
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    public DateTime? NextDueDate { get; set; }
    public double? NextDueMileageKm { get; set; }
    public string Notes { get; set; } = string.Empty;
    public double MileageAtServiceKm { get; set; }
}
