using FleetManagement.Application.DTOs;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Metrics;

/// <summary>
/// Cálculos puros (sin estado, sin acceso a datos) sobre los DTO que ya entregan los servicios
/// de la aplicación. Existe para que el reporte (<c>StatisticsReportDecorator</c>,
/// <c>CargoSummaryReportDecorator</c>) y el resumen del dashboard (<c>FleetDashboardFacade</c>)
/// den EXACTAMENTE las mismas cifras sin repetir la lógica en cada uno.
/// </summary>
public static class FleetMetrics
{
    /// <summary>Estadísticas generales a partir de los vehículos y las rutas ya consultados.</summary>
    public static FleetStatisticsDto Statistics(IReadOnlyCollection<VehicleDto> vehicles, IReadOnlyCollection<DeliveryRouteDto> routes)
    {
        ArgumentNullException.ThrowIfNull(vehicles);
        ArgumentNullException.ThrowIfNull(routes);

        var available = vehicles.Count(v => v.Status == nameof(VehicleStatus.Available));

        return new FleetStatisticsDto(
            vehicles.Count,
            CountByStatus<VehicleStatus>(vehicles.Select(v => v.Status)),
            vehicles.Count == 0 ? 0 : Math.Round(100.0 * available / vehicles.Count, 1),
            vehicles.Count == 0 ? 0 : Math.Round(vehicles.Average(v => v.MileageKm), 3),
            Math.Round(vehicles.Sum(v => v.CapacityKg), 3),
            routes.Count,
            CountByStatus<DeliveryRouteStatus>(routes.Select(r => r.Status)),
            routes.Sum(r => r.DelayMinutes));
    }

    /// <summary>Cantidad, peso y volumen de los artículos recibidos (3 decimales, como el manifiesto Composite).</summary>
    public static CargoTotalsDto CargoTotals(IEnumerable<CargoItemDto> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var list = items.ToList();
        return new CargoTotalsDto(
            list.Count,
            Math.Round(list.Sum(i => i.WeightKg), 3),
            Math.Round(list.Sum(i => i.VolumeM3), 3));
    }

    /// <summary>Mantenimientos vencidos: los elementos que el módulo predictivo marca con <c>IsOverdue</c>.</summary>
    public static int OverdueMaintenance(IEnumerable<MaintenanceDueDto> due)
    {
        ArgumentNullException.ThrowIfNull(due);
        return due.Count(d => d.IsOverdue);
    }

    /// <summary>
    /// Cuenta cuántos elementos hay en cada estado de <typeparamref name="TStatus"/>. Devuelve SIEMPRE todos
    /// los estados del enum, en el orden en que se declaran, con 0 donde no hay ninguno.
    /// </summary>
    public static IReadOnlyDictionary<string, int> CountByStatus<TStatus>(IEnumerable<string> statuses) where TStatus : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(statuses);

        var counts = statuses.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
        return Enum.GetNames<TStatus>().ToDictionary(name => name, name => counts.GetValueOrDefault(name));
    }
}
