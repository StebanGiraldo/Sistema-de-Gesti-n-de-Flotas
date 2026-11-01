using FleetManagement.Application.Geo;

namespace FleetManagement.Application.Optimization;

/// <summary>
/// Asignación que aprovecha los vehículos disponibles (empaquetado "best fit decreasing" con prioridades):
///
///  1. Las entregas salen de una cola de prioridad (<see cref="PriorityQueue{TElement, TPriority}"/>): primero las más
///     urgentes y, entre iguales, las más pesadas (las grandes se colocan primero y las pequeñas rellenan huecos).
///  2. CONSOLIDAR: cada entrega va al vehículo YA cargado al que mejor le calza (el que queda con menos espacio libre).
///     Así se usan menos vehículos y se evitan viajes innecesarios.
///  3. Si en ninguno cabe, se ABRE UN VEHÍCULO NUEVO: el que antes llega al punto de salida (cercanía y velocidad de su
///     tipo); a igual tiempo, el de menor capacidad. Si ninguno cabe, la entrega queda sin asignar con su motivo.
///
/// Prioriza usar menos vehículos sobre recorrer menos kilómetros: agrupar entregas ahorra viajes aunque algún tramo se
/// alargue. Es determinista: los empates se resuelven por orden de llegada y por placa.
/// </summary>
public sealed class PriorityBestFitStrategy : ICargoAssignmentStrategy
{
    public CargoAssignment Assign(IReadOnlyList<PendingDelivery> deliveries, IReadOnlyList<CandidateVehicle> vehicles, GeoCoordinate depot)
    {
        var bins = vehicles.Select(vehicle => new LoadBin(vehicle)).ToList();
        var unassigned = new List<UnassignedDelivery>();
        var queue = RankedQueue(deliveries);

        while (queue.TryDequeue(out var delivery, out _))
        {
            var bin = ChooseBin(delivery, bins, depot);
            if (bin is null)
                unassigned.Add(new UnassignedDelivery(delivery, UnassignedReasons.For(delivery, vehicles)));
            else
                bin.Add(delivery);
        }

        return LoadBin.Assignment(bins, unassigned);
    }

    private static PriorityQueue<PendingDelivery, DeliveryRank> RankedQueue(IReadOnlyList<PendingDelivery> deliveries)
    {
        var queue = new PriorityQueue<PendingDelivery, DeliveryRank>();
        for (var index = 0; index < deliveries.Count; index++)
            queue.Enqueue(deliveries[index], new DeliveryRank(deliveries[index].PriorityRank, deliveries[index].WeightKg, deliveries[index].VolumeM3, index));

        return queue;
    }

    private static LoadBin? ChooseBin(PendingDelivery delivery, IReadOnlyList<LoadBin> bins, GeoCoordinate depot)
        => TightestUsedFit(delivery, bins) ?? EarliestToDepot(delivery, bins, depot);

    private static LoadBin? TightestUsedFit(PendingDelivery delivery, IReadOnlyList<LoadBin> bins)
        => bins.Where(bin => bin.IsUsed && bin.Fits(delivery))
               .OrderBy(bin => bin.FreeKgAfter(delivery))
               .ThenBy(bin => bin.Vehicle.LicensePlate, StringComparer.Ordinal)
               .FirstOrDefault();

    private static LoadBin? EarliestToDepot(PendingDelivery delivery, IReadOnlyList<LoadBin> bins, GeoCoordinate depot)
        => bins.Where(bin => !bin.IsUsed && bin.Fits(delivery))
               .OrderBy(bin => HoursToReach(bin.Vehicle, depot))
               .ThenBy(bin => bin.Vehicle.CapacityKg)
               .ThenBy(bin => bin.Vehicle.LicensePlate, StringComparer.Ordinal)
               .FirstOrDefault();

    private static double HoursToReach(CandidateVehicle vehicle, GeoCoordinate depot)
        => GeoMath.DistanceKm(vehicle.Location, depot) / Math.Max(vehicle.SpeedKmh, 1);

    /// <summary>Orden de salida de la cola: más urgente primero; a igual urgencia, más pesada, luego más voluminosa y, al final, la que llegó antes.</summary>
    private readonly record struct DeliveryRank(int PriorityRank, double WeightKg, double VolumeM3, int Index) : IComparable<DeliveryRank>
    {
        public int CompareTo(DeliveryRank other)
        {
            var byPriority = other.PriorityRank.CompareTo(PriorityRank);
            if (byPriority != 0) return byPriority;

            var byWeight = other.WeightKg.CompareTo(WeightKg);
            if (byWeight != 0) return byWeight;

            var byVolume = other.VolumeM3.CompareTo(VolumeM3);
            return byVolume != 0 ? byVolume : Index.CompareTo(other.Index);
        }
    }
}
