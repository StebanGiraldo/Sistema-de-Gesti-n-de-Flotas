using FleetManagement.Domain.Entities;

namespace FleetManagement.Application.RouteSheets;

/// <summary>
/// Compara la carga de una ruta con lo que el vehículo puede llevar, con el mismo criterio que las reglas de asignación: pesa
/// contra <c>CapacityKg</c>, el volumen contra <c>CapacityM3</c> solo si el vehículo lo declara, y una tolerancia mínima para que
/// una carga justo del tamaño del vehículo no figure como excedida por un error de redondeo.
/// </summary>
internal static class CargoCapacitySummary
{
    private const double Tolerance = 1e-9;
    private const double PercentScale = 100.0;
    private const int WeightDecimals = 3;
    private const int PercentDecimals = 1;

    public static RouteSheetCargo Of(IReadOnlyCollection<CargoItem> items, Vehicle vehicle)
    {
        var weightKg = items.Sum(item => item.WeightKg);
        var volumeM3 = items.Sum(item => item.VolumeM3);

        return new RouteSheetCargo(
            items.Count,
            Math.Round(weightKg, WeightDecimals),
            vehicle.CapacityKg,
            UsagePercent(weightKg, vehicle.CapacityKg),
            Math.Round(volumeM3, WeightDecimals),
            vehicle.CapacityM3,
            Exceeds(weightKg, volumeM3, vehicle));
    }

    // Un vehículo sin capacidad declarada no tiene un porcentaje con sentido: se informa 0 y el exceso lo marca ExceedsCapacity.
    private static double UsagePercent(double weightKg, double capacityKg)
        => capacityKg > 0 ? Math.Round(PercentScale * weightKg / capacityKg, PercentDecimals) : 0;

    private static bool Exceeds(double weightKg, double volumeM3, Vehicle vehicle)
        => weightKg > vehicle.CapacityKg + Tolerance
           || (vehicle.CapacityM3 is { } capacityM3 && volumeM3 > capacityM3 + Tolerance);
}
