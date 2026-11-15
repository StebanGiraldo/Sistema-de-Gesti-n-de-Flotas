using FleetManagement.Domain.Enums;
using FleetManagement.Domain.States;

namespace FleetManagement.Application.Tests.Support;

/// <summary>
/// Estado de vehículo de prueba (State) cuyas capacidades fija la prueba. Sirve para demostrar que un componente le PREGUNTA al
/// estado y no compara el enum: si cambia lo que el estado responde, cambia lo que el componente hace, sin tocar al componente.
/// Solo contesta las consultas; las operaciones de comportamiento no se usan en esas pruebas y fallan si alguien las llama,
/// salvo <see cref="CompleteMaintenance"/>, que se anota y no cambia el estado (para demostrar que quien decide es el estado).
/// </summary>
public sealed class ConfigurableState : IVehicleState
{
    private readonly string? _blockReason;

    public ConfigurableState(VehicleStatus status, bool canOperate, bool canTrackTrip, string? blockReason = null)
    {
        Status = status;
        CanOperate = canOperate;
        CanTrackTrip = canTrackTrip;
        _blockReason = blockReason;
    }

    public VehicleStatus Status { get; }
    public bool CanOperate { get; }
    public bool CanTrackTrip { get; }

    /// <summary>Estados que se le han preguntado por el motivo de bloqueo: así se ve que el componente consultó al estado.</summary>
    public List<string> BlockReasonAskedFor { get; } = new();

    public string? OperationBlockReason(string licensePlate)
    {
        BlockReasonAskedFor.Add(licensePlate);
        return _blockReason is null ? null : $"{_blockReason} ({licensePlate})";
    }

    public void StartTrip(VehicleStateContext context) => throw new NotSupportedException();
    public void FinishTrip(VehicleStateContext context, bool hasOtherActiveTrip) => throw new NotSupportedException();
    public void ChangeTo(VehicleStateContext context, VehicleStatus target) => throw new NotSupportedException();

    /// <summary>Cuántas veces se le pidió a este estado reaccionar a un mantenimiento registrado.</summary>
    public int MaintenanceCompletedCount { get; private set; }

    /// <summary>No cambia el estado del vehículo: solo anota que se le preguntó.</summary>
    public void CompleteMaintenance(VehicleStateContext context) => MaintenanceCompletedCount++;
}

/// <summary>Resolutor de estados de prueba: entrega los estados reales salvo los que la prueba reemplace por otro.</summary>
public sealed class ConfigurableResolver : IVehicleStateResolver
{
    private readonly Dictionary<VehicleStatus, IVehicleState> _overrides = new();

    public ConfigurableResolver Override(IVehicleState state)
    {
        _overrides[state.Status] = state;
        return this;
    }

    public IVehicleState For(VehicleStatus status)
        => _overrides.TryGetValue(status, out var state) ? state : VehicleStateResolver.Default.For(status);
}
