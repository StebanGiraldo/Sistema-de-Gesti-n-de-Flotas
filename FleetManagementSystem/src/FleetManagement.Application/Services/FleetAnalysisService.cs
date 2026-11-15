using FleetManagement.Application.Analysis;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Domain.States;

namespace FleetManagement.Application.Services;

/// <summary>
/// Caso de uso del análisis de la flota (patrón VISITOR). Lee vehículos, rutas y mantenimientos, los adapta a elementos
/// analizables (<see cref="FleetElementStructure"/>) y le pasa a esa estructura un visitante por cada análisis. No contiene
/// ningún cálculo: cada cálculo vive en su visitante, y las entidades no se modifican. Solo lee: no cambia ningún dato.
///
/// Es independiente del Decorator de reportes: no usa <c>IFleetReportGenerator</c> ni sus secciones, y los reportes no saben que
/// este análisis existe. Crea los visitantes en cada llamada (acumulan estado mientras recorren: compartirlos mezclaría
/// resultados de peticiones distintas).
/// </summary>
public class FleetAnalysisService : IFleetAnalysisService
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IDeliveryRouteRepository _routeRepository;
    private readonly IMaintenanceRepository _maintenanceRepository;
    private readonly IVehicleStateResolver _vehicleStates;
    private readonly TimeProvider _clock;
    private readonly InsightThresholds _thresholds;

    public FleetAnalysisService(
        IVehicleRepository vehicleRepository,
        IDeliveryRouteRepository routeRepository,
        IMaintenanceRepository maintenanceRepository,
        IVehicleStateResolver vehicleStates,
        TimeProvider clock,
        InsightThresholds? thresholds = null)
    {
        _vehicleRepository = vehicleRepository;
        _routeRepository = routeRepository;
        _maintenanceRepository = maintenanceRepository;
        _vehicleStates = vehicleStates;
        _clock = clock;
        _thresholds = thresholds ?? new InsightThresholds();
    }

    public async Task<FleetAnalysisDto> AnalyzeFleetAsync()
    {
        var vehicles = await _vehicleRepository.GetAllAsync();
        var routes = await _routeRepository.GetAllAsync();
        var records = await _maintenanceRepository.GetAllAsync();

        var structure = FleetElementStructure.Build(vehicles, routes, records);

        return new FleetAnalysisDto(
            _clock.GetUtcNow(),
            new AnalyzedElementsDto(structure.Count<VehicleElement>(), structure.Count<RouteElement>(), structure.Count<MaintenanceRecordElement>()),
            structure.Analyze(new VehicleUtilizationVisitor(_vehicleStates)),
            structure.Analyze(new RouteDistanceVisitor()),
            structure.Analyze(new MaintenanceHistoryVisitor()),
            structure.Analyze(new OperationalInsightsVisitor(_vehicleStates, _thresholds)));
    }
}
