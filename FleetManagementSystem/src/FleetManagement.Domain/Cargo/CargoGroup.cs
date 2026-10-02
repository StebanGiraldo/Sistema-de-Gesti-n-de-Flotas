namespace FleetManagement.Domain.Cargo;

/// <summary>
/// COMPOSITE (patrón estructural COMPOSITE): grupo de cargas que contiene
/// otros componentes, ya sean artículos individuales o más grupos (por
/// ejemplo un pallet que contiene cajas, o un contenedor que contiene
/// pallets). Sus totales se calculan delegando en sus hijos de forma
/// recursiva, así que siempre son coherentes con el contenido actual del
/// árbol (no se cachean).
///
/// Protege dos invariantes propias de un árbol: un componente no puede
/// agregarse dos veces al mismo grupo, y no puede formarse un ciclo (un
/// grupo dentro de sí mismo o dentro de uno de sus descendientes), porque el
/// cálculo recursivo de totales no terminaría.
/// </summary>
public sealed class CargoGroup : ICargoComponent
{
    private readonly List<ICargoComponent> _children = new();

    public CargoGroup(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El grupo de carga debe tener un nombre.", nameof(name));

        Name = name;
    }

    public string Name { get; }
    public IReadOnlyList<ICargoComponent> Children => _children;

    public double TotalWeightKg => _children.Sum(c => c.TotalWeightKg);
    public double TotalVolumeM3 => _children.Sum(c => c.TotalVolumeM3);
    public int ItemCount => _children.Sum(c => c.ItemCount);

    /// <summary>Agrega una hoja o un subgrupo a este grupo.</summary>
    public void Add(ICargoComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (component.Traverse().Any(c => ReferenceEquals(c, this)))
            throw new InvalidOperationException(
                "No se puede agregar un grupo dentro de sí mismo ni dentro de uno de sus descendientes (formaría un ciclo).");

        if (_children.Contains(component))
            throw new InvalidOperationException("El componente ya pertenece a este grupo.");

        _children.Add(component);
    }

    /// <summary>Quita un hijo directo (con todo su subárbol). Devuelve false si no era hijo de este grupo.</summary>
    public bool Remove(ICargoComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        return _children.Remove(component);
    }
}
