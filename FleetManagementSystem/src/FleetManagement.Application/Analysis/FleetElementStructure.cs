using FleetManagement.Domain.Entities;

namespace FleetManagement.Application.Analysis;

/// <summary>
/// OBJECT STRUCTURE (patrón VISITOR): reúne los elementos de la flota y sabe recorrerlos para ofrecérselos a un visitante. Es el
/// "mecanismo de aceptación y recorrido": no sabe qué hace cada visitante ni qué devuelve, solo conoce el orden y se lo muestra
/// todo. Se construye una vez por análisis a partir de las entidades (<see cref="Build"/>) y no cambia después.
///
/// El orden es determinista (vehículos por placa, rutas por nombre, mantenimientos por fecha) para que el mismo estado de la flota
/// dé siempre el mismo resultado; aun así los visitantes no deben depender del orden.
/// </summary>
public sealed class FleetElementStructure
{
    private readonly IReadOnlyList<IFleetElement> _elements;

    private FleetElementStructure(IReadOnlyList<IFleetElement> elements)
    {
        _elements = elements;
    }

    /// <summary>Todos los elementos, en el orden en que se ofrecen a los visitantes.</summary>
    public IReadOnlyList<IFleetElement> Elements => _elements;

    /// <summary>Adapta las entidades a elementos analizables (no las modifica).</summary>
    public static FleetElementStructure Build(
        IEnumerable<Vehicle> vehicles,
        IEnumerable<DeliveryRoute> routes,
        IEnumerable<MaintenanceRecord> maintenanceRecords)
    {
        ArgumentNullException.ThrowIfNull(vehicles);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(maintenanceRecords);

        var elements = new List<IFleetElement>();
        elements.AddRange(vehicles.Select(VehicleElement.From).OrderBy(v => v.LicensePlate, StringComparer.Ordinal));
        elements.AddRange(routes.Select(RouteElement.From).OrderBy(r => r.Name, StringComparer.Ordinal).ThenBy(r => r.Id));
        elements.AddRange(maintenanceRecords.Select(MaintenanceRecordElement.From).OrderBy(m => m.PerformedAt).ThenBy(m => m.Id));
        return new FleetElementStructure(elements);
    }

    /// <summary>Ofrece cada elemento al visitante; cada elemento decide qué operación del visitante se ejecuta.</summary>
    public void Accept(IFleetVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        foreach (var element in _elements)
            element.Accept(visitor);
    }

    /// <summary>Recorre la estructura con el visitante y devuelve su resultado.</summary>
    public TResult Analyze<TResult>(IFleetVisitor<TResult> visitor)
    {
        Accept(visitor);
        return visitor.Result;
    }

    /// <summary>Cantidad de elementos de un tipo (por ejemplo, <c>Count&lt;VehicleElement&gt;()</c>).</summary>
    public int Count<TElement>() where TElement : IFleetElement => _elements.OfType<TElement>().Count();
}
