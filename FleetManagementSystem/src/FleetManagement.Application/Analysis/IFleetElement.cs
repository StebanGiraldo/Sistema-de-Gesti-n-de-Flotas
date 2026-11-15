namespace FleetManagement.Application.Analysis;

/// <summary>
/// ELEMENT (patrón VISITOR): cualquier cosa de la flota que se puede analizar. Su único método es <see cref="Accept"/>, el que
/// hace el DOBLE DESPACHO: el elemento sabe de qué tipo es y le dice al visitante cuál de sus operaciones ejecutar
/// (<c>visitor.VisitVehicle(this)</c>, <c>visitor.VisitRoute(this)</c>...). Así el visitante recibe cada elemento con su tipo
/// concreto sin <c>if</c>/<c>switch</c> sobre tipos ni conversiones, y agregar un análisis nuevo no obliga a tocar ningún elemento.
///
/// Los elementos NO son las entidades del dominio (<c>Vehicle</c>, <c>DeliveryRoute</c>, <c>MaintenanceRecord</c>), que no se
/// modifican: son adaptadores inmutables que copian lo que el análisis necesita (ver <c>FleetElements.cs</c>).
/// </summary>
public interface IFleetElement
{
    /// <summary>Entrega este elemento al visitante, llamando a la operación que corresponde a su tipo.</summary>
    void Accept(IFleetVisitor visitor);
}
