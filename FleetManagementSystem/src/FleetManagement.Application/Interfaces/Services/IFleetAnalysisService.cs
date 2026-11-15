using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Interfaces.Services;

/// <summary>
/// Análisis de la flota con el patrón Visitor (ver <c>Application/Analysis</c>): utilización de vehículos, recorridos y
/// distancias, historial de mantenimiento y hallazgos operativos. Es independiente de los reportes (<see cref="IFleetReportService"/>,
/// patrón Decorator): ese compone secciones de un reporte y este ejecuta operaciones de análisis sobre los elementos de la flota.
/// </summary>
public interface IFleetAnalysisService
{
    /// <summary>Analiza los vehículos, las rutas y los mantenimientos que hay en este momento y entrega el resultado de cada análisis.</summary>
    Task<FleetAnalysisDto> AnalyzeFleetAsync();
}
