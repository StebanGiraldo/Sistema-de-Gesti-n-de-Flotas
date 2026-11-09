using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Interfaces.Services;

public interface ICargoManifestService
{
    /// <summary>Devuelve el manifiesto jerárquico de carga de la ruta, o null si la ruta no existe.</summary>
    Task<CargoManifestDto?> GetManifestAsync(Guid routeId);

    /// <summary>
    /// Devuelve los artículos de carga de la ruta como una lista plana, en el orden del recorrido en profundidad
    /// del manifiesto (patrón Iterator), o null si la ruta no existe.
    /// </summary>
    Task<CargoManifestItemsDto?> GetCargoItemsAsync(Guid routeId);
}
