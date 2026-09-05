namespace FleetManagement.Application.DTOs;

public record WaypointDto(double Latitude, double Longitude, string Label, int Order);

public record CargoItemDto(string Description, double WeightKg, double VolumeM3, string Priority);

public record DeliveryRouteDto(
    Guid Id,
    string Name,
    double OriginLat,
    double OriginLng,
    double DestinationLat,
    double DestinationLng,
    List<WaypointDto> Waypoints,
    List<CargoItemDto> CargoItems,
    Guid? AssignedVehicleId,
    string? AssignedVehiclePlate,
    Guid? AssignedDriverId,
    string? AssignedDriverName,
    string Status,
    double EstimatedDistanceKm,
    double EstimatedDurationMinutes,
    DateTime CreatedAt,
    DateTime? ScheduledDate,
    int DelayMinutes
);

public record CreateDeliveryRouteRequest(
    string Name,
    double OriginLat,
    double OriginLng,
    double DestinationLat,
    double DestinationLng,
    List<WaypointDto>? Waypoints,
    List<CargoItemDto>? CargoItems,
    Guid? AssignedVehicleId,
    Guid? AssignedDriverId,
    DateTime? ScheduledDate
);

public record CreateExpressRouteRequest(
    string Name,
    double OriginLat,
    double OriginLng,
    double DestinationLat,
    double DestinationLng,
    Guid AssignedVehicleId,
    Guid AssignedDriverId
);

public record DuplicateRouteRequest(DateTime? NewScheduledDate);

public record UpdateRouteStatusRequest(string Status);
