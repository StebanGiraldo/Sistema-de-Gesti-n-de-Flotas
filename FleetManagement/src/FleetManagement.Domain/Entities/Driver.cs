namespace FleetManagement.Domain.Entities;

/// <summary>Conductor/operador de un vehículo de la flota.</summary>
public class Driver
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public Guid? AssignedVehicleId { get; set; }

    /// <summary>Vincula este conductor con su cuenta de acceso (AppUser) para el portal de operador.</summary>
    public Guid UserId { get; set; }
}
