using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;

namespace FleetManagement.Application.Reports;

/// <summary>
/// CONCRETE COMPONENT (patrón DECORATOR): genera el reporte BASE de la flota con lo que ya
/// exponen los servicios existentes: metadatos, vehículos (estado y disponibilidad) y rutas
/// (estado, vehículo y conductor asignados, carga). No calcula nada ni consulta nada más: las
/// secciones adicionales las aportan los decoradores, y este generador no sabe que existen.
/// </summary>
public sealed class FleetReportGenerator : IFleetReportGenerator
{
    public const string FleetReportType = "Fleet";

    private readonly IVehicleService _vehicleService;
    private readonly IDeliveryRouteService _routeService;

    public FleetReportGenerator(IVehicleService vehicleService, IDeliveryRouteService routeService)
    {
        _vehicleService = vehicleService;
        _routeService = routeService;
    }

    public async Task<FleetReportDto> GenerateAsync()
    {
        var vehicles = await _vehicleService.GetAllVehiclesAsync();
        var routes = await _routeService.GetAllRoutesAsync();

        var metadata = new ReportMetadataDto(
            FleetReportType,
            DateTime.UtcNow,
            new[] { nameof(FleetReportDto.Vehicles), nameof(FleetReportDto.Routes) });

        return new FleetReportDto(metadata, vehicles, routes);
    }
}
