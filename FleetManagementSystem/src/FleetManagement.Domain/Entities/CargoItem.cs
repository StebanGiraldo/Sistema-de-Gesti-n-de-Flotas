using FleetManagement.Domain.Common;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Domain.Entities;

/// <summary>Artículo o paquete de carga transportado dentro de una ruta.</summary>
public class CargoItem : IPrototype<CargoItem>
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Description { get; set; } = string.Empty;
    public double WeightKg { get; set; }
    public double VolumeM3 { get; set; }
    public CargoPriority Priority { get; set; } = CargoPriority.Standard;

    /// <summary>Copia profunda usada por DeliveryRoute.Clone() (Prototype).</summary>
    public CargoItem Clone() => new()
    {
        Id = Guid.NewGuid(),
        Description = Description,
        WeightKg = WeightKg,
        VolumeM3 = VolumeM3,
        Priority = Priority
    };
}
