using System.Collections.Concurrent;
using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Monitoring;

/// <summary>
/// CONCRETE OBSERVER #2 (patrón OBSERVER): mantiene la última telemetría conocida de cada vehículo para que la API la consulte
/// (<see cref="ILiveTelemetryBoard"/>). Su responsabilidad es independiente de la del observador de auditoría: este guarda
/// el estado más reciente y aquél deja constancia de los hitos.
///
/// Es un modelo de lectura alimentado por EMPUJE: se actualiza solo cuando el simulador publica un evento, sin leer repositorios
/// ni recalcular nada. <c>TripProgressService</c> sigue siendo la consulta por demanda del avance de los viajes activos y no
/// se toca; el tablero añade lo que esa consulta no tiene (lo último publicado sigue visible cuando el viaje termina).
///
/// Es Singleton, porque su estado debe ser único y compartido entre el paso de la simulación (que escribe) y las peticiones HTTP
/// (que leen), y por eso usa un <see cref="ConcurrentDictionary{TKey,TValue}"/>. Cada valor es un registro inmutable que se
/// reemplaza completo, así que un lector nunca ve un vehículo a medio actualizar. Hay a lo sumo una entrada por vehículo.
/// </summary>
public sealed class LiveTelemetryBoard : ITelemetryObserver, ILiveTelemetryBoard
{
    // Misma precisión que usa TripProgressService para mostrar: metros, décimas de porcentaje y de minuto.
    private const int CoordinateDecimals = 6;
    private const int DistanceDecimals = 3;
    private const int PercentDecimals = 1;
    private const int MinutesDecimals = 1;

    private readonly ConcurrentDictionary<Guid, VehicleLiveStatusDto> _lastKnown = new();

    public void OnTelemetry(TelemetryEvent telemetry)
    {
        switch (telemetry)
        {
            case VehicleMovedEvent moved:
                _lastKnown.AddOrUpdate(
                    moved.VehicleId,
                    _ => ToStatus(moved, lastStopReached: null),
                    // La última parada solo se conserva dentro del mismo viaje; un viaje nuevo empieza sin parada alcanzada.
                    (_, current) => ToStatus(moved, current.RouteId == moved.RouteId ? current.LastStopReached : null));
                break;

            case TripStopReachedEvent stop:
                Update(stop.VehicleId, current => current with { LastStopReached = stop.StopLabel, UpdatedAt = stop.OccurredAt });
                break;

            case TripArrivedEvent arrived:
                Update(arrived.VehicleId, current => current with { Arrived = true, UpdatedAt = arrived.OccurredAt });
                break;
        }
    }

    public IReadOnlyList<VehicleLiveStatusDto> GetAll()
        => _lastKnown.Values.OrderBy(status => status.LicensePlate, StringComparer.Ordinal).ToList();

    public VehicleLiveStatusDto? Get(Guid vehicleId)
        => _lastKnown.TryGetValue(vehicleId, out var status) ? status : null;

    /// <summary>
    /// Cambia lo ya conocido de un vehículo. Si el tablero no lo conoce (se registró a mitad del viaje y todavía no vio ningún
    /// movimiento suyo) no inventa una posición: espera al próximo movimiento, que sí trae todos los datos.
    /// </summary>
    private void Update(Guid vehicleId, Func<VehicleLiveStatusDto, VehicleLiveStatusDto> change)
    {
        while (_lastKnown.TryGetValue(vehicleId, out var current))
        {
            if (_lastKnown.TryUpdate(vehicleId, change(current), current))
                return;
        }
    }

    private static VehicleLiveStatusDto ToStatus(VehicleMovedEvent moved, string? lastStopReached)
        => new(
            moved.VehicleId,
            moved.LicensePlate,
            moved.RouteId,
            Math.Round(moved.Latitude, CoordinateDecimals),
            Math.Round(moved.Longitude, CoordinateDecimals),
            Math.Round(moved.MileageKm, DistanceDecimals),
            Math.Round(moved.ProgressPercent, PercentDecimals),
            moved.NextStopLabel,
            lastStopReached,
            Math.Round(moved.RemainingKm, DistanceDecimals),
            Math.Round(moved.EtaMinutes, MinutesDecimals),
            Arrived: false,
            moved.OccurredAt);
}
