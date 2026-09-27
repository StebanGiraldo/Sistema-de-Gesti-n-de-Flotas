using FleetManagement.Application.Interfaces;
using FleetManagement.Infrastructure.Logging;
using FleetManagement.Infrastructure.Notifications;
using Xunit;

namespace FleetManagement.Application.Tests.Bridge;

/// <summary>
/// PRUEBAS DE LOS IMPLEMENTADORES CONCRETOS (patrón BRIDGE):
/// <see cref="EmailNotificationChannel"/>, <see cref="SmsNotificationChannel"/>
/// y <see cref="PushNotificationChannel"/>. Verifican que cada canal cumple
/// el contrato de <see cref="INotificationChannel"/>, que escribe el
/// prefijo correcto por canal, y que <c>SmsNotificationChannel</c> aplica su
/// propia regla de truncamiento (160 caracteres) sin que la Abstracción ni
/// el resto de canales tengan que saberlo: eso es justamente lo que Bridge
/// permite.
///
/// Para verificar el texto que cada canal "envía" (simulado por consola), se
/// redirige temporalmente <see cref="Console.Out"/> a un buffer en memoria;
/// se restaura siempre en un bloque <c>finally</c>. Estas pruebas comparten
/// el Singleton <c>FleetAuditLogger.Instance</c> (igual que
/// VehicleServiceFactoryMethodTests) pero, siguiendo la misma convención ya
/// usada en ese archivo, no se hacen aserciones sobre su contenido para no
/// acoplarse al orden de ejecución de las pruebas.
/// </summary>
public class NotificationChannelTests
{
    private static async Task<string> CaptureConsoleOutputAsync(Func<Task> action)
    {
        var originalOut = Console.Out;
        var buffer = new StringWriter();
        Console.SetOut(buffer);
        try
        {
            await action();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        return buffer.ToString();
    }

    [Fact]
    public async Task EmailNotificationChannel_WritesEmailPrefixWithRecipientAndSubject()
    {
        var channel = new EmailNotificationChannel(FleetAuditLogger.Instance);

        var output = await CaptureConsoleOutputAsync(() => channel.SendAsync("conductor@demo.test", "Asunto de prueba", "Cuerpo del mensaje"));

        Assert.Contains("[EMAIL]", output);
        Assert.Contains("conductor@demo.test", output);
        Assert.Contains("Asunto de prueba", output);
        Assert.Contains("Cuerpo del mensaje", output);
    }

    [Fact]
    public async Task SmsNotificationChannel_WritesSmsPrefix_ForShortMessage()
    {
        var channel = new SmsNotificationChannel(FleetAuditLogger.Instance);

        var output = await CaptureConsoleOutputAsync(() => channel.SendAsync("+57 300 555 0101", "Aviso", "Mensaje corto"));

        Assert.Contains("[SMS]", output);
        Assert.Contains("Aviso: Mensaje corto", output);
    }

    [Fact]
    public async Task SmsNotificationChannel_TruncatesMessagesLongerThan160Characters()
    {
        var channel = new SmsNotificationChannel(FleetAuditLogger.Instance);
        var longMessage = new string('a', 200);

        var output = await CaptureConsoleOutputAsync(() => channel.SendAsync("+57 300 555 0101", "Aviso", longMessage));

        Assert.Contains("[SMS]", output);
        Assert.Contains("…", output);
        Assert.DoesNotContain(longMessage, output);
    }

    [Fact]
    public async Task PushNotificationChannel_WritesPushPrefixWithRecipientAndSubject()
    {
        var channel = new PushNotificationChannel(FleetAuditLogger.Instance);

        var output = await CaptureConsoleOutputAsync(() => channel.SendAsync("driver-device-42", "Alerta", "Contenido de push"));

        Assert.Contains("[PUSH]", output);
        Assert.Contains("driver-device-42", output);
        Assert.Contains("Alerta", output);
    }

    [Theory]
    [InlineData(typeof(EmailNotificationChannel))]
    [InlineData(typeof(SmsNotificationChannel))]
    [InlineData(typeof(PushNotificationChannel))]
    public void AllConcreteChannels_ImplementINotificationChannel(Type channelType)
    {
        var channel = (INotificationChannel)Activator.CreateInstance(channelType, FleetAuditLogger.Instance)!;

        Assert.IsAssignableFrom<INotificationChannel>(channel);
    }
}
