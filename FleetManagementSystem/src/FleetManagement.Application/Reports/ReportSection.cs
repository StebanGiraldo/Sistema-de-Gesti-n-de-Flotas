namespace FleetManagement.Application.Reports;

/// <summary>
/// Secciones opcionales que se pueden pedir a <c>GET api/reports/fleet</c>. Cada una corresponde a UN
/// decorador (ver <c>FleetReportService</c>) y su nombre coincide con la propiedad homónima de
/// <c>FleetReportDto</c>.
/// </summary>
public enum ReportSection
{
    /// <summary>Estadísticas generales de la flota (<c>StatisticsReportDecorator</c>).</summary>
    Statistics,

    /// <summary>Mantenimientos vencidos (<c>MaintenanceAlertsReportDecorator</c>).</summary>
    MaintenanceAlerts,

    /// <summary>Totales de carga por ruta y generales (<c>CargoSummaryReportDecorator</c>).</summary>
    CargoSummary
}
