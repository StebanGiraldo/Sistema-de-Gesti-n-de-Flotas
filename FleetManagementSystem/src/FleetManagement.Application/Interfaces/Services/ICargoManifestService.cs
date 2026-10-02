using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Interfaces.Services;

public interface ICargoManifestService
{
    /// <summary>Devuelve el manifiesto jerárquico de carga de la ruta, o null si la ruta no existe.</summary>
    Task<CargoManifestDto?> GetManifestAsync(Guid routeId);
}
