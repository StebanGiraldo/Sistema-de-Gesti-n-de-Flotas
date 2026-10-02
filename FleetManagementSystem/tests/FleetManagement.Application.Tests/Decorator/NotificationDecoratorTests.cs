using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Notifications;
using FleetManagement.Application.Notifications.Decorators;
using FleetManagement.Application.Tests.Bridge;
using FleetManagement.Infrastructure.Notifications;
using Xunit;

namespace FleetManagement.Application.Tests.Decorator;

/// <summary>
/// PRUEBAS DEL PATRÓN DECORATOR sobre las notificaciones del Bridge. Cubren:
/// notificación normal, un decorator, varios decorators, combinación en
/// cualquier orden, que el Bridge siga funcionando con distintos canales
/// (incluidos los tres canales reales) y que el comportamiento original de la
/// notificación no se pierda.
/// </summary>
public class NotificationDecoratorTests
{
    private static readonly TimeSpan NoDelay = TimeSpan.Zero;

    private static MaintenanceNotification Maintenance(INotificationChannel channel)
        => new(channel, "ops@demo.test", "TES-001", "OilChange", new DateTime(2027, 1, 1), 15000);

    // ---------- 1. Notificación normal ----------

    [Fact]
    public async Task PlainNotification_IsDeliveredThroughTheBridgeChannel()
    {
        var channel = new RecordingNotificationChannel();

        await Maintenance(channel).SendAsync();

        var sent = Assert.Single(channel.SentMessages);
        Assert.Equal("ops@demo.test", sent.Recipient);
        Assert.Contains("TES-001", sent.Subject);
    }

    [Fact]
    public void FleetNotification_IsTheComponentOfTheDecorator()
    {
        var notification = new VehicleNotification(new RecordingNotificationChannel(), "a", "TES-001", "Available", "EnRoute");

        Assert.IsAssignableFrom<INotification>(notification);
        Assert.IsAssignableFrom<FleetNotification>(notification);
    }

    // ---------- 2. Notificación con un decorator ----------

    [Fact]
    public async Task Audit_OnSuccess_LogsOneEventAndStillDelivers()
    {
        var channel = new RecordingNotificationChannel();
        var audit = new RecordingAuditLogger();

        await Maintenance(channel).WithAudit(audit).SendAsync();

        Assert.Single(channel.SentMessages);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal("Notificación", entry.Category);
        Assert.Contains("MaintenanceNotification", entry.Message);
        Assert.Contains("enviada correctamente", entry.Message);
    }

    [Fact]
    public async Task Audit_OnFailure_LogsTheErrorAndRethrows()
    {
        var channel = new FlakyNotificationChannel(int.MaxValue);
        var audit = new RecordingAuditLogger();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Maintenance(channel).WithAudit(audit).SendAsync());

