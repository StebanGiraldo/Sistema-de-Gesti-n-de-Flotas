using System.Globalization;
using FleetManagement.Application.Interfaces;

namespace FleetManagement.Application.Monitoring;

/// <summary>
/// CONCRETE OBSERVER #1 (patrón OBSERVER): deja en la auditoría los hitos de un recorrido. Su única responsabilidad es esa;
/// no mantiene estado ni conoce al tablero en vivo ni a los demás observadores.
///
/// Solo audita las paradas alcanzadas. No audita cada movimiento (cada paso de la simulación generaría un registro por vehículo
/// y llenaría el historial, que guarda solo los últimos 1000 eventos) ni la llegada: cuando el vehículo llega, la ruta se
/// completa y <c>DeliveryRouteService</c> ya audita ese cambio ("Ruta ... cambió su estado a Completed"); repetirlo aquí
/// duplicaría el registro.
///
/// Es Singleton y solo depende de <see cref="IFleetAuditLogger"/> (Singleton), así que no hay dependencias con ciclos de vida
/// incorrectos.
/// </summary>
public sealed class TripAuditObserver : ITelemetryObserver
{
    public const string AuditCategory = "Telemetría";

    private readonly IFleetAuditLogger _audit;

    public TripAuditObserver(IFleetAuditLogger audit)
    {
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public void OnTelemetry(TelemetryEvent telemetry)
    {
        if (telemetry is not TripStopReachedEvent stop)
            return;

        var percent = stop.ProgressPercent.ToString("0.#", CultureInfo.InvariantCulture);
        _audit.LogEvent(AuditCategory, $"El vehículo {stop.LicensePlate} alcanzó la parada '{stop.StopLabel}' ({percent} % del recorrido).");
    }
}
