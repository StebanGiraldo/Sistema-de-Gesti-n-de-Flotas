namespace FleetManagement.Application.Analysis;

/// <summary>
/// Base de los visitantes concretos: ignora por defecto cada tipo de elemento, así un análisis solo sobrescribe los tipos que le
/// interesan (el de mantenimiento no tiene por qué escribir un método vacío para las rutas). No contiene lógica de análisis.
/// </summary>
public abstract class FleetVisitor<TResult> : IFleetVisitor<TResult>
{
    public virtual void VisitVehicle(VehicleElement vehicle)
    {
    }

    public virtual void VisitRoute(RouteElement route)
    {
    }

    public virtual void VisitMaintenanceRecord(MaintenanceRecordElement record)
    {
    }

    public abstract TResult Result { get; }
}
