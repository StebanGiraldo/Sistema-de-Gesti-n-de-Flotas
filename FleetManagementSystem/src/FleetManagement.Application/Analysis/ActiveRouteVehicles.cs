namespace FleetManagement.Application.Analysis;

/// <summary>
/// Regla única para saber qué vehículos tienen ahora una ruta en curso. La usan el análisis de utilización y el de hallazgos
/// operativos: así ambos coinciden en quién está "en uso" y la regla no se escribe dos veces.
/// </summary>
internal static class ActiveRouteVehicles
{
    /// <summary>Ids de los vehículos asignados a alguna ruta en curso (en progreso o retrasada).</summary>
    public static HashSet<Guid> Of(IEnumerable<RouteElement> routes)
        => routes
            .Where(route => route.IsActive && route.AssignedVehicleId.HasValue)
            .Select(route => route.AssignedVehicleId!.Value)
            .ToHashSet();
}
