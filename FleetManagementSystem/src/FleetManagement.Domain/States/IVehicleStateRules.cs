using FleetManagement.Domain.Enums;

namespace FleetManagement.Domain.States;

/// <summary>
/// STATE (patrón de comportamiento STATE) — lado de CONSULTA del estado operativo de un vehículo: qué permite el estado en el
/// que está, sin cambiar nada. Es el contrato mínimo que necesitan quienes solo preguntan (las reglas de asignación, el
/// seguimiento de viajes y los análisis de flota); quien además debe cambiar el estado usa <see cref="IVehicleStateBehavior"/>.
/// Se separó de la parte de comportamiento por el Principio de Segregación de Interfaces (ISP): un cliente que solo pregunta no
/// tiene por qué conocer las operaciones que mueven al vehículo de un estado a otro.
/// </summary>
public interface IVehicleStateRules
{
    /// <summary>Valor de <see cref="VehicleStatus"/> que representa este estado (el que guarda <c>Vehicle.Status</c>).</summary>
    VehicleStatus Status { get; }

    /// <summary>
    /// El vehículo puede usarse en una ruta. Verdadero en Available y EnRoute; falso en Maintenance y OutOfService (un vehículo en
    /// el taller, o fuera de servicio, no opera).
    /// </summary>
    bool CanOperate { get; }

    /// <summary>
    /// El vehículo participa en el seguimiento de un recorrido (lo mueve la simulación y aparece en el avance de los viajes).
    /// Solo es verdadero en EnRoute.
    /// </summary>
    bool CanTrackTrip { get; }

    /// <summary>
    /// Motivo, en una frase, por el que el vehículo NO puede usarse en una ruta, o <c>null</c> si puede
    /// (<see cref="CanOperate"/>). Es el mismo mensaje que las reglas de asignación devuelven al cliente.
    /// </summary>
    /// <param name="licensePlate">Placa del vehículo, para que el mensaje diga de cuál se trata.</param>
    string? OperationBlockReason(string licensePlate);
}
