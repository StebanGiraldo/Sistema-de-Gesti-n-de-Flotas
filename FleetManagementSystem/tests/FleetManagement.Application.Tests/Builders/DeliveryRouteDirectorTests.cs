using FleetManagement.Application.Builders;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Builders;

/// <summary>
/// PRUEBAS DEL PATRÓN BUILDER — foco en el Director (DeliveryRouteDirector).
///
/// El Director encapsula una "receta" reutilizable (ruta express) sobre
/// CUALQUIER IDeliveryRouteBuilder que se le inyecte. Estas pruebas
/// verifican que la receta ensambla siempre las mismas piezas obligatorias
/// (carga urgente, fecha de hoy) sin que el llamador tenga que conocer el
/// orden de llamadas al builder.
/// </summary>
public class DeliveryRouteDirectorTests
{
    [Fact]
    public void BuildExpressRoute_CreatesRoute_WithUrgentCargoItemByDefault()
    {
        var director = new DeliveryRouteDirector();
        IDeliveryRouteBuilder builder = new DeliveryRouteBuilder();
        var vehicleId = Guid.NewGuid();
        var driverId = Guid.NewGuid();

        var route = director.BuildExpressRoute(
            builder,
            name: "Express Terminal-Aeropuerto",
            originLat: 7.1193, originLng: -73.1227,
            destinationLat: 7.0650, destinationLng: -73.0900,
            vehicleId, driverId,
            estimatedDistanceKm: 18.5,
            estimatedDurationMinutes: 25);

        var cargo = Assert.Single(route.CargoItems);
        Assert.Equal(CargoPriority.Urgent, cargo.Priority);
        Assert.Equal("Envío urgente", cargo.Description);
    }

    [Fact]
    public void BuildExpressRoute_AssignsVehicleDriverAndTripEstimates()
    {
        var director = new DeliveryRouteDirector();
        IDeliveryRouteBuilder builder = new DeliveryRouteBuilder();
        var vehicleId = Guid.NewGuid();
        var driverId = Guid.NewGuid();

        var route = director.BuildExpressRoute(
            builder, "Express", 7.10, -73.10, 7.20, -73.20,
            vehicleId, driverId, estimatedDistanceKm: 10, estimatedDurationMinutes: 15);

        Assert.Equal(vehicleId, route.AssignedVehicleId);
        Assert.Equal(driverId, route.AssignedDriverId);
        Assert.Equal(10, route.EstimatedDistanceKm);
        Assert.Equal(15, route.EstimatedDurationMinutes);
        Assert.NotNull(route.ScheduledDate);
    }

    [Fact]
    public void BuildExpressRoute_WorksWithAnyIDeliveryRouteBuilderImplementation()
    {
        // Demuestra que el Director depende sólo de la ABSTRACCIÓN
        // IDeliveryRouteBuilder, no de la clase concreta DeliveryRouteBuilder
        // (principio de inversión de dependencias propio del patrón Builder).
        var director = new DeliveryRouteDirector();
        IDeliveryRouteBuilder builder = new DeliveryRouteBuilder();

        var route = director.BuildExpressRoute(
            builder, "Express genérica", 7.0, -73.0, 7.1, -73.1,
            Guid.NewGuid(), Guid.NewGuid(), 5, 10);

        Assert.Equal("Express genérica", route.Name);
    }
}
