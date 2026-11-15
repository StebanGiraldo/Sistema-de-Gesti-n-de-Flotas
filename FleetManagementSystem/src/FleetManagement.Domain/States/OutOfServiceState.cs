using FleetManagement.Domain.Enums;

namespace FleetManagement.Domain.States;

/// <summary>
/// CONCRETE STATE (patrón STATE): el vehículo está fuera de servicio. No opera, no se asigna a nuevas rutas y conserva este estado
/// al terminar un viaje (regla 3 del Mediator). Puede volver a Available o pasar a Maintenance; pasar directamente a EnRoute a
/// mano no está permitido.
///
/// Nota: igual que en <see cref="MaintenanceState"/>, un viaje que se pone en marcha lo deja En ruta (regla conservada del
/// Mediator); el bloqueo de un vehículo fuera de servicio ocurre antes, en las reglas de asignación.
/// </summary>
public sealed class OutOfServiceState : VehicleState
{
    private static readonly VehicleStatus[] Targets = { VehicleStatus.Available, VehicleStatus.Maintenance };

    internal OutOfServiceState() : base(VehicleStatus.OutOfService) { }

    public override bool CanOperate => false;

    protected override IReadOnlyList<VehicleStatus> ManualTargets => Targets;

    public override string? OperationBlockReason(string licensePlate)
        => $"El vehículo {licensePlate} está fuera de servicio; no se puede usar en una ruta.";
}
