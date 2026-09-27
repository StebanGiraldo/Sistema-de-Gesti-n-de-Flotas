namespace FleetManagement.Domain.Enums;

/// <summary>Tipo de vehículo de la flota. Determinante para el patrón Factory Method y Abstract Factory.</summary>
public enum VehicleType
{
    Truck,
    Van,
    Car,
    Motorcycle
}

/// <summary>Estado operativo actual de un vehículo.</summary>
public enum VehicleStatus
{
    Available,
    EnRoute,
    Maintenance,
    OutOfService
}

/// <summary>Estado del ciclo de vida de una ruta de reparto.</summary>
public enum DeliveryRouteStatus
{
    Planned,
    InProgress,
    Delayed,
    Completed,
    Cancelled
}

/// <summary>Prioridad de un artículo de carga dentro de una ruta.</summary>
public enum CargoPriority
{
    Standard,
    High,
    Urgent,
    Fragile
}

/// <summary>Tipo de tarea de mantenimiento programada o realizada.</summary>
public enum MaintenanceType
{
    OilChange,
    TireRotation,
    BrakeInspection,
    GeneralInspection,
    Repair,
    Other
}

/// <summary>Tipo de incidente reportado por un operador durante un viaje.</summary>
public enum AlertType
{
    Delay,
    Breakdown,
    TrafficJam,
    Accident,
    WeatherCondition,
    Other
}

/// <summary>Estado de atención de una alerta reportada.</summary>
public enum AlertStatus
{
    Open,
    Acknowledged,
    Resolved
}

/// <summary>Rol de un usuario dentro del sistema (control de acceso).</summary>
public enum UserRole
{
    Admin,
    Operator
}
