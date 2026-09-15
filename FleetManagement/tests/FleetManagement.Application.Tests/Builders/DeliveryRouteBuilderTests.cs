using FleetManagement.Application.Builders;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Builders;

/// <summary>
/// PRUEBAS DEL PATRÓN BUILDER (nivel unitario, sobre DeliveryRouteBuilder
/// directamente, sin Director).
///
/// Una DeliveryRoute tiene piezas opcionales y de longitud variable
/// (waypoints, artículos de carga). Estas pruebas verifican que:
///  1) la interfaz fluida ensambla correctamente cada pieza,
///  2) Build() valida precondiciones antes de entregar el objeto,
///  3) el builder se reinicia después de Build() para poder reutilizarse.
/// </summary>
public class DeliveryRouteBuilderTests
{
    [Fact]
    public void Build_WithNameOriginAndDestination_CreatesRouteWithThoseValues()
    {
        IDeliveryRouteBuilder builder = new DeliveryRouteBuilder();

        var route = builder
            .WithName("Ruta Centro-Norte")
            .WithOrigin(7.1193, -73.1227)
            .WithDestination(7.1500, -73.1000)
            .Build();

        Assert.Equal("Ruta Centro-Norte", route.Name);
        Assert.Equal(7.1193, route.Origin.Y, 4);
        Assert.Equal(-73.1227, route.Origin.X, 4);
        Assert.Equal(7.1500, route.Destination.Y, 4);
    }

    [Fact]
    public void AddWaypoint_CalledMultipleTimes_PreservesInsertionOrder()
    {
        IDeliveryRouteBuilder builder = new DeliveryRouteBuilder();

        var route = builder
            .WithName("Ruta con paradas")
            .WithOrigin(7.10, -73.10)
            .WithDestination(7.20, -73.20)
            .AddWaypoint(7.12, -73.12, "Bodega A")
            .AddWaypoint(7.14, -73.14, "Bodega B")
            .Build();

        Assert.Equal(2, route.Waypoints.Count);
        Assert.Equal("Bodega A", route.Waypoints[0].Label);
        Assert.Equal(0, route.Waypoints[0].Order);
        Assert.Equal("Bodega B", route.Waypoints[1].Label);
        Assert.Equal(1, route.Waypoints[1].Order);
    }

    [Fact]
    public void AddCargoItem_AddsItemWithGivenProperties()
    {
        IDeliveryRouteBuilder builder = new DeliveryRouteBuilder();

        var route = builder
            .WithName("Ruta con carga")
            .WithOrigin(7.10, -73.10)
            .WithDestination(7.20, -73.20)
            .AddCargoItem("Repuestos", 25.5, 0.3, CargoPriority.Fragile)
            .Build();

        var cargo = Assert.Single(route.CargoItems);
        Assert.Equal("Repuestos", cargo.Description);
        Assert.Equal(CargoPriority.Fragile, cargo.Priority);
    }

    [Fact]
    public void Build_WithoutName_ThrowsInvalidOperationException()
    {
        IDeliveryRouteBuilder builder = new DeliveryRouteBuilder();

        builder.WithOrigin(7.10, -73.10).WithDestination(7.20, -73.20);

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_WithoutOriginOrDestination_ThrowsInvalidOperationException()
    {
        IDeliveryRouteBuilder builder = new DeliveryRouteBuilder();

        builder.WithName("Ruta incompleta");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_ResetsInternalState_SoSameBuilderCanBeReusedForANewRoute()
    {
        IDeliveryRouteBuilder builder = new DeliveryRouteBuilder();

        var first = builder
            .WithName("Primera ruta")
            .WithOrigin(7.10, -73.10)
            .WithDestination(7.20, -73.20)
            .AddWaypoint(7.15, -73.15, "Parada única de la primera ruta")
            .Build();

        // Si el builder no se reinicia, la segunda ruta arrastraría el
        // waypoint de la primera; esto demuestra que no ocurre.
        var second = builder
            .WithName("Segunda ruta")
            .WithOrigin(8.00, -74.00)
            .WithDestination(8.10, -74.10)
            .Build();

        Assert.NotEqual(first.Id, second.Id);
        Assert.Single(first.Waypoints);
        Assert.Empty(second.Waypoints);
        Assert.Equal("Segunda ruta", second.Name);
    }
}
