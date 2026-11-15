using FleetManagement.Domain.Enums;

namespace FleetManagement.Domain.States;

/// <summary>
/// CONCRETE STATE (patrón STATE): el vehículo está en el taller. No opera, no se asigna a nuevas rutas y conserva este estado al
/// terminar un viaje (regla 3 del Mediator: terminar una ruta no lo repara). Para volver a salir a la carretera debe quedar antes
/// Available (se hace a mano, o al registrar un mantenimiento); pasar directamente a EnRoute a mano no está permitido.
///
/// Nota: si un viaje suyo se pone en marcha, <see cref="VehicleState.StartTrip"/> lo deja En ruta como siempre hizo el Mediator
/// (regla conservada); lo que impide que un vehículo en mantenimiento salga a una ruta son las reglas de asignación.
/// </summary>
public sealed class MaintenanceState : VehicleState
{
    private static readonly VehicleStatus[] Targets = { VehicleStatus.Available, VehicleStatus.OutOfService };

    internal MaintenanceState() : base(VehicleStatus.Maintenance) { }

    public override bool CanOperate => false;

    protected override IReadOnlyList<VehicleStatus> ManualTargets => Targets;

    public override string? OperationBlockReason(string licensePlate)
        => $"El vehículo {licensePlate} está en mantenimiento; no se puede usar en una ruta.";

    /// <summary>Registrar un mantenimiento saca al vehículo del taller: queda Available (la regla que ya tenía <c>MaintenanceService</c>).</summary>
    public override void CompleteMaintenance(VehicleStateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.TransitionTo(VehicleStatus.Available);
    }
}
