namespace FleetManagement.Domain.Cargo;

/// <summary>
/// Punto de entrada del patrón ITERATOR sobre el COMPOSITE de carga (el "CreateIterator" del agregado, en
/// términos GoF). Se escribe como métodos de extensión sobre <see cref="ICargoComponent"/> para no tocar el
/// Composite: la interfaz y sus implementaciones (<see cref="CargoGroup"/>, <see cref="CargoItemComponent"/>)
/// quedan exactamente como estaban, igual que <see cref="CargoComponentExtensions.Traverse"/>.
/// </summary>
public static class CargoIteratorExtensions
{
    /// <summary>Iterador sobre TODOS los componentes (grupos y artículos), en profundidad y pre-orden; el primero es el propio componente.</summary>
    public static ICargoIterator CreateIterator(this ICargoComponent root)
        => new CargoDepthFirstIterator(root);

    /// <summary>Iterador sólo sobre los artículos individuales (las hojas), en el mismo orden en que aparecen en el árbol.</summary>
    public static ICargoIterator CreateItemIterator(this ICargoComponent root)
        => new CargoDepthFirstIterator(root, IsItem);

    /// <summary>
    /// Un artículo es un componente sin hijos que cuenta como carga. Se define con el contrato de
    /// <see cref="ICargoComponent"/> (una hoja vale 1 en <see cref="ICargoComponent.ItemCount"/>), así que no
    /// distingue clases concretas y un grupo vacío (0 artículos) no se confunde con una hoja.
    /// </summary>
    private static bool IsItem(ICargoComponent component)
        => component.Children.Count == 0 && component.ItemCount > 0;
}
