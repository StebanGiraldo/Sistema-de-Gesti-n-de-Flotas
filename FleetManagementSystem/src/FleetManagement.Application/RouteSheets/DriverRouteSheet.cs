using FleetManagement.Application.DTOs;
using FleetManagement.Application.Simulation;

namespace FleetManagement.Application.RouteSheets;

/// <summary>
/// Hoja para el conductor: qué recorrer y cómo seguir. Aporta las paradas en orden (origen, paradas intermedias y destino, con
/// las mismas etiquetas que el avance del viaje), el enlace de navegación externa que ya genera <c>INavigationService</c> y,
/// si el viaje está en curso, la próxima parada con su hora estimada de llegada. No aporta resumen de carga.
/// </summary>
public sealed class DriverRouteSheet : RouteSheetTemplate
{
    private const string Title = "Hoja de ruta del conductor";

    public DriverRouteSheet(RouteSheetCollaborators collaborators)
        : base(RouteSheetAudience.Driver, collaborators)
    {
    }

    protected override RouteSheetHeader BuildHeader(RouteSheetSource source) => RouteSheetHeader.From(source, Title);

    protected override IReadOnlyList<TripStopDto> BuildStops(RouteSheetSource source)
        => RoutePath.From(source.Route).Stops
            .Select(stop => new TripStopDto(stop.Label, stop.Location.Latitude, stop.Location.Longitude))
            .ToList();

    protected override RouteSheetProgress? BuildProgress(RouteSheetSource source)
        => source.Trip is { } trip
            ? new RouteSheetProgress(RemainingKm: trip.RemainingKm, NextStop: trip.NextStopLabel, EtaMinutes: trip.EtaMinutes)
            : null;

    protected override async Task<string?> BuildNavigationAsync(RouteSheetSource source)
        => (await Navigation.GenerateExternalNavigationLinkAsync(source.Route.Id)).ExternalMapsUrl;
}
