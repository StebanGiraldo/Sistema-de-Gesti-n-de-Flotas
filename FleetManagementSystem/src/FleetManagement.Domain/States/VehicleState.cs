using FleetManagement.Domain.Enums;

namespace FleetManagement.Domain.States;

/// <summary>
/// ABSTRACT STATE (patrón STATE): lo que comparten los cuatro estados concretos. Implementa <see cref="IVehicleState"/> con las
/// reglas comunes y deja a cada estado concreto declarar lo suyo: qué permite (capacidades y motivo de bloqueo), a qué estados
/// se puede pasar a mano y cómo reacciona cuando termina un viaje.
///
/// Las clases concretas son <c>sealed</c> y se obtienen siempre por <see cref="IVehicleStateResolver"/>: no tienen estado propio
/// (son inmutables y compartidas), así que una sola instancia de cada una sirve a todos los vehículos y a todos los hilos.
/// </summary>
public abstract class VehicleState : IVehicleState
{
    protected VehicleState(VehicleStatus status)
    {
        Status = status;
    }

    public VehicleStatus Status { get; }

    public abstract bool CanOperate { get; }

    public virtual bool CanTrackTrip => false;

    public virtual string? OperationBlockReason(string licensePlate) => null;

    /// <summary>Estados a los que se puede pasar a mano desde este (sin contar el mismo: pedirlo de nuevo es inofensivo).</summary>
    protected abstract IReadOnlyList<VehicleStatus> ManualTargets { get; }

    /// <summary>
    /// Regla 1 del Mediator, que no distingue estados: el viaje que se pone en marcha deja al vehículo En ruta. Es una regla
    /// existente (la fija la prueba <c>MarkEnRoute_IsUnconditional...</c>); no se endurece aquí porque quien decide si un vehículo
    /// puede salir a una ruta son las reglas de asignación, que se ejecutan antes de llegar al Mediator.
    /// </summary>
    public virtual void StartTrip(VehicleStateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.TransitionTo(VehicleStatus.EnRoute);
    }

    /// <summary>Por defecto, terminar un viaje no cambia el estado (regla 3 del Mediator); solo EnRoute lo sobrescribe.</summary>
    public virtual void FinishTrip(VehicleStateContext context, bool hasOtherActiveTrip)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    /// <summary>Por defecto, registrar un mantenimiento no cambia el estado; solo Maintenance lo sobrescribe (el vehículo sale del taller).</summary>
    public virtual void CompleteMaintenance(VehicleStateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    public void ChangeTo(VehicleStateContext context, VehicleStatus target)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (target == Status)
            return;

        if (!ManualTargets.Contains(target))
            throw new InvalidVehicleTransitionException(context.Vehicle.LicensePlate, Status, target, ManualTargets);

        context.TransitionTo(target);
    }
}