        var entry = Assert.Single(audit.Entries);
        Assert.Contains("falló", entry.Message);
        Assert.Contains("InvalidOperationException", entry.Message);
        Assert.Empty(channel.Delivered.SentMessages);
    }

    [Fact]
    public async Task Retry_RecoversFromTransientChannelFailures()
    {
        var channel = new FlakyNotificationChannel(failuresBeforeSuccess: 2);
        var retry = new RetryNotificationDecorator(Maintenance(channel), maxAttempts: 3, delayBetweenAttempts: NoDelay);

        await retry.SendAsync();

        Assert.Equal(3, channel.Calls);
        Assert.Equal(3, retry.AttemptsMade);
        Assert.Equal(3, retry.MaxAttempts);
        Assert.Single(channel.Delivered.SentMessages);
    }

    [Fact]
    public async Task Retry_WhenAllAttemptsFail_RethrowsAndDeliversNothing()
    {
        var channel = new FlakyNotificationChannel(int.MaxValue);
        var retry = new RetryNotificationDecorator(Maintenance(channel), maxAttempts: 2, delayBetweenAttempts: NoDelay);

        await Assert.ThrowsAsync<InvalidOperationException>(() => retry.SendAsync());

        Assert.Equal(2, channel.Calls);
        Assert.Equal(2, retry.AttemptsMade);
        Assert.Empty(channel.Delivered.SentMessages);
    }

    [Theory]
    [InlineData(NotificationPriority.Low, 1)]
    [InlineData(NotificationPriority.Normal, 2)]
    [InlineData(NotificationPriority.High, 3)]
    [InlineData(NotificationPriority.Critical, 5)]
    public async Task Retry_WithoutExplicitAttempts_LetsThePriorityDecideTheDeliveryEffort(NotificationPriority priority, int expectedAttempts)
    {
        var channel = new FlakyNotificationChannel(int.MaxValue);
        var notification = Maintenance(channel).WithPriority(priority).WithRetry(delayBetweenAttempts: NoDelay);

        await Assert.ThrowsAsync<InvalidOperationException>(() => notification.SendAsync());

        Assert.Equal(expectedAttempts, channel.Calls);
    }

    [Fact]
    public async Task Retry_WithoutAPriorityInTheChain_TreatsTheNotificationAsNormal()
    {
        var channel = new FlakyNotificationChannel(int.MaxValue);
        var notification = Maintenance(channel).WithRetry(delayBetweenAttempts: NoDelay);

        await Assert.ThrowsAsync<InvalidOperationException>(() => notification.SendAsync());

        Assert.Equal(2, channel.Calls);
    }

    [Fact]
    public async Task Priority_ExposesItsLevel_AndDoesNotChangeDelivery()
    {
        var channel = new RecordingNotificationChannel();
        var decorator = new PriorityNotificationDecorator(Maintenance(channel), NotificationPriority.High);

        await decorator.SendAsync();

        Assert.Equal(NotificationPriority.High, decorator.Priority);
        Assert.Single(channel.SentMessages);
    }

    [Fact]
    public void Decorators_RejectInvalidArguments()
    {
        var inner = Maintenance(new RecordingNotificationChannel());

        Assert.Throws<ArgumentNullException>(() => new PriorityNotificationDecorator(null!, NotificationPriority.Low));
        Assert.Throws<ArgumentNullException>(() => new AuditNotificationDecorator(inner, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PriorityNotificationDecorator(inner, (NotificationPriority)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryNotificationDecorator(inner, maxAttempts: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryNotificationDecorator(inner, delayBetweenAttempts: TimeSpan.FromSeconds(-1)));
    }

    // ---------- 3 y 4. Varios decorators, combinables ----------

    [Fact]
    public void SeveralDecorators_FormAChain_ThatEndsInTheOriginalBridgeNotification()
    {
        var original = Maintenance(new RecordingNotificationChannel());
        var decorated = original
            .WithPriority(NotificationPriority.High)
            .WithRetry(delayBetweenAttempts: NoDelay)
            .WithAudit(new RecordingAuditLogger());

        var chain = NotificationChain.Walk(decorated).Select(n => n.GetType()).ToArray();

        Assert.Equal(
            new[]
            {
                typeof(AuditNotificationDecorator),
                typeof(RetryNotificationDecorator),
                typeof(PriorityNotificationDecorator),
                typeof(MaintenanceNotification)
            },
            chain);
        Assert.Same(original, NotificationChain.Unwrap(decorated));
    }

    [Fact]
    public async Task SeveralDecorators_ProduceOneAuditRecord_WithPriorityAndAttempts()
    {
        var channel = new FlakyNotificationChannel(failuresBeforeSuccess: 1);
        var audit = new RecordingAuditLogger();

        await Maintenance(channel)
            .WithPriority(NotificationPriority.High)
            .WithRetry(delayBetweenAttempts: NoDelay)
            .WithAudit(audit)
            .SendAsync();

        Assert.Single(channel.Delivered.SentMessages);
        var entry = Assert.Single(audit.Entries);
        Assert.Contains("enviada correctamente", entry.Message);
        Assert.Contains("prioridad High", entry.Message);
        Assert.Contains("2 de 3 intento(s)", entry.Message);
    }

    [Fact]
    public async Task Decorators_CanBeCombinedInAnyOrder_AndTheOrderChangesWhatIsObserved()
    {
        // Auditoría DENTRO de reintentos: cada intento queda auditado (2 fallos + 1 éxito).
        var channel = new FlakyNotificationChannel(failuresBeforeSuccess: 2);
        var audit = new RecordingAuditLogger();

        await Maintenance(channel).WithAudit(audit).WithRetry(maxAttempts: 3, delayBetweenAttempts: NoDelay).SendAsync();

        Assert.Equal(3, audit.Entries.Count);
        Assert.Equal(2, audit.Entries.Count(e => e.Message.Contains("falló")));
        Assert.Single(channel.Delivered.SentMessages);
    }

    [Fact]
    public void NotificationChain_FindsCapabilitiesOnlyWhereTheyExist()
    {
        var plain = Maintenance(new RecordingNotificationChannel());
        var decorated = plain.WithPriority(NotificationPriority.Critical).WithAudit(new RecordingAuditLogger());

        Assert.Single(NotificationChain.Walk(plain));
        Assert.Null(NotificationChain.Find<IPrioritizedNotification>(plain));
        Assert.Equal(NotificationPriority.Critical, NotificationChain.Find<IPrioritizedNotification>(decorated)!.Priority);
        Assert.Null(NotificationChain.Find<IRetryingNotification>(decorated));
    }

    // ---------- 5. El Bridge continúa funcionando ----------

    [Fact]
    public async Task TheSameDecoratedNotification_CanStillUseDifferentChannels_ThroughTheBridge()
    {
        var emailChannel = new RecordingNotificationChannel();
        var smsChannel = new RecordingNotificationChannel();
        var audit = new RecordingAuditLogger();

        await Maintenance(emailChannel).WithPriority(NotificationPriority.Normal).WithAudit(audit).SendAsync();
        await Maintenance(smsChannel).WithPriority(NotificationPriority.Normal).WithAudit(audit).SendAsync();

        Assert.Single(emailChannel.SentMessages);
        Assert.Single(smsChannel.SentMessages);
        Assert.Equal(2, audit.Entries.Count);
    }

    [Fact]
    public async Task DecoratedNotification_WorksWithTheThreeRealInfrastructureChannels()
    {
        var audit = new RecordingAuditLogger();
        INotificationChannel[] channels =
        {
            new EmailNotificationChannel(audit),
            new SmsNotificationChannel(audit),
            new PushNotificationChannel(audit)
        };

        foreach (var channel in channels)
        {
            await Maintenance(channel)
                .WithPriority(NotificationPriority.Low)
                .WithRetry(delayBetweenAttempts: NoDelay)
                .WithAudit(audit)
                .SendAsync();
        }

        // 3 envíos x (1 evento de transporte del canal + 1 evento de auditoría del decorador).
        Assert.Equal(6, audit.Entries.Count);
        Assert.Equal(3, audit.Entries.Count(e => e.Category == "Notificación"));
        Assert.Contains(audit.Entries, e => e.Category == "Notificación (Email)");
        Assert.Contains(audit.Entries, e => e.Category == "Notificación (SMS)");
        Assert.Contains(audit.Entries, e => e.Category == "Notificación (Push)");
    }

    [Fact]
    public async Task AllThreeBridgeNotificationTypes_CanBeDecorated()
    {
        var channel = new RecordingNotificationChannel();
        var audit = new RecordingAuditLogger();
        INotification[] notifications =
        {
            new MaintenanceNotification(channel, "a", "TES-001", "OilChange", null, null),
            new TripAlertNotification(channel, "b", "Ruta 1", "Accident", "Choque", 30),
            new VehicleNotification(channel, "c", "TES-001", "Available", "OutOfService")
        };

        foreach (var notification in notifications)
            await notification.WithAudit(audit).SendAsync();

        Assert.Equal(3, channel.SentMessages.Count);
        Assert.Contains("MaintenanceNotification", audit.Entries[0].Message);
        Assert.Contains("TripAlertNotification", audit.Entries[1].Message);
        Assert.Contains("VehicleNotification", audit.Entries[2].Message);
    }

    // ---------- 6. El comportamiento original no se pierde ----------

    [Fact]
    public async Task Decorators_DoNotAlterTheContentBuiltByTheOriginalNotification()
    {
        var plain = new RecordingNotificationChannel();
        var decorated = new RecordingNotificationChannel();

        await Maintenance(plain).SendAsync();
        await Maintenance(decorated)
            .WithPriority(NotificationPriority.Critical)
            .WithRetry(delayBetweenAttempts: NoDelay)
            .WithAudit(new RecordingAuditLogger())
            .SendAsync();

        Assert.Equal(plain.SentMessages[0], decorated.SentMessages[0]);
    }
}
