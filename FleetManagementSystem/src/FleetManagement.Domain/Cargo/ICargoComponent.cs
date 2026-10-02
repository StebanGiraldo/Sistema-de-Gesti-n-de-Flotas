namespace FleetManagement.Domain.Cargo;

/// <summary>
/// COMPONENT (patrón estructural COMPOSITE).
///
/// Contrato común que permite tratar de manera uniforme un artículo de carga
/// individual (hoja) y un grupo de cargas (compuesto), incluso cuando los
/// grupos contienen otros grupos. Quien consume un <see cref="ICargoComponent"/>
/// obtiene peso, volumen y cantidad de artículos con la misma llamada, sin
/// preguntar si se trata de una hoja o de un grupo.
///
/// Las operaciones que modifican la estructura (Add/Remove) NO forman parte
/// de esta interfaz: sólo existen en <see cref="CargoGroup"/>. Es la variante
/// "segura" de Composite descrita por GoF: una hoja nunca debe lanzar
/// NotSupportedException por una operación que no tiene sentido para ella
/// (Principio de Segregación de Interfaces), a costa de que quien arma o
/// edita la estructura trabaje con <see cref="CargoGroup"/>.
/// </summary>
public interface ICargoComponent
{
    /// <summary>Descripción del artículo (hoja) o nombre del grupo (compuesto).</summary>
    string Name { get; }

    /// <summary>Peso total en kg: el propio de una hoja, o la suma de sus descendientes en un grupo.</summary>
    double TotalWeightKg { get; }

    /// <summary>Volumen total en m³: el propio de una hoja, o la suma de sus descendientes en un grupo.</summary>
    double TotalVolumeM3 { get; }

    /// <summary>Cantidad de artículos de carga (hojas) contenidos: 1 para una hoja, la suma recursiva para un grupo.</summary>
    int ItemCount { get; }

    /// <summary>Hijos directos. Vacío para una hoja; permite recorrer el árbol sin conocer el tipo concreto.</summary>
    IReadOnlyList<ICargoComponent> Children { get; }
}
