using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Assignment;

/// <summary>
/// Asignar conductor y/o vehículo a una ruta YA creada que no los tiene (por ejemplo, una ruta duplicada, que nace sin
/// asignar). Es una interfaz aparte de <c>IDeliveryRouteService</c> a propósito: así no cambia el contrato que ya usan los
/// controladores, el simulador, el decorador de reglas y las pruebas (principio abierto/cerrado e interfaces pequeñas).
/// </summary>
public interface IRouteAssignmentService
{
    /// <summary>
    /// Completa lo que le falta a la ruta. Reglas (se informan todas las incumplidas a la vez):
    /// la ruta existe y no está Completada ni Cancelada; solo se llena lo que falta (no reemplaza a quien ya está);
    /// las mismas reglas de una ruta nueva (vehículo operativo, sin mantenimiento vencido, carga dentro de la capacidad,
    /// conductor existente); el conductor no puede estar en otro viaje activo; y si la ruta ya está en curso, el vehículo
    /// tampoco puede estar en otro viaje activo.
    /// </summary>
    /// <exception cref="KeyNotFoundException">La ruta no existe.</exception>
    /// <exception cref="ArgumentException">No se indicó ni conductor ni vehículo.</exception>
    /// <exception cref="AssignmentRuleException">Alguna regla no se cumple.</exception>
    Task<DeliveryRouteDto> AssignAsync(Guid routeId, AssignRouteRequest request);

    /// <summary>Conductores y vehículos que hoy cumplen las reglas para esta ruta (lo que ofrece el selector del panel).</summary>
    /// <exception cref="KeyNotFoundException">La ruta no existe.</exception>
    Task<RouteAssignmentOptionsDto> GetOptionsAsync(Guid routeId);
}
