using FleetManagement.Domain.Enums;

namespace FleetManagement.Domain.States;

/// <summary>
/// STATE (patrón de comportamiento STATE) — lado de COMPORTAMIENTO: cómo reacciona cada estado a los eventos que le llegan al
/// vehículo. Cada operación recibe el <see cref="VehicleStateContext"/> y es el propio estado quien decide si el vehículo cambia
/// de estado (moviéndolo con el contexto) o si el evento se rechaza o se ignora; el contexto no tiene ningún condicional sobre
/// el estado.
///
/// Hay tres orígenes de eventos, y se mantienen separados a propósito porque así ya funcionaba el sistema:
/// <list type="bullet">
///   <item>El CICLO DE VIDA de las rutas (<see cref="StartTrip"/>, <see cref="FinishTrip"/>), que llega por el Mediator
///         (<c>VehicleColleague</c>). Son reglas de coordinación ya existentes y no se endurecen.</item>
///   <item>El CAMBIO MANUAL del estado (<see cref="ChangeTo"/>) que pide un administrador por la API
///         (<c>VehicleService.UpdateVehicleStatusAsync</c>), donde sí se validan las transiciones.</item>
///   <item>El REGISTRO DE UN MANTENIMIENTO (<see cref="CompleteMaintenance"/>), que llega desde
///         <c>MaintenanceService.CreateAsync</c>: la regla de siempre —un vehículo en el taller sale de él— que antes era un
///         <c>if</c> sobre el estado dentro del servicio y ahora es comportamiento del estado.</item>
/// </list>
/// </summary>
public interface IVehicleStateBehavior
{
    /// <summary>
    /// Una ruta del vehículo se puso en marcha (En progreso o Retrasada). Regla conservada del Mediator: el vehículo queda
    /// En ruta desde cualquier estado. Que un vehículo en Maintenance u OutOfService no salga a una ruta se comprueba ANTES,
    /// en las reglas de asignación (que consultan <see cref="IVehicleStateRules.OperationBlockReason"/>), no aquí.
    /// </summary>
    void StartTrip(VehicleStateContext context);

    /// <summary>
    /// Una ruta del vehículo terminó o se canceló. Solo un vehículo En ruta que no sigue en otro viaje activo
    /// (<paramref name="hasOtherActiveTrip"/>) queda Available; en cualquier otro caso conserva su estado: terminar una ruta no
    /// repara un vehículo en Maintenance ni lo devuelve de OutOfService.
    /// </summary>
    void FinishTrip(VehicleStateContext context, bool hasOtherActiveTrip);

    /// <summary>
    /// Cambio manual a <paramref name="target"/>. Pedir el estado en el que ya está no cambia nada (es inofensivo, como siempre).
    /// </summary>
    /// <exception cref="InvalidVehicleTransitionException">Si el estado actual no permite pasar a <paramref name="target"/>.</exception>
    void ChangeTo(VehicleStateContext context, VehicleStatus target);

    /// <summary>
    /// Se registró un mantenimiento del vehículo. Solo un vehículo que está en el taller (Maintenance) sale de él y queda
    /// Available; en cualquier otro estado no cambia nada: registrar un mantenimiento no devuelve a un vehículo Fuera de servicio
    /// ni saca de su viaje a uno En ruta (es la regla que ya tenía <c>MaintenanceService</c>).
    /// </summary>
    void CompleteMaintenance(VehicleStateContext context);
}
