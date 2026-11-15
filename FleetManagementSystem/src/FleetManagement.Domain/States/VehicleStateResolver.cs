using FleetManagement.Domain.Enums;

namespace FleetManagement.Domain.States;

/// <summary>
/// Tabla de los cuatro estados concretos, uno por cada valor de <see cref="VehicleStatus"/>. Los estados no tienen estado propio,
/// así que hay UNA sola instancia de cada uno, compartida por todos los vehículos y todos los hilos (seguro sin sincronizar).
/// En la aplicación se registra como Singleton con <see cref="Default"/>, igual que se hace con el logger de auditoría, para que
/// el contenedor y quien use el valor por defecto compartan las mismas instancias.
/// </summary>
public sealed class VehicleStateResolver : IVehicleStateResolver
{
    /// <summary>Instancia compartida. Es la que se registra en el contenedor y la que se usa cuando no se inyecta otra.</summary>
    public static VehicleStateResolver Default { get; } = new();

    private readonly IReadOnlyDictionary<VehicleStatus, IVehicleState> _states;

    private VehicleStateResolver()
    {
        _states = new IVehicleState[]
        {
            new AvailableState(),
            new EnRouteState(),
            new MaintenanceState(),
            new OutOfServiceState()
        }.ToDictionary(state => state.Status);
    }

    public IVehicleState For(VehicleStatus status)
        => _states.TryGetValue(status, out var state)
            ? state
            : throw new ArgumentOutOfRangeException(nameof(status), status, "Estado de vehículo desconocido.");
}
