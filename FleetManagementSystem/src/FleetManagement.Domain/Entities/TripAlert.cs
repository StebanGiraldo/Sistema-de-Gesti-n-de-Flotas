using FleetManagement.Domain.Enums;

namespace FleetManagement.Domain.Entities;

/// <summary>
/// Alerta o incidente reportado por un operador durante un viaje (retraso,
/// avería, tráfico, accidente, clima, etc.). Se llama "TripAlert" para
/// evitar un nombre genérico como "Alert".
/// </summary>
public class TripAlert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RouteId { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public AlertType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public int DelayMinutes { get; set; }
    public AlertStatus Status { get; set; } = AlertStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
