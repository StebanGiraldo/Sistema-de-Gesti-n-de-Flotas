namespace FleetManagement.Domain.Cargo;

/// <summary>
/// ITERATOR (patrón de comportamiento ITERATOR): contrato del recorrido de la carga.
///
/// Permite a un cliente recorrer la carga de una ruta de uno en uno SIN conocer cómo está organizada
/// la estructura COMPOSITE (grupos, subgrupos, hojas) ni cómo se guardan los hijos: sólo pregunta
/// <see cref="HasNext"/> y pide <see cref="Next"/>. Si mañana el árbol cambiara de forma, o se quisiera
/// otro orden de recorrido, los clientes no se enterarían: bastaría con otro iterador que cumpla este contrato.
///
/// Es un iterador EXTERNO: lo maneja quien lo usa (puede detenerse, retomarse, y se pueden tener varios
/// recorridos independientes sobre el mismo árbol a la vez). El método <c>Traverse()</c> que ya existía es el
/// iterador INTERNO (la propia colección empuja los elementos con <c>yield return</c>); se conserva sin cambios.
/// </summary>
public interface ICargoIterator
{
    /// <summary>Indica si todavía queda algún componente por entregar. No consume nada: se puede llamar varias veces.</summary>
    bool HasNext();

    /// <summary>
    /// Entrega el siguiente componente y avanza.
    /// Lanza <see cref="InvalidOperationException"/> si el recorrido ya terminó (<see cref="HasNext"/> es false).
    /// </summary>
    ICargoComponent Next();
}
