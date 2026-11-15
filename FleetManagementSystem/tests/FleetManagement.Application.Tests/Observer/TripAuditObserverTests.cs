using System.Globalization;
using FleetManagement.Application.Monitoring;
using FleetManagement.Application.Tests.Decorator;
using Xunit;

namespace FleetManagement.Application.Tests.Observer;

/// <summary>
/// OBSERVADOR CONCRETO #1: deja en la bitácora de auditoría (Singleton, vía <c>IFleetAuditLogger</c>) constancia de cada parada que
/// alcanza un vehículo. Solo registra paradas: el movimiento ocurre en cada paso (sería ruido) y la llegada ya la audita
/// <c>DeliveryRouteService</c> al completar la ruta (se duplicaría).
/// </summary>
public class TripAuditObserverTests
{
    private readonly RecordingAuditLogger _audit = new();

    private TripAuditObserver Observer() => new(_audit);

    [Fact]
    public void AReachedStop_LeavesOneAuditEntry_WithTheVehicleTheStopAndTheProgress()
    {
        Observer().OnTelemetry(Telemetry.Stop(label: "Terminal de carga", plate: "TRK-001", percent: 16.7));

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("Telemetría", entry.Category);
        Assert.Equal(TripAuditObserver.AuditCategory, entry.Category);
        Assert.Equal("El vehículo TRK-001 alcanzó la parada 'Terminal de carga' (16.7 % del recorrido).", entry.Message);
    }

    [Theory]
    [InlineData(50.0, "50")]
    [InlineData(16.66666, "16.7")]
    [InlineData(0.04, "0")]
    [InlineData(99.95, "100")]
    public void TheProgress_IsShownWithAtMostOneDecimal(double percent, string expected)
    {
        Observer().OnTelemetry(Telemetry.Stop(percent: percent));

        Assert.Contains($"({expected} % del recorrido)", Assert.Single(_audit.Entries).Message);
    }

    [Fact]
    public void TheProgress_UsesADotAsDecimalSeparator_WhateverTheCultureOfTheServer()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-CO");   // en esa cultura el separador decimal es la coma

            Observer().OnTelemetry(Telemetry.Stop(percent: 16.7));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }

        Assert.Contains("(16.7 % del recorrido)", Assert.Single(_audit.Entries).Message);
    }

    [Fact]
    public void Movement_IsNotAudited_ItHappensEveryStep()
    {
        Observer().OnTelemetry(Telemetry.Moved());

        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Arrival_IsNotAudited_TheRouteServiceAlreadyDoesItWhenTheRouteIsCompleted()
    {
        Observer().OnTelemetry(Telemetry.Arrived());

        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void EachStop_IsAuditedOnce()
    {
        var observer = Observer();

        observer.OnTelemetry(Telemetry.Stop(label: "Primera"));
        observer.OnTelemetry(Telemetry.Moved());
        observer.OnTelemetry(Telemetry.Stop(label: "Segunda"));
        observer.OnTelemetry(Telemetry.Arrived());

        Assert.Equal(2, _audit.Entries.Count);
        Assert.Contains("'Primera'", _audit.Entries[0].Message);
        Assert.Contains("'Segunda'", _audit.Entries[1].Message);
    }

    [Fact]
    public void ThereIsNoWayToBuildItWithoutAnAuditLogger()
    {
        Assert.Throws<ArgumentNullException>(() => new TripAuditObserver(null!));
    }

    [Fact]
    public void ItIsAnObserver_AndNothingElse_ItKnowsNothingOfThePublisher()
    {
        Assert.True(typeof(ITelemetryObserver).IsAssignableFrom(typeof(TripAuditObserver)));
        var dependencies = typeof(TripAuditObserver).GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType);
        Assert.Equal(new[] { typeof(FleetManagement.Application.Interfaces.IFleetAuditLogger) }, dependencies);
    }
}
