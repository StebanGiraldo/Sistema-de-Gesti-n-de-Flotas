using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Interfaces.Services;

public interface IFleetReportService
{
    /// <summary>
    /// Genera el reporte de la flota. Sin secciones devuelve el reporte base (metadatos, vehículos y rutas); cada
    /// sección pedida (Statistics, MaintenanceAlerts, CargoSummary; sin distinguir mayúsculas, repetidas o separadas
    /// por comas) aplica el decorador correspondiente, en el orden en que se piden.
    /// </summary>
    /// <exception cref="ArgumentException">Si alguna sección pedida no existe.</exception>
    Task<FleetReportDto> GenerateFleetReportAsync(IEnumerable<string>? sections);
}
