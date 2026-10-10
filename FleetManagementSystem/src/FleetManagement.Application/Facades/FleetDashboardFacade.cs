using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Metrics;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Facades;

/// <summary>
/// FACADE (patrón estructural): coordina los servicios existentes de vehículos, rutas, mantenimiento y alertas
/// para armar el resumen del dashboard en una sola operación. NO reemplaza a esos servicios (siguen
/// siendo la única fuente de cada dato y de sus reglas) y no contiene lógica de negocio propia: consulta,
/// delega los cálculos en <see cref="FleetMetrics"/> y compone el <see cref="FleetSummaryDto"/>.
///
/// Los errores no se capturan: si un subsistema falla, la excepción llega al controlador tal cual, sin
/// ocultarla ni sustituirla por un resumen con datos inventados. Las consultas se hacen una tras otra (no en
/// paralelo) porque los servicios comparten el ámbito de la petición.
/// </summary>
public class FleetDashboardFacade : IFleetDashboardFacade
{
    private readonly IVehicleService _vehicleService;
    private readonly IDeliveryRouteService _routeService;
    private readonly IMaintenanceService _maintenanceService;
    private readonly ITripAlertService _alertService;

    public FleetDashboardFacade(
        IVehicleService vehicleService,
        IDeliveryRouteService routeService,
        IMaintenanceService maintenanceService,
        ITripAlertService alertService)
    {
        _vehicleService = vehicleService;
        _routeService = routeService;
        _maintenanceService = maintenanceService;
        _alertService = alertService;
    }

    public async Task<FleetSummaryDto> GetSummaryAsync()
    {
        var vehicles = await _vehicleService.GetAllVehiclesAsync();
        var routes = await _routeService.GetAllRoutesAsync();
        var maintenanceDue = await _maintenanceService.GetVehiclesDueForMaintenanceAsync();
        var alerts = await _alertService.GetAllAsync();

        return new FleetSummaryDto(
            DateTime.UtcNow,
            FleetMetrics.Statistics(vehicles, routes),
            FleetMetrics.CargoTotals(routes.SelectMany(route => route.CargoItems)),
            FleetMetrics.OverdueMaintenance(maintenanceDue),
            FleetMetrics.CountByStatus<AlertStatus>(alerts.Select(alert => alert.Status)));
    }
}
