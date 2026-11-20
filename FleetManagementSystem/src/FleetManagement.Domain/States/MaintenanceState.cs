using FleetManagement.Domain.Enums;

namespace FleetManagement.Domain.States;

/// <summary>
/// CONCRETE STATE (patrón STATE): el vehículo está en el taller. No opera, no se asigna a nuevas rutas y conserva este estado al
/// terminar un viaje (regla 3 del Mediator: terminar una ruta no lo repara). Para volver a salir a la carretera debe quedar antes
/// Available, y eso solo ocurre al registrar su mantenimiento (<see cref="CompleteMaintenance"/>): a mano únicamente puede pasar a
/// OutOfService, así que no sale del taller sin que quede constancia de lo que se le hizo. Pasar directamente a EnRoute tampoco está permitido.
///
/// Nota: si un viaje suyo se pone en marcha, <see cref="VehicleState.StartTrip"/> lo deja En ruta como siempre hizo el Mediator
/// (regla conservada); lo que impide que un vehículo en mantenimiento salga a una ruta son las reglas de asignación.
/// </summary>
public sealed class MaintenanceState : VehicleState
{
    // Available no está aquí a propósito: del taller solo se sale registrando el mantenimiento (CompleteMaintenance), no a mano.
    private static readonly VehicleStatus[] Targets = { VehicleStatus.OutOfService };

    internal MaintenanceState() : base(VehicleStatus.Maintenance) { }

    public override bool CanOperate => false;

    protected override IReadOnlyList<VehicleStatus> ManualTargets => Targets;

    public override string? OperationBlockReason(string licensePlate)
        => $"El vehículo {licensePlate} está en mantenimiento; no se puede usar en una ruta.";

    /// <summary>Pedir Available (o EnRoute) a mano se rechaza con la indicación de cómo sí se sale del taller.</summary>
    protected override string? RejectionHint(VehicleStatus target)
        => target is VehicleStatus.Available or VehicleStatus.EnRoute
            ? "Para dejarlo Disponible, registre el mantenimiento del vehículo."
            : null;

    /// <summary>Registrar un mantenimiento saca al vehículo del taller: queda Available (la regla que ya tenía <c>MaintenanceService</c>); es la única forma de llegar a Available desde aquí.</summary>
    public override void CompleteMaintenance(VehicleStateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.TransitionTo(VehicleStatus.Available);
    }
}
