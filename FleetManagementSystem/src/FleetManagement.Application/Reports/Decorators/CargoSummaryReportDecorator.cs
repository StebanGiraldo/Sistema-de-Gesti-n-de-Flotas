using FleetManagement.Application.DTOs;
using FleetManagement.Application.Metrics;

namespace FleetManagement.Application.Reports.Decorators;

/// <summary>
/// DECORATOR concreto: añade al reporte los totales de carga (artículos, peso y volumen) por ruta y en
/// general. Parte de los artículos que ya vienen dentro de cada ruta del reporte envuelto, así que no hace
/// consultas nuevas, y solo lista las rutas que realmente llevan carga. Usa el mismo redondeo que el manifiesto
/// jerárquico del patrón Composite (<c>GET api/routes/{id}/cargo-manifest</c>), por lo que ambas cifras coinciden.
/// </summary>
public sealed class CargoSummaryReportDecorator : FleetReportDecorator
{
    public CargoSummaryReportDecorator(IFleetReportGenerator inner) : base(inner) { }

    protected override string SectionName => nameof(FleetReportDto.CargoSummary);

    protected override Task<FleetReportDto> AddSectionAsync(FleetReportDto report)
    {
        var routesWithCargo = report.Routes
            .Where(route => route.CargoItems.Count > 0)
            .Select(route => new RouteCargoDto(route.Id, route.Name, route.Status, FleetMetrics.CargoTotals(route.CargoItems)))
            .ToList();

        var total = FleetMetrics.CargoTotals(report.Routes.SelectMany(route => route.CargoItems));

        return Task.FromResult(report with { CargoSummary = new CargoSummaryDto(total, routesWithCargo) });
    }
}
