using System.ComponentModel.DataAnnotations;

namespace FleetManagement.Infrastructure.Navigation;

/// <summary>
/// Parámetros del caché del proxy de navegación (<see cref="CachingNavigationProviderProxy"/>). Se leen de la sección
/// <c>"NavigationCache"</c> de la configuración con el patrón Options (<c>IOptions&lt;T&gt;</c>) y se validan al arrancar
/// (<c>ValidateOnStart</c>, ver Program.cs): un valor imposible detiene el arranque con un mensaje claro. Así el código
/// del caché no tiene números mágicos: cada valor tiene nombre, valor por defecto y una regla de validez.
/// </summary>
public sealed class NavigationCacheOptions
{
    public const string SectionName = "NavigationCache";

    // Una semana: más allá de eso un tiempo de vida deja de ser un caché y sería, en la práctica, un error de configuración.
    private const double MaxTimeToLiveMinutes = 60 * 24 * 7;

    /// <summary>
    /// Minutos que una URL generada se considera vigente; pasado ese tiempo se vuelve a pedir al proveedor real. Limita
    /// cuánto puede durar una URL desactualizada si el proveedor cambiara su resultado (p. ej., por tráfico).
    /// </summary>
    [Range(0.0, MaxTimeToLiveMinutes, MinimumIsExclusive = true,
        ErrorMessage = "NavigationCache:TimeToLiveMinutes debe ser mayor que 0 y no pasar de una semana (10080 minutos).")]
    public double TimeToLiveMinutes { get; set; } = 10.0;

    /// <summary>
    /// Cantidad máxima de URLs guardadas a la vez, para que el caché no crezca sin límite. Al llegar al máximo se descarta
    /// primero la que está más cerca de vencer.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "NavigationCache:MaxEntries debe ser al menos 1.")]
    public int MaxEntries { get; set; } = 500;

    /// <summary>Tiempo de vida como <see cref="TimeSpan"/>. Es calculado: el binder de configuración solo escribe las propiedades con setter.</summary>
    public TimeSpan TimeToLive => TimeSpan.FromMinutes(TimeToLiveMinutes);
}
