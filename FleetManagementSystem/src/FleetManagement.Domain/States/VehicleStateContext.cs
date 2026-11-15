using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Domain.States;

/// <summary>
/// CONTEXT (patrón STATE): envuelve a un <see cref="Vehicle"/> y le da el comportamiento que corresponde a su estado. Permite
/// CONSULTAR el estado (<see cref="State"/>, <see cref="CanOperate"/>...) y CAMBIARLO (<see cref="StartTrip"/>,
/// <see cref="FinishTrip"/>, <see cref="ChangeTo"/>, <see cref="CompleteMaintenance"/>); cada operación se delega en el objeto de
/// estado vigente, que decide qué hacer.
/// El contexto no contiene ningún <c>if</c>/<c>switch</c> sobre el estado.
///
/// El estado vive en UN solo lugar, <c>Vehicle.Status</c> (lo que ya persisten los repositorios, los DTO y la API): el estado
/// vigente se resuelve a partir de él en cada consulta, así que no existe una segunda copia que pueda quedar desincronizada y el
/// contrato de la entidad no cambia. La entidad <see cref="Vehicle"/> no se modificó.
///
/// Es un objeto liviano de un solo uso: se crea alrededor del vehículo que se acaba de leer, se opera y se guarda el vehículo
/// con el repositorio, como antes.
/// </summary>
public sealed class VehicleStateContext
{
    private readonly IVehicleStateResolver _states;

    public VehicleStateContext(Vehicle vehicle, IVehicleStateResolver? states = null)
    {
        Vehicle = vehicle ?? throw new ArgumentNullException(nameof(vehicle));
        _states = states ?? VehicleStateResolver.Default;
    }

    /// <summary>El vehículo que se está operando.</summary>
    public Vehicle Vehicle { get; }

    /// <summary>Objeto de estado vigente, derivado de <c>Vehicle.Status</c>.</summary>
    public IVehicleState State => _states.For(Vehicle.Status);

    public VehicleStatus Status => Vehicle.Status;

    public bool CanOperate => State.CanOperate;

    public bool CanTrackTrip => State.CanTrackTrip;

    /// <summary>Motivo por el que el vehículo no puede usarse en una ruta, o <c>null</c> si puede.</summary>
    public string? OperationBlockReason() => State.OperationBlockReason(Vehicle.LicensePlate);

    /// <summary>Una ruta del vehículo se puso en marcha (ver <see cref="IVehicleStateBehavior.StartTrip"/>).</summary>
    public void StartTrip() => State.StartTrip(this);

    /// <summary>Una ruta del vehículo terminó o se canceló (ver <see cref="IVehicleStateBehavior.FinishTrip"/>).</summary>
    public void FinishTrip(bool hasOtherActiveTrip) => State.FinishTrip(this, hasOtherActiveTrip);

    /// <summary>Cambio manual de estado (ver <see cref="IVehicleStateBehavior.ChangeTo"/>).</summary>
    /// <exception cref="InvalidVehicleTransitionException">Si el estado actual no permite pasar a <paramref name="target"/>.</exception>
    public void ChangeTo(VehicleStatus target) => State.ChangeTo(this, target);

    /// <summary>Se registró un mantenimiento del vehículo (ver <see cref="IVehicleStateBehavior.CompleteMaintenance"/>).</summary>
    public void CompleteMaintenance() => State.CompleteMaintenance(this);

    /// <summary>
    /// Mueve el vehículo al estado indicado. Es <c>internal</c> a propósito: solo los estados, que viven en este ensamblado y
    /// aplican sus reglas, pueden decidir una transición; nadie de afuera puede saltarse esas reglas.
    /// </summary>
    internal void TransitionTo(VehicleStatus target)
    {
        _states.For(target); // rechaza un valor que no sea un estado conocido antes de tocar al vehículo
        Vehicle.Status = target;
    }
}
