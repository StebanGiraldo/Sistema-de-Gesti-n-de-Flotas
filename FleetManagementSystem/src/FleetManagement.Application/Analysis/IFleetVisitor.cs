namespace FleetManagement.Application.Analysis;

/// <summary>
/// VISITOR (patrón VISITOR): una operación de análisis sobre la flota, con un método por cada tipo de elemento. Un visitante
/// concreto acumula lo que le interesa de cada elemento que visita y después entrega su resultado. Agregar un análisis nuevo es
/// escribir un visitante nuevo; los elementos y la estructura no cambian (Open/Closed).
///
/// Un visitante acumula estado mientras recorre: se usa UNO por análisis y no se comparte entre peticiones ni entre hilos. Por
/// eso no se registra en el contenedor de dependencias: <c>FleetAnalysisService</c> crea los suyos en cada petición.
/// </summary>
public interface IFleetVisitor
{
    void VisitVehicle(VehicleElement vehicle);

    void VisitRoute(RouteElement route);

    void VisitMaintenanceRecord(MaintenanceRecordElement record);
}

/// <summary>
/// Visitante que, al terminar el recorrido, entrega un resultado de tipo <typeparamref name="TResult"/>. Separarlo de
/// <see cref="IFleetVisitor"/> (ISP) deja la estructura de objetos sin saber qué devuelve cada análisis: solo recorre y acepta.
/// </summary>
public interface IFleetVisitor<out TResult> : IFleetVisitor
{
    /// <summary>Resultado del análisis con todo lo visitado hasta ahora. No depende del orden en que se visitaron los elementos.</summary>
    TResult Result { get; }
}
