namespace FleetManagement.Application.Commands;

/// <summary>Deshacer la creación de rutas: como no se borran, se cancelan. Solo si TODAS siguen planificadas (todo o nada).</summary>
public interface IPlannedRouteCanceller
{
    Task CancelAsync(IReadOnlyCollection<Guid> routeIds);
}
