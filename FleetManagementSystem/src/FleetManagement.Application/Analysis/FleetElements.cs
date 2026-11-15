using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Analysis;

// CONCRETE ELEMENTS (patrón VISITOR) con forma de ADAPTADORES: cada uno presenta una entidad del dominio como un elemento
// analizable. Son registros inmutables que COPIAN los datos que el análisis necesita en el momento de construirse, por tres
// razones: (1) las entidades no se tocan —no se les agrega ningún método de análisis—; (2) un visitante no puede modificar una
// entidad aunque quisiera; (3) el análisis ve una foto coherente aunque la simulación siga moviendo los vehículos mientras tanto.

/// <summary>Un vehículo de la flota, tal como lo ve el análisis.</summary>
public sealed record VehicleElement(
    Guid Id,
    string LicensePlate,
    VehicleType Type,
    VehicleStatus Status,
    double MileageKm) : IFleetElement
{
    public static VehicleElement From(Vehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        return new VehicleElement(vehicle.Id, vehicle.LicensePlate, vehicle.Type, vehicle.Status, vehicle.MileageKm);
    }

    public void Accept(IFleetVisitor visitor) => visitor.VisitVehicle(this);
}

/// <summary>Una ruta de reparto, tal como la ve el análisis. La distancia es la estimada que ya guarda la ruta.</summary>
public sealed record RouteElement(
    Guid Id,
    string Name,
    DeliveryRouteStatus Status,
    Guid? AssignedVehicleId,
    double DistanceKm,
    int StopCount,
    double EstimatedDurationMinutes,
    int DelayMinutes) : IFleetElement
{
    /// <summary>Una ruta está en curso cuando está en progreso o retrasada (la misma definición que usa el resto del sistema).</summary>
    public bool IsActive => Status is DeliveryRouteStatus.InProgress or DeliveryRouteStatus.Delayed;

    public static RouteElement From(DeliveryRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        return new RouteElement(
            route.Id, route.Name, route.Status, route.AssignedVehicleId, route.EstimatedDistanceKm,
            route.Waypoints.Count, route.EstimatedDurationMinutes, route.DelayMinutes);
    }

    public void Accept(IFleetVisitor visitor) => visitor.VisitRoute(this);
}

/// <summary>Un mantenimiento ya realizado, tal como lo ve el análisis.</summary>
public sealed record MaintenanceRecordElement(
    Guid Id,
    Guid VehicleId,
    MaintenanceType Type,
    DateTime PerformedAt,
    double MileageAtServiceKm) : IFleetElement
{
    public static MaintenanceRecordElement From(MaintenanceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new MaintenanceRecordElement(record.Id, record.VehicleId, record.Type, record.PerformedAt, record.MileageAtServiceKm);
    }

    public void Accept(IFleetVisitor visitor) => visitor.VisitMaintenanceRecord(this);
}
