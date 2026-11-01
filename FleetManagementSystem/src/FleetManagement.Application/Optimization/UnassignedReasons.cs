using System.Globalization;

namespace FleetManagement.Application.Optimization;

/// <summary>Motivos por los que una entrega no se pudo asignar, redactados para quien planifica.</summary>
internal static class UnassignedReasons
{
    public static string For(PendingDelivery delivery, IReadOnlyList<CandidateVehicle> vehicles)
    {
        if (vehicles.Count == 0)
            return "No hay vehículos disponibles.";

        if (vehicles.Any(vehicle => new LoadBin(vehicle).Fits(delivery)))
            return "Los vehículos disponibles ya no tienen espacio libre para esta entrega.";

        var largestKg = vehicles.Max(vehicle => vehicle.CapacityKg);
        return $"Excede la capacidad de todos los vehículos disponibles ({Format(delivery.WeightKg)} kg y {Format(delivery.VolumeM3)} m³; " +
               $"el de mayor capacidad admite {Format(largestKg)} kg).";
    }

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
