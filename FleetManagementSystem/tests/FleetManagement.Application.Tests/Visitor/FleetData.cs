using FleetManagement.Application.Analysis;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Tests.Visitor;

/// <summary>
/// Entidades de prueba con solo los datos que importan al análisis, para que cada prueba muestre únicamente lo que le interesa
/// y cada resultado esperado se pueda comprobar a mano.
/// </summary>
public static class FleetData
{
    public static Vehicle Vehicle(string plate, VehicleType type = VehicleType.Van, VehicleStatus status = VehicleStatus.Available, double mileageKm = 0)
        => new() { LicensePlate = plate, Type = type, Status = status, MileageKm = mileageKm };

    public static DeliveryRoute Route(
        string name,
        DeliveryRouteStatus status = DeliveryRouteStatus.Planned,
        Vehicle? vehicle = null,
        double distanceKm = 0,
        int stops = 0,
        double durationMinutes = 0,
        int delayMinutes = 0)
    {
        var route = new DeliveryRoute
        {
            Name = name,
            Status = status,
            AssignedVehicleId = vehicle?.Id,
            EstimatedDistanceKm = distanceKm,
            EstimatedDurationMinutes = durationMinutes,
            DelayMinutes = delayMinutes
        };
        for (var order = 0; order < stops; order++)
            route.Waypoints.Add(new Waypoint { Order = order, Label = $"Parada {order + 1}" });
        return route;
    }

    public static MaintenanceRecord Record(Vehicle vehicle, MaintenanceType type, DateTime performedAt, double mileageAtServiceKm)
        => new() { VehicleId = vehicle.Id, Type = type, PerformedAt = performedAt, MileageAtServiceKm = mileageAtServiceKm };

    public static FleetElementStructure Structure(
        IEnumerable<Vehicle>? vehicles = null,
        IEnumerable<DeliveryRoute>? routes = null,
        IEnumerable<MaintenanceRecord>? records = null)
        => FleetElementStructure.Build(
            vehicles ?? Enumerable.Empty<Vehicle>(),
            routes ?? Enumerable.Empty<DeliveryRoute>(),
            records ?? Enumerable.Empty<MaintenanceRecord>());
}
