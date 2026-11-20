using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;

namespace FleetManagement.Application.RouteSheets;

/// <summary>
/// Colaboradores que la plantilla recibe por composición (un solo parámetro en vez de tres): quien carga y valida los datos,
/// la navegación y el avance de los viajes, los tres por sus interfaces existentes. La herencia queda solo para el esqueleto.
/// </summary>
public sealed record RouteSheetCollaborators(
    IRouteSheetSourceLoader Source,
    INavigationService Navigation,
    ITripProgressService Progress);

/// <summary>
/// PATRÓN TEMPLATE METHOD. <see cref="BuildAsync"/> es el esqueleto: siempre los mismos pasos y en el mismo orden, y ninguna
/// variante puede cambiarlo (no es virtual).
/// <code>
/// cargar y validar (fijo) → encabezado → paradas → carga → avance → navegación → ensamblar (fijo)
/// </code>
/// Los pasos fijos viven aquí; los cinco que cambian son miembros <c>protected</c>. Dos son abstractos porque toda audiencia
/// debe decidirlos (encabezado, avance). Tres son «hooks» con un valor por defecto válido porque «no aportar nada» es un
/// resultado legítimo para alguna audiencia (el conductor no necesita el resumen de carga; el despachador no necesita las
/// paradas ni el enlace de navegación): ninguna subclase queda con un método vacío ni lanza <see cref="NotSupportedException"/>,
/// y ninguna está obligada a llamar a la base.
/// Es un patrón distinto del Template Method de <c>Application/Commands/RouteCreationCommand</c>, que no se tocó.
/// </summary>
public abstract class RouteSheetTemplate
{
    private readonly RouteSheetCollaborators _collaborators;

    protected RouteSheetTemplate(RouteSheetAudience audience, RouteSheetCollaborators collaborators)
    {
        Audience = audience;
        _collaborators = collaborators;
    }

    /// <summary>A quién sirve esta plantilla; el proveedor la elige con este valor.</summary>
    public RouteSheetAudience Audience { get; }

    protected INavigationService Navigation => _collaborators.Navigation;

    /// <summary>
    /// Arma la hoja de ruta. Si la ruta no existe o no tiene vehículo, falla en el primer paso: ningún paso variable llega a ejecutarse.
    /// </summary>
    public async Task<RouteSheetDto> BuildAsync(Guid routeId)
    {
        var source = await LoadAsync(routeId);
        var header = BuildHeader(source);
        var stops = BuildStops(source);
        var cargo = BuildCargo(source);
        var progress = BuildProgress(source);
        var navigation = await BuildNavigationAsync(source);
        return new RouteSheetDto(Audience.ToString(), header, stops, cargo, progress, navigation);
    }

    /// <summary>Encabezado de la hoja (todas las audiencias tienen uno, pero con contenido distinto).</summary>
    protected abstract RouteSheetHeader BuildHeader(RouteSheetSource source);

    /// <summary>Avance del viaje según lo que la audiencia necesita; <c>null</c> si el viaje no está en curso y no hay nada que decir.</summary>
    protected abstract RouteSheetProgress? BuildProgress(RouteSheetSource source);

    /// <summary>Paradas en orden. Por defecto ninguna.</summary>
    protected virtual IReadOnlyList<TripStopDto> BuildStops(RouteSheetSource source) => Array.Empty<TripStopDto>();

    /// <summary>Resumen de la carga. Por defecto, ninguno.</summary>
    protected virtual RouteSheetCargo? BuildCargo(RouteSheetSource source) => null;

    /// <summary>Enlace de navegación externa. Por defecto, ninguno.</summary>
    protected virtual Task<string?> BuildNavigationAsync(RouteSheetSource source) => Task.FromResult<string?>(null);

    // Paso fijo. El avance se pide una vez aquí porque todas las audiencias lo usan; qué se cuenta de él lo decide cada variante.
    private async Task<RouteSheetSource> LoadAsync(Guid routeId)
    {
        var source = await _collaborators.Source.LoadAsync(routeId);
        var activeTrips = await _collaborators.Progress.GetActiveTripsAsync();
        return source with { Trip = activeTrips.FirstOrDefault(trip => trip.RouteId == routeId) };
    }
}
