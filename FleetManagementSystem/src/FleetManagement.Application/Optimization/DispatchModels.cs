using FleetManagement.Application.Geo;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Optimization;

/// <summary>Una entrega por repartir: qué se lleva, cuánto pesa y ocupa, qué tan urgente es y a dónde va.</summary>
public sealed record PendingDelivery(
    string Description,
    double WeightKg,
    double VolumeM3,
    CargoPriority Priority,
    GeoCoordinate Destination,
    string DestinationLabel)
{
    /// <summary>
    /// Qué tan pronto debe entregarse: Urgente (3) antes que Alta (2), Frágil (1) y Estándar (0). La carga frágil va
    /// antes que la estándar porque cuanto menos tiempo pase en el vehículo, menos riesgo corre.
    /// </summary>
    public int PriorityRank => Priority switch
    {
        CargoPriority.Urgent => 3,
        CargoPriority.High => 2,
        CargoPriority.Fragile => 1,
        _ => 0
    };
}

/// <summary>Un vehículo disponible con lo que importa para repartirle carga: capacidad, posición y velocidad de su tipo.</summary>
public sealed record CandidateVehicle(
    Guid Id,
    string LicensePlate,
    VehicleType Type,
    double CapacityKg,
    double? CapacityM3,
    GeoCoordinate Location,
    double SpeedKmh);

/// <summary>Las entregas que le tocan a un vehículo, en el orden en que se le asignaron.</summary>
public sealed record VehicleLoad(CandidateVehicle Vehicle, IReadOnlyList<PendingDelivery> Deliveries)
{
    public double TotalWeightKg => Deliveries.Sum(delivery => delivery.WeightKg);
    public double TotalVolumeM3 => Deliveries.Sum(delivery => delivery.VolumeM3);
}

/// <summary>Una entrega que no se pudo asignar, con el motivo (para que quien planifica sepa qué hacer).</summary>
public sealed record UnassignedDelivery(PendingDelivery Delivery, string Reason);

/// <summary>Resultado de repartir las entregas entre los vehículos: solo aparecen los vehículos que llevan algo.</summary>
public sealed record CargoAssignment(IReadOnlyList<VehicleLoad> Loads, IReadOnlyList<UnassignedDelivery> Unassigned);

/// <summary>El viaje de un vehículo: las paradas en el orden en que se visitan, su largo y lo que tarda a la velocidad de su tipo.</summary>
public sealed record PlannedTrip(VehicleLoad Load, IReadOnlyList<PendingDelivery> Stops, double DistanceKm, double Minutes);

/// <summary>Plan completo: los viajes y las entregas que quedaron sin asignar, con los totales con los que se compara contra otro plan.</summary>
public sealed record PlannedDispatch(IReadOnlyList<PlannedTrip> Trips, IReadOnlyList<UnassignedDelivery> Unassigned)
{
    public int AssignedCount => Trips.Sum(trip => trip.Stops.Count);
    public int VehiclesUsed => Trips.Count;
    public double TotalKm => Trips.Sum(trip => trip.DistanceKm);

    /// <summary>Suma de los tiempos de conducción de todos los viajes.</summary>
    public double TotalMinutes => Trips.Sum(trip => trip.Minutes);

    /// <summary>Lo que tarda el viaje más largo: cuándo termina de entregarse todo si los vehículos salen a la vez.</summary>
    public double LongestTripMinutes => Trips.Count == 0 ? 0 : Trips.Max(trip => trip.Minutes);
}
