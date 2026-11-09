namespace FleetManagement.Application.Commands;

/// <summary>
/// Deshacer la creación de rutas: se ELIMINAN, no se dejan canceladas. Así deshacer y rehacer una y otra vez no va dejando
/// rutas canceladas acumuladas en la lista. Solo si TODAS siguen planificadas (todo o nada).
/// </summary>
public interface IPlannedRouteDeleter
{
    Task DeleteAsync(IReadOnlyCollection<Guid> routeIds);
}
