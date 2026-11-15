using FleetManagement.Domain.Enums;

namespace FleetManagement.Domain.States;

/// <summary>
/// CONCRETE STATE (patrón STATE): el vehículo está en recorrido. Es el único estado que participa en el seguimiento del viaje
/// (lo mueve la simulación y aparece en el avance de los viajes). Al terminar o cancelarse un viaje queda Available, salvo que
/// siga ocupado en otro viaje activo (regla 2 del Mediator). Puede pasar a Available (liberado a mano), a Maintenance (avería en
/// ruta) o a OutOfService.
/// </summary>
public sealed class EnRouteState : VehicleState
{
    private static readonly VehicleStatus[] Targets = { VehicleStatus.Available, VehicleStatus.Maintenance, VehicleStatus.OutOfService };

    internal EnRouteState() : base(VehicleStatus.EnRoute) { }

    public override bool CanOperate => true;

    public override bool CanTrackTrip => true;

    protected override IReadOnlyList<VehicleStatus> ManualTargets => Targets;

    public override void FinishTrip(VehicleStateContext context, bool hasOtherActiveTrip)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!hasOtherActiveTrip)
            context.TransitionTo(VehicleStatus.Available);
    }
}
