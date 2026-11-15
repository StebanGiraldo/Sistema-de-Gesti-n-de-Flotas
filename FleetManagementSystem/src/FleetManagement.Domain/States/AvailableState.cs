using FleetManagement.Domain.Enums;

namespace FleetManagement.Domain.States;

/// <summary>
/// CONCRETE STATE (patrón STATE): el vehículo está libre. Puede operar, pero todavía no sigue ningún viaje. Puede salir a una ruta
/// (EnRoute), ir al taller (Maintenance) o quedar fuera de servicio (OutOfService); terminar un viaje no lo cambia.
/// </summary>
public sealed class AvailableState : VehicleState
{
    private static readonly VehicleStatus[] Targets = { VehicleStatus.EnRoute, VehicleStatus.Maintenance, VehicleStatus.OutOfService };

    internal AvailableState() : base(VehicleStatus.Available) { }

    public override bool CanOperate => true;

    protected override IReadOnlyList<VehicleStatus> ManualTargets => Targets;
}
