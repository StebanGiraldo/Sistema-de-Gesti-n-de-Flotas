using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Interfaces.Services;

public interface ITripProgressService
{
    /// <summary>
    /// Avance de TODOS los viajes activos (vehículo en recorrido con su ruta en curso o retrasada) en una sola consulta.
    /// Si no hay viajes activos devuelve una lista vacía.
    /// </summary>
    Task<IReadOnlyList<TripProgressDto>> GetActiveTripsAsync();
}
