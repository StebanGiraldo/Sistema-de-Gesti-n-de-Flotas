namespace FleetManagement.Application.RouteSheets;

/// <summary>
/// Hoja para quien despacha: cómo va la operación. Aporta el resumen de la carga frente a la capacidad del vehículo, el retraso
/// de la ruta y el avance (porcentaje y kilómetros que faltan, si el viaje está en curso) y el nombre del conductor en el
/// encabezado. No aporta paradas ni enlace de navegación.
/// </summary>
public sealed class DispatcherRouteSheet : RouteSheetTemplate
{
    private const string Title = "Hoja de despacho";

    public DispatcherRouteSheet(RouteSheetCollaborators collaborators)
        : base(RouteSheetAudience.Dispatcher, collaborators)
    {
    }

    protected override RouteSheetHeader BuildHeader(RouteSheetSource source)
        => RouteSheetHeader.From(source, Title) with { DriverName = source.Driver?.FullName };

    protected override RouteSheetCargo? BuildCargo(RouteSheetSource source)
        => CargoCapacitySummary.Of(source.Route.CargoItems, source.Vehicle);

    protected override RouteSheetProgress? BuildProgress(RouteSheetSource source)
        => new RouteSheetProgress(
            Percent: source.Trip?.Percent,
            RemainingKm: source.Trip?.RemainingKm,
            DelayMinutes: source.Route.DelayMinutes);
}
