namespace FleetManagement.Application.DTOs;

/// <summary>Una entrega que se quiere repartir: qué es, cuánto pesa y ocupa, qué tan urgente es y a dónde va.</summary>
public record DispatchDeliveryRequest(
    string Description,
    double WeightKg,
    double VolumeM3,
    string? Priority,
    double Latitude,
    double Longitude);

/// <summary>
/// Pide un plan de reparto: desde dónde salen todos los vehículos y qué hay que entregar. <paramref name="VehicleIds"/>
/// limita el plan a esos vehículos (si se omite se usan todos los disponibles); <paramref name="ScheduledDate"/> solo se
/// usa al aplicar el plan.
/// </summary>
public record DispatchPlanRequest(
    double OriginLat,
    double OriginLng,
    List<DispatchDeliveryRequest>? Deliveries,
    List<Guid>? VehicleIds = null,
    DateTime? ScheduledDate = null);

public record DispatchDeliveryDto(string Description, double WeightKg, double VolumeM3, string Priority, double Latitude, double Longitude);

/// <summary>El viaje de un vehículo: qué lleva, en qué orden visita las paradas y cuánto recorre y tarda.</summary>
public record DispatchTripDto(
    Guid VehicleId,
    string LicensePlate,
    string VehicleType,
    double CapacityKg,
    double? CapacityM3,
    double LoadKg,
    double LoadM3,
    double LoadPercent,
    double SpeedKmh,
    List<DispatchDeliveryDto> Stops,
    double DistanceKm,
    double EstimatedMinutes);

public record DispatchUnassignedDto(DispatchDeliveryDto Delivery, string Reason);

public record DispatchExcludedVehicleDto(Guid VehicleId, string LicensePlate, string Reason);

public record DispatchTotalsDto(int AssignedCount, int UnassignedCount, int VehiclesUsed, double TotalKm, double TotalMinutes, double LongestTripMinutes);

/// <summary>
/// Plan optimizado frente al plan sin optimizar. Solo es comparable si ambos asignan la misma cantidad de entregas
/// (<paramref name="Comparable"/>); un ahorro negativo significa que el optimizado recorre más.
/// </summary>
public record DispatchComparisonDto(bool Comparable, double KmSaved, double KmSavedPercent, double MinutesSaved, string? Note);

public record DispatchPlanDto(
    double OriginLat,
    double OriginLng,
    List<DispatchTripDto> Trips,
    List<DispatchUnassignedDto> Unassigned,
    List<DispatchExcludedVehicleDto> ExcludedVehicles,
    DispatchTotalsDto Totals,
    DispatchTotalsDto Baseline,
    DispatchComparisonDto Comparison);

public record DispatchApplyResultDto(
    List<DeliveryRouteDto> CreatedRoutes,
    List<DispatchUnassignedDto> Unassigned,
    DispatchTotalsDto Totals);
