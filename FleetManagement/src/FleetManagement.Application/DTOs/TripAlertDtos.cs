namespace FleetManagement.Application.DTOs;

public record TripAlertDto(
    Guid Id,
    Guid RouteId,
    string RouteName,
    Guid? VehicleId,
    Guid? DriverId,
    string Type,
    string Description,
    int DelayMinutes,
    string Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt
);

public record CreateTripAlertRequest(
    Guid RouteId,
    string Type,
    string Description,
    int DelayMinutes
);
