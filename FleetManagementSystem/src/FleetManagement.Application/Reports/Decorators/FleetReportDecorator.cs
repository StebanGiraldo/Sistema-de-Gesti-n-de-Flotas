using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Reports.Decorators;

/// <summary>
/// DECORATOR base (patrón estructural DECORATOR): ES un <see cref="IFleetReportGenerator"/> y además TIENE
/// otro generador envuelto (<see cref="Inner"/>), que puede ser el generador base u otro decorador.
///
/// Fija el esqueleto común para que cada decorador concreto solo escriba lo suyo: pide el reporte al
/// generador envuelto, deja que el decorador concreto le añada SU sección (<see cref="AddSectionAsync"/>) y
/// anota el nombre de esa sección en los metadatos. Como toda la información sale del reporte que ya trae el
/// generador envuelto, un decorador no repite las consultas del generador base.
/// </summary>
public abstract class FleetReportDecorator : IFleetReportGenerator
{
    protected FleetReportDecorator(IFleetReportGenerator inner)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <summary>Generador envuelto: el reporte base o el decorador que se aplicó antes.</summary>
    protected IFleetReportGenerator Inner { get; }

    /// <summary>Nombre de la sección que aporta este decorador; coincide con la propiedad homónima de <see cref="FleetReportDto"/>.</summary>
    protected abstract string SectionName { get; }

    public async Task<FleetReportDto> GenerateAsync()
    {
        var report = await Inner.GenerateAsync();
        var decorated = await AddSectionAsync(report);

        return decorated with
        {
            Metadata = decorated.Metadata with
            {
                IncludedSections = decorated.Metadata.IncludedSections.Append(SectionName).ToList()
            }
        };
    }

    /// <summary>Devuelve el reporte recibido con la sección de este decorador completada. No debe tocar lo demás.</summary>
    protected abstract Task<FleetReportDto> AddSectionAsync(FleetReportDto report);
}
