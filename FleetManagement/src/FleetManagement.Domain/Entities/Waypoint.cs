using FleetManagement.Domain.Common;
using NetTopologySuite.Geometries;

namespace FleetManagement.Domain.Entities;

/// <summary>Parada intermedia dentro de una ruta de reparto.</summary>
public class Waypoint : IPrototype<Waypoint>
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Order { get; set; }
    public string Label { get; set; } = string.Empty;
    public Point Location { get; set; } = new Point(0, 0) { SRID = 4326 };

    /// <summary>Copia profunda usada por DeliveryRoute.Clone() (Prototype).</summary>
    public Waypoint Clone() => new()
    {
        Id = Guid.NewGuid(),
        Order = Order,
        Label = Label,
        Location = new Point(Location.X, Location.Y) { SRID = 4326 }
    };
}
