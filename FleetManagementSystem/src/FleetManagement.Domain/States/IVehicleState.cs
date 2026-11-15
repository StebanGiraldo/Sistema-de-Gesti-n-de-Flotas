namespace FleetManagement.Domain.States;

/// <summary>
/// STATE (patrón de comportamiento STATE): contrato de los estados operativos de un vehículo (Available, EnRoute, Maintenance,
/// OutOfService). Reúne lo que cada estado PERMITE (<see cref="IVehicleStateRules"/>) y lo que HACE ante los eventos
/// (<see cref="IVehicleStateBehavior"/>). No agrega miembros propios: quien solo necesita una de las dos mitades depende solo de
/// esa interfaz.
/// </summary>
public interface IVehicleState : IVehicleStateRules, IVehicleStateBehavior
{
}
