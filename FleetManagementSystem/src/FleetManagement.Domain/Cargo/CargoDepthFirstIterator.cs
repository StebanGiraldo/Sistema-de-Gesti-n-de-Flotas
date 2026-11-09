namespace FleetManagement.Domain.Cargo;

/// <summary>
/// CONCRETE ITERATOR (patrón ITERATOR): recorre un árbol de carga en profundidad (pre-orden): primero el propio
/// componente y luego, de izquierda a derecha, cada uno de sus descendientes. Es el mismo orden de
/// <c>Traverse()</c>, pero con el estado del recorrido guardado en el iterador (una pila explícita) y no en la
/// pila de llamadas, lo que le da tres propiedades que el recorrido recursivo no tiene:
///
///  • Es externo: el cliente avanza cuando quiere (<see cref="HasNext"/>/<see cref="Next"/>).
///  • No es recursivo: un árbol muy profundo no puede desbordar la pila de llamadas y cada nodo cuesta O(1).
///  • No repite componentes: cada instancia se entrega una sola vez aunque sea alcanzable por dos caminos
///    (un mismo componente agregado a dos grupos) y termina aunque una implementación propia de
///    <see cref="ICargoComponent"/> formara un ciclo. <c>CargoGroup.Add</c> ya impide los ciclos, así que esto es
///    una segunda defensa, no la primera.
///
/// Un filtro opcional deja pasar sólo los componentes que interesan (por ejemplo, sólo los artículos) sin
/// cambiar el orden. El iterador sólo LEE el árbol (<see cref="ICargoComponent.Children"/>): no lo modifica.
/// La estructura no debería modificarse mientras se recorre: cada nodo se lee en el momento de visitarlo.
/// </summary>
public sealed class CargoDepthFirstIterator : ICargoIterator
{
    private readonly Stack<ICargoComponent> _pending = new();
    private readonly HashSet<ICargoComponent> _visited = new(ReferenceEqualityComparer.Instance);
    private readonly Func<ICargoComponent, bool> _accepts;

    private ICargoComponent? _lookahead;
    private bool _hasLookahead;

    /// <param name="root">Componente desde el que se recorre (hoja o grupo).</param>
    /// <param name="accepts">Filtro opcional: sólo se entregan los componentes para los que devuelve true. Sin filtro se entregan todos.</param>
    public CargoDepthFirstIterator(ICargoComponent root, Func<ICargoComponent, bool>? accepts = null)
    {
        ArgumentNullException.ThrowIfNull(root);

        _accepts = accepts ?? (_ => true);
        _pending.Push(root);
    }

    public bool HasNext()
    {
        Peek();
        return _lookahead is not null;
    }

    public ICargoComponent Next()
    {
        Peek();

        var current = _lookahead ?? throw new InvalidOperationException("El recorrido de la carga ya terminó: no quedan más componentes.");
        _lookahead = null;
        _hasLookahead = false;
        return current;
    }

    /// <summary>Calcula (una sola vez por elemento) el próximo componente a entregar; así HasNext() no consume nada.</summary>
    private void Peek()
    {
        if (_hasLookahead)
            return;

        _lookahead = FindNext();
        _hasLookahead = true;
    }

    private ICargoComponent? FindNext()
    {
        while (_pending.Count > 0)
        {
            var component = _pending.Pop();
            if (!_visited.Add(component))
                continue; // ya entregado: componente compartido por dos grupos o ciclo

            // Se apilan al revés para que el primer hijo quede arriba y se visite primero: conserva el orden.
            for (var i = component.Children.Count - 1; i >= 0; i--)
                _pending.Push(component.Children[i]);

            if (_accepts(component))
                return component;
        }

        return null;
    }
}
