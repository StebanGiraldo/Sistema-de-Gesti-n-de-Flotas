using FleetManagement.Domain.Entities;

namespace FleetManagement.Domain.Cargo;

/// <summary>
/// LEAF (patrón COMPOSITE): representa un artículo de carga individual.
///
/// Envuelve (por composición) la entidad <see cref="CargoItem"/> ya existente
/// en lugar de modificarla: la entidad conserva intactos su mapeo, su
/// serialización y su Clone() (patrón Prototype). Es una vista "en vivo": si
/// el peso o volumen del <see cref="CargoItem"/> cambia, los totales del
/// árbol lo reflejan en la siguiente consulta.
/// </summary>
public sealed class CargoItemComponent : ICargoComponent
{
    public CargoItemComponent(CargoItem item)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
    }

    /// <summary>Entidad de dominio envuelta por esta hoja.</summary>
    public CargoItem Item { get; }

    public string Name => Item.Description;
    public double TotalWeightKg => Item.WeightKg;
    public double TotalVolumeM3 => Item.VolumeM3;
    public int ItemCount => 1;
    public IReadOnlyList<ICargoComponent> Children => Array.Empty<ICargoComponent>();
}
