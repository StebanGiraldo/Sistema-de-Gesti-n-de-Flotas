using FleetManagement.Application.Geo;
using Xunit;

namespace FleetManagement.Application.Tests.Geo;

public class TripEstimateTests
{
    [Fact]
    public void DefaultSpeed_IsTheAverageForMixedRoadsThatTheServiceAlwaysUsed() => Assert.Equal(55.0, TripEstimate.DefaultSpeedKmh);

    [Theory]
    [InlineData(110, 55, 120)]
    [InlineData(80, 80, 60)]
    [InlineData(50, 100, 30)]
    [InlineData(0, 80, 0)]
    public void Minutes_IsDistanceOverSpeedInHours(double km, double speedKmh, double expectedMinutes)
        => Assert.Equal(expectedMinutes, TripEstimate.Minutes(km, speedKmh), 9);

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Minutes_RejectsASpeedThatIsNotPositive(double speedKmh)
        => Assert.Throws<ArgumentOutOfRangeException>(() => TripEstimate.Minutes(10, speedKmh));
}
