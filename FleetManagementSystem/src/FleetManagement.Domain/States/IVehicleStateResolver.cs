using FleetManagement.Domain.Enums;

namespace FleetManagement.Domain.States;

/// <summary>
/// Entrega el objeto de estado que corresponde a un valor de <see cref="VehicleStatus"/>. Existe para que los servicios dependan
/// de una abstracción (DIP) y no construyan estados por su cuenta, y para poder sustituirlo en las pruebas.
/// </summary>
public interface IVehicleStateResolver
{
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="status"/> no es un estado conocido.</exception>
    IVehicleState For(VehicleStatus status);
}
