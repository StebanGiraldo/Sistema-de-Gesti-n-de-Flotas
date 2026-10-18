using System.Globalization;
using FleetManagement.Application.DTOs;
using FleetManagement.Infrastructure.Navigation;
using Xunit;

namespace FleetManagement.Application.Tests.Proxy;

/// <summary>
/// Pruebas de la clave de caché (<see cref="NavigationCacheKey"/>): fijan el formato exacto (que es lo que decide cuándo
/// dos pedidos comparten entrada) y sus reglas: 6 decimales, cultura invariante, paradas por <c>Order</c>.
/// </summary>
public class NavigationCacheKeyTests
{
    private static readonly WaypointDto First = new(7.15, -73.15, "Primera", 0);
    private static readonly WaypointDto Second = new(7.17, -73.17, "Segunda", 1);

    [Fact]
    public void From_WritesOriginThenWaypointsInVisitOrderThenDestination_WithSixDecimals()
    {
        var key = NavigationCacheKey.From(7.1, -73.1, 7.2, -73.2, new[] { Second, First });

        Assert.Equal("7.100000,-73.100000>7.150000,-73.150000>7.170000,-73.170000>7.200000,-73.200000", key.Value);
    }

    [Fact]
    public void From_WithoutWaypoints_IsJustOriginAndDestination()
    {
        var key = NavigationCacheKey.From(7.1, -73.1, 7.2, -73.2, Array.Empty<WaypointDto>());

        Assert.Equal("7.100000,-73.100000>7.200000,-73.200000", key.Value);
    }

    [Fact]
    public void From_RoundsToSixDecimals_AndTreatsNegativeZeroAsZero()
    {
        var key = NavigationCacheKey.From(7.1234567, -0.0000001, 7.2, 0.0, Array.Empty<WaypointDto>());

        Assert.Equal("7.123457,0.000000>7.200000,0.000000", key.Value);
    }

    [Fact]
    public void From_UsesTheInvariantCulture_EvenWhenTheMachineUsesADecimalComma()
    {
        var commaCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        commaCulture.NumberFormat.NumberDecimalSeparator = ",";
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = commaCulture;
            var key = NavigationCacheKey.From(7.1, -73.1, 7.2, -73.2, Array.Empty<WaypointDto>());

            Assert.Equal("7.100000,-73.100000>7.200000,-73.200000", key.Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void TwoKeysBuiltFromTheSameRoute_AreEqual_AndHaveTheSameHash()
    {
        var one = NavigationCacheKey.From(7.1, -73.1, 7.2, -73.2, new[] { First, Second });
        var other = NavigationCacheKey.From(7.1, -73.1, 7.2, -73.2, new[] { Second, First });

        Assert.Equal(one, other);
        Assert.Equal(one.GetHashCode(), other.GetHashCode());
    }

    [Fact]
    public void ARouteAndItsReverse_AreDifferentKeys()
    {
        var forward = NavigationCacheKey.From(7.1, -73.1, 7.2, -73.2, Array.Empty<WaypointDto>());
        var backward = NavigationCacheKey.From(7.2, -73.2, 7.1, -73.1, Array.Empty<WaypointDto>());

        Assert.NotEqual(forward, backward);
    }

    [Fact]
    public void AWaypointIsNotTheSameAsAnEndpoint()
    {
        // Origen A, destino C, parada B contra origen A, destino B: no deben confundirse aunque compartan puntos.
        var withStop = NavigationCacheKey.From(7.1, -73.1, 7.3, -73.3, new[] { new WaypointDto(7.2, -73.2, "B", 0) });
        var direct = NavigationCacheKey.From(7.1, -73.1, 7.2, -73.2, Array.Empty<WaypointDto>());

        Assert.NotEqual(withStop, direct);
    }

    [Fact]
    public void From_RejectsANullWaypointList()
    {
        Assert.Throws<ArgumentNullException>(() => NavigationCacheKey.From(7.1, -73.1, 7.2, -73.2, null!));
    }
}
