using FleetManagement.Application.Geo;

namespace FleetManagement.Application.Optimization;

/// <summary>
/// Línea base: lo que haría quien reparte "como viene". Toma las entregas en el orden recibido y le da cada una al primer
/// vehículo de la lista al que le quepa, sin ordenar por tamaño ni por urgencia y sin mirar qué vehículo está más cerca.
/// Existe para poder medir cuánto mejora <see cref="PriorityBestFitStrategy"/> (el plan de comparación) y como estrategia
/// de referencia en las pruebas.
/// </summary>
public sealed class FirstFitInOrderStrategy : ICargoAssignmentStrategy
{
    public CargoAssignment Assign(IReadOnlyList<PendingDelivery> deliveries, IReadOnlyList<CandidateVehicle> vehicles, GeoCoordinate depot)
    {
        var bins = vehicles.Select(vehicle => new LoadBin(vehicle)).ToList();
        var unassigned = new List<UnassignedDelivery>();

        foreach (var delivery in deliveries)
        {
            var bin = bins.FirstOrDefault(candidate => candidate.Fits(delivery));
            if (bin is null)
                unassigned.Add(new UnassignedDelivery(delivery, UnassignedReasons.For(delivery, vehicles)));
            else
                bin.Add(delivery);
        }

        return LoadBin.Assignment(bins, unassigned);
    }
}
