namespace FleetManagement.Application.DTOs;

/// <summary>Representación plana de un Vehicle para la API/frontend (evita serializar tipos de NetTopologySuite directamente).</summary>
public record VehicleDto(
    Guid Id,
    string LicensePlate,
    string Brand,
    string Model,
    int Year,
    string Type,
    string Status,
    double CapacityKg,
    double MileageKm,
    double Latitude,
    double Longitude,
    Guid? AssignedDriverId,
    string? AssignedDriverName,
    DateTime RegisteredAt,
    DateTime? LastMaintenanceDate
);

public record CreateVehicleRequest(
    string LicensePlate,
    string Brand,
    string Model,
    int Year,
    string Type,
    double Latitude,
    double Longitude
);

public record CloneVehicleRequest(string NewLicensePlate);

public record UpdateVehicleStatusRequest(string Status);

/// <summary>Representación plana de un Driver para la API/frontend (listados y selectores del dashboard).</summary>
public record DriverDto(Guid Id, string FullName, string LicenseNumber, string Phone, Guid? AssignedVehicleId, string? AssignedVehiclePlate);
