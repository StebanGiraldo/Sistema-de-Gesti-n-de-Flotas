using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Metrics;

namespace FleetManagement.Application.Reports.Decorators;

/// <summary>
/// DECORATOR concreto: añade al reporte los mantenimientos vencidos. Es la única fuente de datos nueva entre los
/// decoradores: pide la lista a <see cref="IMaintenanceService"/> (el módulo predictivo que ya decide qué está
/// vencido, por fecha o por kilometraje) y la incluye tal cual, sin reimplementar esa regla.
/// </summary>
public sealed class MaintenanceAlertsReportDecorator : FleetReportDecorator
{
    private readonly IMaintenanceService _maintenanceService;

    public MaintenanceAlertsReportDecorator(IFleetReportGenerator inner, IMaintenanceService maintenanceService) : base(inner)
    {
        _maintenanceService = maintenanceService ?? throw new ArgumentNullException(nameof(maintenanceService));
    }

    protected override string SectionName => nameof(FleetReportDto.MaintenanceAlerts);

    protected override async Task<FleetReportDto> AddSectionAsync(FleetReportDto report)
    {
        var due = await _maintenanceService.GetVehiclesDueForMaintenanceAsync();
        return report with { MaintenanceAlerts = new MaintenanceAlertsSectionDto(FleetMetrics.OverdueMaintenance(due), due) };
    }
}
