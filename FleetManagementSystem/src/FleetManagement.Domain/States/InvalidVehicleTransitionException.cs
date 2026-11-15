using FleetManagement.Domain.Enums;

namespace FleetManagement.Domain.States;

/// <summary>
/// Se lanza cuando el estado actual de un vehículo no permite pasar al estado pedido (por ejemplo, de Maintenance a EnRoute sin
/// quedar antes Available). Hereda de <see cref="InvalidOperationException"/>, como <c>AssignmentRuleException</c>: es una regla
/// de negocio incumplida, no un argumento mal formado. La API la responde con 400 y el motivo.
/// </summary>
public sealed class InvalidVehicleTransitionException : InvalidOperationException
{
    public InvalidVehicleTransitionException(string licensePlate, VehicleStatus from, VehicleStatus to, IReadOnlyCollection<VehicleStatus> allowedTargets)
        : base($"Transición de estado no permitida: el vehículo {licensePlate} está en {from} y no puede pasar a {to}. " +
               $"Desde {from} solo puede pasar a: {string.Join(", ", allowedTargets)}.")
    {
        LicensePlate = licensePlate;
        From = from;
        To = to;
        AllowedTargets = allowedTargets;
    }

    public string LicensePlate { get; }

    /// <summary>Estado en el que estaba el vehículo (no cambió).</summary>
    public VehicleStatus From { get; }

    /// <summary>Estado que se pidió y no se permitió.</summary>
    public VehicleStatus To { get; }

    /// <summary>Estados a los que el vehículo SÍ puede pasar desde <see cref="From"/>.</summary>
    public IReadOnlyCollection<VehicleStatus> AllowedTargets { get; }
}
