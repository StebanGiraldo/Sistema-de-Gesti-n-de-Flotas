using FleetManagement.Domain.Common;
using FleetManagement.Domain.Enums;
using NetTopologySuite.Geometries;

namespace FleetManagement.Domain.Entities;

/// <summary>
/// Representa un vehículo de la flota. La ubicación se modela con
/// NetTopologySuite.Geometries.Point (X = Longitud, Y = Latitud, SRID 4326 /
/// WGS84) para que el modelo sea compatible de forma nativa con PostgreSQL +
/// PostGIS el día que los repositorios en memoria se reemplacen por
/// Entity Framework Core (ver FleetManagement.Infrastructure/Persistence/FleetDbContext.cs).
///
/// Nota de diseño: no se crea con `new Vehicle()` directamente desde los
/// servicios de aplicación; en su lugar se usa una fábrica concreta de
/// IVehicleFactory (patrón FACTORY METHOD) que conoce los valores por
/// defecto correctos para cada tipo de vehículo.
/// </summary>
public class Vehicle : IPrototype<Vehicle>
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string LicensePlate { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public VehicleType Type { get; set; }
    public VehicleStatus Status { get; set; } = VehicleStatus.Available;
    public double CapacityKg { get; set; }
    public double MileageKm { get; set; }

    /// <summary>Posición GPS actual del vehículo.</summary>
    public Point CurrentLocation { get; set; } = new Point(0, 0) { SRID = 4326 };

    public Guid? AssignedDriverId { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastMaintenanceDate { get; set; }

    /// <summary>Actualiza la posición GPS a partir de latitud/longitud (grados decimales, WGS84).</summary>
    public void UpdateLocation(double latitude, double longitude)
    {
        CurrentLocation = new Point(longitude, latitude) { SRID = 4326 };
    }

    /// <summary>
    /// PATRÓN PROTOTYPE: crea una copia profunda de este vehículo para usarlo
    /// como plantilla al dar de alta rápidamente nuevos vehículos del mismo
    /// modelo de flota (mismo tipo, marca, modelo y capacidad), evitando
    /// repetir la captura manual de esos datos. Ver
    /// VehicleService.CloneVehicleAsync para el caso de uso completo.
    /// El identificador, la placa, el kilometraje, el conductor asignado y el
    /// historial de mantenimiento NUNCA se copian: cada vehículo clonado nace
    /// como una unidad físicamente distinta e independiente.
    /// </summary>
    public Vehicle Clone()
    {
        return new Vehicle
        {
            Id = Guid.NewGuid(),
            LicensePlate = string.Empty, // debe asignarse individualmente tras clonar
            Brand = Brand,
            Model = Model,
            Year = Year,
            Type = Type,
            Status = VehicleStatus.Available,
            CapacityKg = CapacityKg,
            MileageKm = 0,
            CurrentLocation = new Point(CurrentLocation.X, CurrentLocation.Y) { SRID = 4326 },
            AssignedDriverId = null,
            RegisteredAt = DateTime.UtcNow,
            LastMaintenanceDate = null
        };
    }
}
