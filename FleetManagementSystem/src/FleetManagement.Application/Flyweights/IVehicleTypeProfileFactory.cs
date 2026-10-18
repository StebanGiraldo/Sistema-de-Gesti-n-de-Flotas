using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Flyweights;

/// <summary>
/// FÁBRICA DE FLYWEIGHTS: entrega siempre la MISMA instancia de <see cref="VehicleTypeProfile"/> para un tipo de vehículo.
/// Es una interfaz pequeña a propósito (un solo método): quien necesita un perfil, por ejemplo el simulador de
/// movimiento para saber la velocidad del tipo, no necesita saber cómo ni cuándo se construye.
/// </summary>
public interface IVehicleTypeProfileFactory
{
    /// <exception cref="NotSupportedException">Si no hay fábrica de incorporación registrada para el tipo.</exception>
    VehicleTypeProfile GetProfile(VehicleType type);
}
