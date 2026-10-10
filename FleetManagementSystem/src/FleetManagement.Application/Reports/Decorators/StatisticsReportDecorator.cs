using FleetManagement.Application.DTOs;
using FleetManagement.Application.Metrics;

namespace FleetManagement.Application.Reports.Decorators;

/// <summary>
/// DECORATOR concreto: añade al reporte las estadísticas generales de la flota (vehículos y rutas por estado,
/// disponibilidad, kilometraje medio, capacidad total y minutos de retraso). Las calcula con los vehículos y
/// rutas que YA trae el reporte envuelto (<see cref="FleetMetrics"/>), así que no hace ninguna consulta nueva.
/// </summary>
public sealed class StatisticsReportDecorator : FleetReportDecorator
{
    public StatisticsReportDecorator(IFleetReportGenerator inner) : base(inner) { }

    protected override string SectionName => nameof(FleetReportDto.Statistics);

    protected override Task<FleetReportDto> AddSectionAsync(FleetReportDto report)
        => Task.FromResult(report with { Statistics = FleetMetrics.Statistics(report.Vehicles, report.Routes) });
}
