namespace FleetManagement.Application.DTOs;

// Resultados del análisis de la flota con el patrón Visitor (ver Application/Analysis). Cada visitante concreto entrega
// directamente uno de estos registros. Los números van redondeados a la precisión que ya usa el resto de la API
// (3 decimales en kilómetros, 1 en porcentajes), y los diccionarios/listas traen siempre todos los valores del enum
// (con 0 donde no hay nada) para que el cliente reciba siempre la misma forma.

/// <summary>Uso de un vehículo: cuántas rutas tiene asignadas y si está en uso ahora.</summary>
/// <param name="RoutesAssigned">Rutas asignadas al vehículo, de cualquier estado.</param>
/// <param name="RoutesCompleted">De ellas, las ya completadas.</param>
/// <param name="InUse">Verdadero si el vehículo puede operar y tiene una ruta en curso.</param>
public record VehicleUsageDto(
    Guid VehicleId,
    string LicensePlate,
    string Type,
    string Status,
    double MileageKm,
    int RoutesAssigned,
    int RoutesCompleted,
    bool InUse);

/// <summary>Uso de la flota por tipo de vehículo (aparecen siempre todos los tipos).</summary>
public record VehicleTypeUsageDto(string Type, int Vehicles, int InUse, double AverageMileageKm);

/// <summary>
/// Resumen de utilización de los vehículos. "Operables" son los que su estado deja operar (Disponible o En ruta);
/// "en uso" son los operables con una ruta en curso; la utilización es en uso / operables.
/// </summary>
public record VehicleUtilizationDto(
    int TotalVehicles,
    int OperableVehicles,
    int InUseVehicles,
    double UtilizationPercent,
    double TotalMileageKm,
    double AverageMileageKm,
    IReadOnlyList<VehicleTypeUsageDto> ByType,
    IReadOnlyList<VehicleUsageDto> Vehicles);

/// <summary>Una ruta con su distancia estimada, sus paradas intermedias y su retraso.</summary>
public record RouteDistanceDto(
    Guid RouteId,
    string Name,
    string Status,
    double DistanceKm,
    int Stops,
    double EstimatedDurationMinutes,
    int DelayMinutes);

/// <summary>Cantidad de rutas y distancia acumulada de un estado de ruta (aparecen siempre todos los estados).</summary>
public record RouteStatusDistanceDto(string Status, int Routes, double DistanceKm);

/// <summary>
/// Análisis de recorridos y distancias. Los totales y promedios NO incluyen las rutas canceladas (no se recorren); el
/// desglose por estado sí las muestra aparte.
/// </summary>
public record RouteDistanceAnalysisDto(
    int TotalRoutes,
    double TotalDistanceKm,
    double CompletedDistanceKm,
    double ActiveDistanceKm,
    double PlannedDistanceKm,
    double AverageDistanceKm,
    RouteDistanceDto? LongestRoute,
    int DelayedRoutes,
    int TotalDelayMinutes,
    IReadOnlyList<RouteStatusDistanceDto> ByStatus,
    IReadOnlyList<RouteDistanceDto> Routes);

/// <summary>Cantidad de mantenimientos de un tipo (aparecen siempre todos los tipos).</summary>
public record MaintenanceTypeCountDto(string Type, int Records);

/// <summary>Historial de un vehículo con mantenimientos: cuántos, el último y los kilómetros recorridos desde él.</summary>
public record VehicleMaintenanceSummaryDto(
    Guid VehicleId,
    string LicensePlate,
    int Records,
    DateTime LastServiceAt,
    string LastServiceType,
    double MileageKm,
    double KmSinceLastService);

/// <summary>
/// Análisis de los registros de mantenimiento. No repite el módulo predictivo (qué está vencido): describe el historial.
/// </summary>
/// <param name="AverageKmBetweenServices">Promedio de kilómetros entre mantenimientos consecutivos de un mismo vehículo, o <c>null</c> si ningún vehículo tiene dos o más.</param>
public record MaintenanceHistoryAnalysisDto(
    int TotalRecords,
    IReadOnlyList<MaintenanceTypeCountDto> ByType,
    int VehiclesWithRecords,
    IReadOnlyList<string> VehiclesWithoutRecords,
    double? AverageKmBetweenServices,
    IReadOnlyList<VehicleMaintenanceSummaryDto> Vehicles);

/// <summary>Un hallazgo que merece atención del operador. <see cref="Code"/> es estable; <see cref="Message"/> es para leer.</summary>
/// <param name="Severity">Critical, Warning o Info.</param>
/// <param name="Subject">A quién se refiere: la placa del vehículo o el nombre de la ruta.</param>
public record OperationalInsightDto(string Code, string Severity, string Subject, string Message);

/// <summary>Cuántos elementos de cada tipo se analizaron.</summary>
public record AnalyzedElementsDto(int Vehicles, int Routes, int MaintenanceRecords);

/// <summary>Análisis completo de la flota: lo que entrega cada visitante sobre los mismos elementos.</summary>
public record FleetAnalysisDto(
    DateTimeOffset GeneratedAt,
    AnalyzedElementsDto Elements,
    VehicleUtilizationDto Utilization,
    RouteDistanceAnalysisDto Routes,
    MaintenanceHistoryAnalysisDto Maintenance,
    IReadOnlyList<OperationalInsightDto> Insights);
