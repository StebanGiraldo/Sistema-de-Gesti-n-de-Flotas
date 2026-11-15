using FleetManagement.Application.Monitoring;

namespace FleetManagement.Application.Simulation;

/// <summary>
/// Traduce UN paso de la simulación en los eventos de telemetría que le corresponden (patrón OBSERVER, lado del publicador).
/// Vive en Simulation y no en Monitoring a propósito: conoce los viajes y la poligonal (<see cref="ActiveTrip"/>,
/// <see cref="RoutePath"/>), y así Monitoring no depende de Simulation. Es una función pura —mismos datos, mismos eventos—,
/// por lo que se prueba sin simulador ni repositorios, y mantiene al simulador con una sola línea nueva en vez de una
/// responsabilidad más.
///
/// No recalcula nada: la posición, el porcentaje, la próxima parada y la ETA salen de <see cref="RoutePath.ProgressAt"/>, la
/// misma operación que usa <c>TripProgressService</c>, de modo que lo que ve cada consumidor no puede contradecirse.
/// </summary>
public static class TripTelemetryEvents
{
    /// <summary>
    /// Eventos de un paso que llevó al vehículo de <paramref name="previousKm"/> a <paramref name="currentKm"/> sobre su ruta,
    /// en este orden: el movimiento; una parada alcanzada por cada parada intermedia que el vehículo cruzó en el paso (en el orden
    /// del recorrido); y, si llegó al destino, la llegada. El vehículo de <paramref name="trip"/> debe traer ya el kilometraje
    /// actualizado.
    /// </summary>
    public static IReadOnlyList<TelemetryEvent> ForStep(
        ActiveTrip trip,
        RoutePath path,
        double speedKmh,
        double previousKm,
        double currentKm,
        DateTimeOffset occurredAt)
    {
        var vehicle = trip.Vehicle;
        var progress = path.ProgressAt(vehicle.Id, currentKm, speedKmh);

        var events = new List<TelemetryEvent>
        {
            new VehicleMovedEvent(
                vehicle.Id, vehicle.LicensePlate, trip.Route.Id, occurredAt,
                progress.Latitude, progress.Longitude, vehicle.MileageKm, currentKm - previousKm,
                progress.Percent, progress.NextStopLabel, progress.RemainingKm, progress.EtaMinutes)
        };

        events.AddRange(StopsReached(trip, path, speedKmh, previousKm, currentKm, occurredAt));

        if (progress.Arrived)
            events.Add(new TripArrivedEvent(vehicle.Id, vehicle.LicensePlate, trip.Route.Id, occurredAt, path.TotalKm));

        return events;
    }

    // Paradas intermedias: entre el origen (la primera) y el destino (la última), que no es una parada sino la llegada. Una parada
    // se alcanza en el paso que la cruza: estaba más allá del punto anterior y no más allá del actual. Así cada parada se
    // informa una sola vez aunque un paso largo cruce varias.
    private static IEnumerable<TelemetryEvent> StopsReached(
        ActiveTrip trip, RoutePath path, double speedKmh, double previousKm, double currentKm, DateTimeOffset occurredAt)
    {
        for (var index = 1; index < path.Stops.Count - 1; index++)
        {
            var stopKm = path.CumulativeKm[index];
            if (stopKm <= previousKm || stopKm > currentKm)
                continue;

            var percentAtStop = path.ProgressAt(trip.Vehicle.Id, stopKm, speedKmh).Percent;
            yield return new TripStopReachedEvent(
                trip.Vehicle.Id, trip.Vehicle.LicensePlate, trip.Route.Id, occurredAt,
                path.Stops[index].Label, stopKm, percentAtStop);
        }
    }
}
