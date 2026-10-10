using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Reports;

/// <summary>
/// COMPONENT (patrón estructural DECORATOR, aplicado a la generación de reportes).
///
/// Contrato común de "algo que genera el reporte de la flota". Lo implementan tanto el generador
/// base (<see cref="FleetReportGenerator"/>) como todos los decoradores
/// (<see cref="Decorators.FleetReportDecorator"/>), así que quien pide el reporte no sabe ni le importa
/// si tiene delante el generador original o uno envuelto por varios decoradores.
///
/// Es una jerarquía independiente de la del Decorator de notificaciones
/// (<c>FleetManagement.Application.Notifications.Decorators</c>): no comparten tipos.
/// </summary>
public interface IFleetReportGenerator
{
    Task<FleetReportDto> GenerateAsync();
}
