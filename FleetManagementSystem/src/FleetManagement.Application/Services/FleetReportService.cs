using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Reports;
using FleetManagement.Application.Reports.Decorators;

namespace FleetManagement.Application.Services;

/// <summary>
/// Caso de uso de reportes de la flota. Traduce las secciones pedidas por el cliente en una cadena de
/// decoradores (patrón DECORATOR) apilada sobre el generador base y la ejecuta. Es el único lugar que conoce
/// qué decorador corresponde a cada sección: ni el generador base ni los decoradores se conocen entre sí.
///
/// Se registra en DI solo el generador base (<see cref="IFleetReportGenerator"/>); los decoradores se crean
/// aquí, en cada petición, porque su combinación depende de lo que el cliente pide.
/// </summary>
public class FleetReportService : IFleetReportService
{
    private readonly IFleetReportGenerator _baseGenerator;
    private readonly IMaintenanceService _maintenanceService;

    public FleetReportService(IFleetReportGenerator baseGenerator, IMaintenanceService maintenanceService)
    {
        _baseGenerator = baseGenerator;
        _maintenanceService = maintenanceService;
    }

    public async Task<FleetReportDto> GenerateFleetReportAsync(IEnumerable<string>? sections)
    {
        var generator = _baseGenerator;
        foreach (var section in ParseSections(sections))
            generator = Decorate(generator, section);

        return await generator.GenerateAsync();
    }

    /// <summary>Envuelve el generador con el decorador de la sección pedida. El primero que se aplica queda más adentro.</summary>
    private IFleetReportGenerator Decorate(IFleetReportGenerator generator, ReportSection section) => section switch
    {
        ReportSection.Statistics => new StatisticsReportDecorator(generator),
        ReportSection.MaintenanceAlerts => new MaintenanceAlertsReportDecorator(generator, _maintenanceService),
        ReportSection.CargoSummary => new CargoSummaryReportDecorator(generator),
        _ => throw new InvalidOperationException($"La sección de reporte '{section}' no tiene un decorador asociado.")
    };

    /// <summary>
    /// Acepta las secciones repetidas (<c>sections=A&amp;sections=B</c>) o separadas por comas (<c>sections=A,B</c>), sin
    /// distinguir mayúsculas. Ignora entradas vacías y secciones duplicadas; solo vale el NOMBRE de la sección (no su número).
    /// </summary>
    private static IReadOnlyList<ReportSection> ParseSections(IEnumerable<string>? requested)
    {
        var parsed = new List<ReportSection>();
        var tokens = (requested ?? Enumerable.Empty<string>())
            .SelectMany(entry => entry.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        foreach (var token in tokens)
        {
            var name = Enum.GetNames<ReportSection>().FirstOrDefault(n => string.Equals(n, token, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException(
                    $"Sección de reporte inválida: '{token}'. Valores válidos: {string.Join(", ", Enum.GetNames<ReportSection>())}.");

            var section = Enum.Parse<ReportSection>(name);
            if (!parsed.Contains(section))
                parsed.Add(section);
        }

        return parsed;
    }
}
