namespace FleetManagement.Domain.Cargo;

public static class CargoComponentExtensions
{
    /// <summary>
    /// Recorre el árbol en profundidad (pre-orden): primero el propio
    /// componente y luego cada uno de sus descendientes. Se escribe una sola
    /// vez sobre <see cref="ICargoComponent.Children"/>, por lo que sirve
    /// igual para hojas y grupos sin duplicar lógica en cada implementación.
    /// </summary>
    public static IEnumerable<ICargoComponent> Traverse(this ICargoComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        yield return component;

        foreach (var child in component.Children)
            foreach (var descendant in child.Traverse())
                yield return descendant;
    }
}
