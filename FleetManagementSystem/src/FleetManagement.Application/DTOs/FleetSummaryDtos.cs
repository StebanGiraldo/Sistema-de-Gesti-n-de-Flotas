namespace FleetManagement.Application.DTOs;

/// <summary>
/// Resumen general de la flota para el dashboard (respuesta consolidada del patrón FACADE).
/// Reúne en una sola respuesta lo que hoy exige consultar vehículos, rutas, mantenimiento
/// y alertas por separado.
/// </summary>
/// <param name="GeneratedAtUtc">Momento en que se armó el resumen (UTC).</param>
/// <param name="Statistics">Vehículos y rutas por estado, disponibilidad, kilometraje y capacidad.</param>
/// <param name="Cargo">Carga registrada en todas las rutas, cualquiera sea su estado.</param>
/// <param name="OverdueMaintenanceCount">Mantenimientos vencidos según el módulo predictivo.</param>
/// <param name="AlertsByStatus">Alertas de viaje por estado (Open, Acknowledged, Resolved).</param>
public record FleetSummaryDto(
    DateTime GeneratedAtUtc,
    FleetStatisticsDto Statistics,
    CargoTotalsDto Cargo,
    int OverdueMaintenanceCount,
    IReadOnlyDictionary<string, int> AlertsByStatus);
