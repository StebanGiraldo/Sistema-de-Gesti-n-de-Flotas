using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Monitoring;

namespace FleetManagement.Application.Tests.Observer;

/// <summary>
/// Observador de prueba (escrito a mano, sin librerías de mocking, igual que el resto del proyecto): guarda en orden todo lo
/// que recibe. La entrega del publicador está serializada, así que una lista simple basta.
/// </summary>
public sealed class RecordingObserver : ITelemetryObserver
{
    private readonly List<TelemetryEvent> _received = new();

    public IReadOnlyList<TelemetryEvent> Received => _received;

    public void OnTelemetry(TelemetryEvent telemetry) => _received.Add(telemetry);
}

/// <summary>Observador cuyo comportamiento lo pone la prueba (por ejemplo, publicar otro evento o cambiar la suscripción).</summary>
public sealed class DelegateObserver : ITelemetryObserver
{
    private readonly Action<TelemetryEvent> _onTelemetry;

    public DelegateObserver(Action<TelemetryEvent> onTelemetry)
    {
        _onTelemetry = onTelemetry;
    }

    public void OnTelemetry(TelemetryEvent telemetry) => _onTelemetry(telemetry);
}

/// <summary>Observador que siempre falla, para comprobar que su error no alcanza ni al publicador ni a los demás observadores.</summary>
public sealed class ThrowingObserver : ITelemetryObserver
{
    public const string FailureMessage = "fallo de prueba del observador";

    public int Calls { get; private set; }

    public void OnTelemetry(TelemetryEvent telemetry)
    {
        Calls++;
        throw new InvalidOperationException(FailureMessage);
    }
}

/// <summary>
/// Observador que mide si dos hilos llegaron a estar dentro a la vez. Cede el procesador mientras "trabaja" para que, si la entrega
/// NO estuviera serializada, el solapamiento fuera casi seguro y la prueba lo viera.
/// </summary>
public sealed class ExclusiveObserver : ITelemetryObserver
{
    private readonly List<TelemetryEvent> _received = new();
    private int _inside;
    private int _maxInside;

    public IReadOnlyList<TelemetryEvent> Received => _received;

    public int MaxConcurrency => Volatile.Read(ref _maxInside);

    public void OnTelemetry(TelemetryEvent telemetry)
    {
        var inside = Interlocked.Increment(ref _inside);
        RaiseMax(inside);

        _received.Add(telemetry);   // una lista no soporta escrituras simultáneas: si la entrega se solapara, esto se corrompería
        Thread.Yield();

        Interlocked.Decrement(ref _inside);
    }

    private void RaiseMax(int candidate)
    {
        int current;
        while ((current = Volatile.Read(ref _maxInside)) < candidate)
        {
            if (Interlocked.CompareExchange(ref _maxInside, candidate, current) == current)
                return;
        }
    }
}

/// <summary>Bitácora que falla siempre: informar un error del observador no debe poder romper la entrega.</summary>
public sealed class ThrowingAuditLogger : IFleetAuditLogger
{
    public void LogEvent(string category, string message, string? username = null)
        => throw new InvalidOperationException("la bitácora tampoco está disponible");

    public IReadOnlyList<AuditLogEntryDto> GetRecentLogs(int count = 100) => new List<AuditLogEntryDto>();
}

/// <summary>Constructores de eventos de telemetría con valores razonables, para que cada prueba muestre solo lo que le importa.</summary>
public static class Telemetry
{
    public static readonly Guid DefaultVehicle = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DefaultRoute = new("22222222-2222-2222-2222-222222222222");
    public static readonly DateTimeOffset At = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    public static VehicleMovedEvent Moved(
        string plate = "VAN-001",
        Guid? vehicleId = null,
        Guid? routeId = null,
        double latitude = 0,
        double longitude = 0.05,
        double mileageKm = 1005,
        double stepKm = 5,
        double percent = 7.5,
        string nextStop = "Primera",
        double remainingKm = 61.717,
        double etaMinutes = 37,
        DateTimeOffset? at = null)
        => new(vehicleId ?? DefaultVehicle, plate, routeId ?? DefaultRoute, at ?? At, latitude, longitude, mileageKm, stepKm, percent, nextStop, remainingKm, etaMinutes);

    public static TripStopReachedEvent Stop(
        string label = "Primera",
        string plate = "VAN-001",
        Guid? vehicleId = null,
        Guid? routeId = null,
        double distanceKm = 11.1,
        double percent = 16.7,
        DateTimeOffset? at = null)
        => new(vehicleId ?? DefaultVehicle, plate, routeId ?? DefaultRoute, at ?? At, label, distanceKm, percent);

    public static TripArrivedEvent Arrived(
        string plate = "VAN-001",
        Guid? vehicleId = null,
        Guid? routeId = null,
        double totalKm = 66.7,
        DateTimeOffset? at = null)
        => new(vehicleId ?? DefaultVehicle, plate, routeId ?? DefaultRoute, at ?? At, totalKm);
}
