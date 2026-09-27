using FleetManagement.Domain.Common;
using FleetManagement.Domain.Enums;
using NetTopologySuite.Geometries;

namespace FleetManagement.Domain.Entities;

/// <summary>
/// Ruta de reparto: origen, destino, paradas intermedias, artículos de carga
/// y asignación de vehículo/conductor. Se nombra "DeliveryRoute" (y no
/// "Route") deliberadamente para evitar colisión con
/// Microsoft.AspNetCore.Routing.Route, que el SDK Web incluye en los
/// "using" implícitos del proyecto API.
///
/// La forma recomendada de construir instancias complejas de esta clase es
/// IDeliveryRouteBuilder (patrón BUILDER), no un constructor con muchos
/// parámetros.
/// </summary>
public class DeliveryRoute : IPrototype<DeliveryRoute>
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Point Origin { get; set; } = new Point(0, 0) { SRID = 4326 };
    public Point Destination { get; set; } = new Point(0, 0) { SRID = 4326 };
    public List<Waypoint> Waypoints { get; set; } = new();
    public List<CargoItem> CargoItems { get; set; } = new();
    public Guid? AssignedVehicleId { get; set; }
    public Guid? AssignedDriverId { get; set; }
    public DeliveryRouteStatus Status { get; set; } = DeliveryRouteStatus.Planned;
    public double EstimatedDistanceKm { get; set; }
    public double EstimatedDurationMinutes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ScheduledDate { get; set; }
    public int DelayMinutes { get; set; }

    /// <summary>
    /// PATRÓN PROTOTYPE: clona la ruta completa (incluyendo copias profundas
    /// de paradas y artículos de carga) para crear rápidamente rutas
    /// recurrentes (por ejemplo, "repetir esta ruta mañana") sin reconstruir
    /// cada parada manualmente con el Builder. El vehículo y el conductor
    /// asignados NO se copian: cada nueva instancia de la ruta debe asignarse
    /// explícitamente para evitar reservar por accidente los mismos recursos
    /// en dos rutas distintas. Ver DeliveryRouteService.DuplicateRouteAsync.
    /// </summary>
    public DeliveryRoute Clone()
    {
        return new DeliveryRoute
        {
            Id = Guid.NewGuid(),
            Name = $"{Name} (copia)",
            Origin = new Point(Origin.X, Origin.Y) { SRID = 4326 },
            Destination = new Point(Destination.X, Destination.Y) { SRID = 4326 },
            Waypoints = Waypoints.Select(w => w.Clone()).ToList(),
            CargoItems = CargoItems.Select(c => c.Clone()).ToList(),
            AssignedVehicleId = null,
            AssignedDriverId = null,
            Status = DeliveryRouteStatus.Planned,
            EstimatedDistanceKm = EstimatedDistanceKm,
            EstimatedDurationMinutes = EstimatedDurationMinutes,
            CreatedAt = DateTime.UtcNow,
            ScheduledDate = null,
            DelayMinutes = 0
        };
    }
}
