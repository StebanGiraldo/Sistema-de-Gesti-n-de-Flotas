using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using NetTopologySuite.Geometries;

namespace FleetManagement.Application.Builders;

/// <summary>Implementación concreta del Builder de rutas de reparto.</summary>
public class DeliveryRouteBuilder : IDeliveryRouteBuilder
{
    private DeliveryRoute _route = new();
    private int _waypointOrder;
    private bool _originSet;
    private bool _destinationSet;

    public IDeliveryRouteBuilder WithName(string name)
    {
        _route.Name = name;
        return this;
    }

    public IDeliveryRouteBuilder WithOrigin(double latitude, double longitude)
    {
        _route.Origin = new Point(longitude, latitude) { SRID = 4326 };
        _originSet = true;
        return this;
    }

    public IDeliveryRouteBuilder WithDestination(double latitude, double longitude)
    {
        _route.Destination = new Point(longitude, latitude) { SRID = 4326 };
        _destinationSet = true;
        return this;
    }

    public IDeliveryRouteBuilder AddWaypoint(double latitude, double longitude, string label)
    {
        _route.Waypoints.Add(new Waypoint
        {
            Location = new Point(longitude, latitude) { SRID = 4326 },
            Label = label,
            Order = _waypointOrder++
        });
        return this;
    }

    public IDeliveryRouteBuilder AddCargoItem(string description, double weightKg, double volumeM3, CargoPriority priority)
    {
        _route.CargoItems.Add(new CargoItem
        {
            Description = description,
            WeightKg = weightKg,
            VolumeM3 = volumeM3,
            Priority = priority
        });
        return this;
    }

    public IDeliveryRouteBuilder AssignVehicle(Guid vehicleId)
    {
        _route.AssignedVehicleId = vehicleId;
        return this;
    }

    public IDeliveryRouteBuilder AssignDriver(Guid driverId)
    {
        _route.AssignedDriverId = driverId;
        return this;
    }

    public IDeliveryRouteBuilder ScheduleFor(DateTime date)
    {
        _route.ScheduledDate = date;
        return this;
    }

    public IDeliveryRouteBuilder WithEstimatedTrip(double distanceKm, double durationMinutes)
    {
        _route.EstimatedDistanceKm = distanceKm;
        _route.EstimatedDurationMinutes = durationMinutes;
        return this;
    }

    public DeliveryRoute Build()
    {
        if (string.IsNullOrWhiteSpace(_route.Name))
            throw new InvalidOperationException("La ruta debe tener un nombre antes de construirse (WithName).");
        if (!_originSet || !_destinationSet)
            throw new InvalidOperationException("La ruta debe tener origen y destino definidos (WithOrigin/WithDestination).");

        var result = _route;

        // Reinicia el estado interno para que el mismo builder pueda reutilizarse en una siguiente construcción.
        _route = new DeliveryRoute();
        _waypointOrder = 0;
        _originSet = false;
        _destinationSet = false;

        return result;
    }
}
