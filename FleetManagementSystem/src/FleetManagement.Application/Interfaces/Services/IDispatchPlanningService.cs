using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Interfaces.Services;

/// <summary>Reparte entregas entre los vehículos disponibles y arma el recorrido de cada uno.</summary>
public interface IDispatchPlanningService
{
    /// <summary>Propone el reparto y lo compara con el plan sin optimizar. NO guarda nada.</summary>
    /// <exception cref="ArgumentException">Si los datos de la solicitud no son válidos.</exception>
    Task<DispatchPlanDto> PlanAsync(DispatchPlanRequest request);

    /// <summary>Crea una ruta planificada por cada viaje del plan. Todo o nada: si alguna no cumple las reglas, no se crea ninguna.</summary>
    /// <exception cref="ArgumentException">Si los datos de la solicitud no son válidos.</exception>
    /// <exception cref="InvalidOperationException">Si no hay nada asignable, o si alguna ruta incumple las reglas de asignación.</exception>
    Task<DispatchApplyResultDto> ApplyAsync(DispatchPlanRequest request);
}
