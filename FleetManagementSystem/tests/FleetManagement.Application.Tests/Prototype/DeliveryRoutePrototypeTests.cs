using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using NetTopologySuite.Geometries;
using Xunit;

namespace FleetManagement.Application.Tests.Prototype;

/// <summary>
/// PRUEBAS DEL PATRÓN PROTOTYPE sobre DeliveryRoute.Clone(), el caso más
/// rico del sistema: clona no sólo campos simples sino DOS colecciones
/// completas (Waypoints y CargoItems), cada una con su propio Clone()
/// (Waypoint e CargoItem también implementan IPrototype&lt;T&gt;).
///
/// La prueba clave del Prototype con colecciones es demostrar que la copia
/// es PROFUNDA: el clon debe tener sus propias instancias de Waypoint y
/// CargoItem, no referencias a las del original.
/// </summary>
public class DeliveryRoutePrototypeTests
{
    private static DeliveryRoute CreateSourceRoute()
    {
        var route = new DeliveryRoute
        {
            Name = "Ruta Semanal Bodega Norte",
            Origin = new Point(-73.1227, 7.1193) { SRID = 4326 },
            Destination = new Point(-73.1000, 7.1500) { SRID = 4326 },
            AssignedVehicleId = Guid.NewGuid(),
            AssignedDriverId = Guid.NewGuid(),
            Status = DeliveryRouteStatus.Completed,
            EstimatedDistanceKm = 12.5,
            EstimatedDurationMinutes = 30,
            ScheduledDate = DateTime.UtcNow,
            DelayMinutes = 8
        };
        route.Waypoints.Add(new Waypoint { Label = "Bodega A", Order = 0, Location = new Point(-73.12, 7.12) { SRID = 4326 } });
        route.CargoItems.Add(new CargoItem { Description = "Electrodomésticos", WeightKg = 120, VolumeM3 = 1.2, Priority = CargoPriority.High });
        return route;
    }

    [Fact]
    public void Clone_CopiesNameOriginAndDestination_ButMarksNameAsCopy()
    {
        var original = CreateSourceRoute();

        var clone = original.Clone();

        Assert.Equal($"{original.Name} (copia)", clone.Name);
        Assert.Equal(original.Origin.X, clone.Origin.X, 4);
        Assert.Equal(original.Destination.Y, clone.Destination.Y, 4);
    }

    [Fact]
    public void Clone_ResetsAssignmentAndSchedulingFields_ToAvoidDoubleBookingResources()
    {
        var original = CreateSourceRoute();

        var clone = original.Clone();

        // Regla de negocio del Prototype aquí: vehículo/conductor NO se
        // copian, para que nunca queden dos rutas reservando el mismo recurso.
        Assert.NotEqual(original.Id, clone.Id);
        Assert.Null(clone.AssignedVehicleId);
        Assert.Null(clone.AssignedDriverId);
        Assert.Null(clone.ScheduledDate);
        Assert.Equal(DeliveryRouteStatus.Planned, clone.Status);
        Assert.Equal(0, clone.DelayMinutes);
    }

    [Fact]
    public void Clone_DeepCopiesWaypoints_AsIndependentInstances()
    {
        var original = CreateSourceRoute();

        var clone = original.Clone();

        Assert.Single(clone.Waypoints);
        Assert.Equal(original.Waypoints[0].Label, clone.Waypoints[0].Label);
        Assert.NotEqual(original.Waypoints[0].Id, clone.Waypoints[0].Id);
        Assert.NotSame(original.Waypoints[0], clone.Waypoints[0]);
    }

    [Fact]
    public void Clone_DeepCopiesCargoItems_AsIndependentInstances()
    {
        var original = CreateSourceRoute();

        var clone = original.Clone();

        Assert.Single(clone.CargoItems);
        Assert.Equal(original.CargoItems[0].WeightKg, clone.CargoItems[0].WeightKg);
        Assert.NotEqual(original.CargoItems[0].Id, clone.CargoItems[0].Id);
        Assert.NotSame(original.CargoItems[0], clone.CargoItems[0]);
    }

    [Fact]
    public void Clone_ThenMutatingCloneCollections_DoesNotAffectOriginal()
    {
        // La prueba definitiva de "copia profunda": modificar las
        // colecciones del clon (agregar un waypoint nuevo) no debe alterar
        // en absoluto al original.
        var original = CreateSourceRoute();
        var clone = original.Clone();

        clone.Waypoints.Add(new Waypoint { Label = "Parada extra sólo del clon", Order = 1 });

        Assert.Single(original.Waypoints);
        Assert.Equal(2, clone.Waypoints.Count);
    }

    [Fact]
    public void WaypointClone_CopiesLabelAndOrder_ButAssignsNewId()
    {
        var original = new Waypoint { Label = "Peaje Norte", Order = 3, Location = new Point(-73.05, 7.05) { SRID = 4326 } };

        var clone = original.Clone();

        Assert.Equal(original.Label, clone.Label);
        Assert.Equal(original.Order, clone.Order);
        Assert.NotEqual(original.Id, clone.Id);
    }

    [Fact]
    public void CargoItemClone_CopiesDescriptionWeightAndPriority_ButAssignsNewId()
    {
        var original = new CargoItem { Description = "Vidrio", WeightKg = 15, VolumeM3 = 0.1, Priority = CargoPriority.Fragile };

        var clone = original.Clone();

        Assert.Equal(original.Description, clone.Description);
        Assert.Equal(original.Priority, clone.Priority);
        Assert.NotEqual(original.Id, clone.Id);
    }
}
