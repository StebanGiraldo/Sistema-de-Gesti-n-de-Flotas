using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Assignment;

/// <summary>
/// Reglas que debe cumplir una asignación antes de aceptarla. Ambas operaciones lanzan
/// <see cref="AssignmentRuleException"/> con TODAS las reglas incumplidas (no solo la primera) y no hacen nada si todo está bien.
/// </summary>
public interface IRouteAssignmentValidator
{
    /// <summary>
    /// Ruta nueva: el vehículo y el conductor existen, el vehículo está operativo y sin mantenimiento vencido, y la
    /// carga cabe en peso y volumen. No exige que el vehículo esté libre hoy: se puede planificar con anticipación.
    /// </summary>
    Task ValidateNewRouteAsync(Guid? vehicleId, Guid? driverId, IEnumerable<CargoItemDto>? cargo);

    /// <summary>
    /// Salida a ruta (pasar a En progreso o Retrasada): el vehículo está operativo, sin mantenimiento vencido y sin otro
    /// viaje activo; el conductor tampoco tiene otro viaje activo. Una ruta que ya está en curso se acepta sin más.
    /// </summary>
    Task ValidateStartAsync(Guid routeId);
}
