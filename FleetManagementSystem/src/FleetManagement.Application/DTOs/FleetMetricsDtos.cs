namespace FleetManagement.Application.DTOs;

/// <summary>
/// Estadísticas generales de la flota. Se calculan sobre los DTO que ya
/// entregan <c>IVehicleService</c> e <c>IDeliveryRouteService</c> (sin consultas
/// adicionales). Las claves de los diccionarios son los nombres de
/// <c>VehicleStatus</c> / <c>DeliveryRouteStatus</c>; aparecen todas, aunque
/// su cantidad sea 0, para que el cliente reciba siempre la misma forma.
/// Lo comparten el reporte (<c>StatisticsReportDecorator</c>) y el resumen del
/// dashboard (<c>FleetDashboardFacade</c>) para no calcular lo mismo dos veces.
/// </summary>
public record FleetStatisticsDto(
    int TotalVehicles,
    IReadOnlyDictionary<string, int> VehiclesByStatus,
    double AvailabilityPercent,
    double AverageMileageKm,
    double TotalCapacityKg,
    int TotalRoutes,
    IReadOnlyDictionary<string, int> RoutesByStatus,
    int TotalDelayMinutes);

/// <summary>
/// Totales de carga: cantidad de artículos y peso/volumen acumulados. Se
/// redondean a 3 decimales, igual que el manifiesto del patrón Composite
/// (<c>CargoManifestDto</c>), para que ambas vistas coincidan.
/// </summary>
public record CargoTotalsDto(int ItemCount, double TotalWeightKg, double TotalVolumeM3);
