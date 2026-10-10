namespace FleetManagement.Application.DTOs;

/// <summary>
/// Metadatos del reporte: tipo, fecha de generación (UTC) y nombres de las
/// secciones que contiene, en el orden en que se fueron añadiendo
/// (primero las del reporte base y luego una por cada decorador aplicado).
/// Los nombres coinciden con las propiedades de <see cref="FleetReportDto"/>.
/// </summary>
public record ReportMetadataDto(string ReportType, DateTime GeneratedAtUtc, IReadOnlyList<string> IncludedSections);

/// <summary>
/// Mantenimientos vencidos de la flota. Reutiliza <see cref="MaintenanceDueDto"/>, el resultado
/// del módulo predictivo (<c>IMaintenanceService.GetVehiclesDueForMaintenanceAsync</c>); el
/// sistema solo expone los mantenimientos ya vencidos, no los "próximos a vencer".
/// </summary>
public record MaintenanceAlertsSectionDto(int OverdueCount, IReadOnlyList<MaintenanceDueDto> Items);

/// <summary>Totales de carga de una ruta, con su estado para que quien lea el reporte pueda filtrarla.</summary>
public record RouteCargoDto(Guid RouteId, string RouteName, string RouteStatus, CargoTotalsDto Cargo);

/// <summary>
/// Carga registrada en las rutas del reporte (cualquiera sea su estado): total general y
/// detalle por ruta. Solo aparecen las rutas que tienen artículos de carga.
/// </summary>
public record CargoSummaryDto(CargoTotalsDto Total, IReadOnlyList<RouteCargoDto> Routes);

/// <summary>
/// Reporte de la flota (patrón DECORATOR). El reporte base trae metadatos, vehículos y rutas
/// (con los mismos DTO que ya expone la API); cada decorador aplicado completa UNA de las secciones
/// opcionales. Una sección no solicitada es <c>null</c> y no se serializa (la API ignora los nulos).
/// </summary>
public record FleetReportDto(
    ReportMetadataDto Metadata,
    IReadOnlyList<VehicleDto> Vehicles,
    IReadOnlyList<DeliveryRouteDto> Routes,
    FleetStatisticsDto? Statistics = null,
    MaintenanceAlertsSectionDto? MaintenanceAlerts = null,
    CargoSummaryDto? CargoSummary = null);
