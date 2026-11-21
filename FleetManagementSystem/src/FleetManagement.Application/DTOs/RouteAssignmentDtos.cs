namespace FleetManagement.Application.DTOs;

/// <summary>
/// Asignación de conductor y/o vehículo a una ruta que aún no los tiene. Se puede mandar uno solo (el que falta) o los dos.
/// </summary>
public record AssignRouteRequest(Guid? DriverId, Guid? VehicleId);

/// <summary>Conductor que hoy se puede asignar: no está en ningún viaje activo (En progreso o Retrasada).</summary>
public record AssignableDriverDto(Guid Id, string FullName, string LicenseNumber);

/// <summary>Vehículo que hoy se puede asignar a una ruta concreta: opera, sin mantenimiento vencido, la carga cabe y no está en un viaje activo.</summary>
public record AssignableVehicleDto(Guid Id, string LicensePlate, string Type, string Status, double CapacityKg, double? CapacityM3);

/// <summary>Lo que se puede elegir en el selector "Asignar" de una ruta; lo calcula el servidor con las mismas reglas con que valida.</summary>
public record RouteAssignmentOptionsDto(
    IReadOnlyList<AssignableDriverDto> Drivers,
    IReadOnlyList<AssignableVehicleDto> Vehicles);
