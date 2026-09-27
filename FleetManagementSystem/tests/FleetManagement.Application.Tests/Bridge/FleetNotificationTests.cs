using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Notifications;
using Xunit;

namespace FleetManagement.Application.Tests.Bridge;

/// <summary>
/// PRUEBAS DEL PATRÓN BRIDGE sobre la Abstracción (<see cref="FleetNotification"/>)
/// y sus especializaciones. La prueba más importante de este archivo es
/// <see cref="MaintenanceNotification_CanUseDifferentChannels_WithoutCreatingCombinedSubclasses"/>,
/// que demuestra exactamente lo pedido en el encargo: "una misma notificación
/// puede utilizar diferentes canales sin crear clases combinadas".
/// </summary>
public class FleetNotificationTests
{
    [Fact]
    public async Task MaintenanceNotification_CanUseDifferentChannels_WithoutCreatingCombinedSubclasses()
    {
        // Dos canales distintos, sin ninguna clase "MaintenanceEmailNotification"
        // ni "MaintenanceSmsNotification": la combinación se logra por
        // COMPOSICIÓN, pasando un canal distinto al mismo tipo de notificación.
        var emailChannel = new RecordingNotificationChannel();
        var smsChannel = new RecordingNotificationChannel();

        var viaEmail = new MaintenanceNotification(emailChannel, "conductor@demo.test", "TES-001", "OilChange", DateTime.UtcNow.AddMonths(3), 15000);
        var viaSms = new MaintenanceNotification(smsChannel, "+57 300 555 0101", "TES-001", "OilChange", DateTime.UtcNow.AddMonths(3), 15000);

        await viaEmail.SendAsync();
        await viaSms.SendAsync();

        Assert.Single(emailChannel.SentMessages);
        Assert.Single(smsChannel.SentMessages);
        Assert.Equal("conductor@demo.test", emailChannel.SentMessages[0].Recipient);
        Assert.Equal("+57 300 555 0101", smsChannel.SentMessages[0].Recipient);

        // Mismo tipo de notificación en ambos casos: no existe ninguna
        // subclase combinada tipo x canal.
        Assert.IsType<MaintenanceNotification>(viaEmail);
        Assert.IsType<MaintenanceNotification>(viaSms);
        Assert.Equal(viaEmail.GetType(), viaSms.GetType());
    }

    [Fact]
    public async Task MaintenanceNotification_WithDateAndMileage_IncludesBothInMessage()
    {
        var channel = new RecordingNotificationChannel();
        var notification = new MaintenanceNotification(channel, "ops@demo.test", "ABC-123", "Tires", new DateTime(2027, 3, 15), 20000);

        await notification.SendAsync();

        var sent = Assert.Single(channel.SentMessages);
        Assert.Contains("ABC-123", sent.Subject);
        Assert.Contains("Tires", sent.Message);
        Assert.Contains("2027-03-15", sent.Message);
        Assert.Contains("20,000 km", sent.Message);
    }

    [Fact]
    public async Task MaintenanceNotification_WithoutDateOrMileage_StatesUndefined()
    {
        var channel = new RecordingNotificationChannel();
        var notification = new MaintenanceNotification(channel, "ops@demo.test", "ABC-123", "GeneralCheck", null, null);

        await notification.SendAsync();

        var sent = Assert.Single(channel.SentMessages);
        Assert.Contains("sin definir", sent.Message);
    }

    [Fact]
    public async Task TripAlertNotification_WithDelay_IncludesDelayInMessage()
    {
        var channel = new RecordingNotificationChannel();
        var notification = new TripAlertNotification(channel, "operaciones@fleetcontrol.demo", "Ruta Norte", "Traffic", "Congestión en la vía principal", 25);

        await notification.SendAsync();

        var sent = Assert.Single(channel.SentMessages);
        Assert.Contains("Ruta Norte", sent.Subject);
        Assert.Contains("Congestión en la vía principal", sent.Message);
        Assert.Contains("25 min", sent.Message);
    }

    [Fact]
    public async Task TripAlertNotification_WithoutDelay_OmitsDelayText()
    {
        var channel = new RecordingNotificationChannel();
        var notification = new TripAlertNotification(channel, "operaciones@fleetcontrol.demo", "Ruta Sur", "Weather", "Lluvia fuerte", 0);

        await notification.SendAsync();

        var sent = Assert.Single(channel.SentMessages);
        Assert.DoesNotContain("Retraso estimado", sent.Message);
    }

    [Fact]
    public async Task VehicleNotification_BuildsMessageWithPreviousAndNewStatus()
    {
        var channel = new RecordingNotificationChannel();
        var notification = new VehicleNotification(channel, "+57 300 555 0102", "XYZ-789", "Available", "Maintenance");

        await notification.SendAsync();

        var sent = Assert.Single(channel.SentMessages);
        Assert.Contains("XYZ-789", sent.Subject);
        Assert.Contains("Available -> Maintenance", sent.Message);
    }

    [Fact]
    public void FleetNotification_WithNullChannel_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new VehicleNotification(null!, "ops@demo.test", "ABC-123", "Available", "OutOfService"));
    }

    [Fact]
    public void AllSpecializations_AreFleetNotifications_ButDifferentConcreteTypes()
    {
        // Confirma la jerarquía de la Abstracción sin explosión combinatoria:
        // 3 especializaciones (por tipo), no 3 x N (por tipo y canal).
        INotificationChannel channel = new RecordingNotificationChannel();
        FleetNotification maintenance = new MaintenanceNotification(channel, "a", "ABC-123", "OilChange", null, null);
        FleetNotification tripAlert = new TripAlertNotification(channel, "b", "Ruta 1", "Delay", "desc", 5);
        FleetNotification vehicle = new VehicleNotification(channel, "c", "ABC-123", "Available", "EnRoute");

        Assert.IsAssignableFrom<FleetNotification>(maintenance);
        Assert.IsAssignableFrom<FleetNotification>(tripAlert);
        Assert.IsAssignableFrom<FleetNotification>(vehicle);
        Assert.NotEqual(maintenance.GetType(), tripAlert.GetType());
        Assert.NotEqual(tripAlert.GetType(), vehicle.GetType());
    }
}
