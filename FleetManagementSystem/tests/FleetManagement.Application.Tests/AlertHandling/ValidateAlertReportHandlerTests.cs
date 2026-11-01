using FleetManagement.Application.AlertHandling;
using Xunit;
using static FleetManagement.Application.Tests.AlertHandling.AlertHandlingTestData;

namespace FleetManagement.Application.Tests.AlertHandling;

/// <summary>Primer eslabón: rechaza el reporte que no cumple las reglas antes de que se registre nada.</summary>
public class ValidateAlertReportHandlerTests
{
    private readonly List<string> _journal = new();
    private readonly AlertHandlingOptions _options = new();

    private IAlertHandler ChainWithAFollower() => AlertHandlerChain.Of(new ValidateAlertReportHandler(_options), new SpyHandler("siguiente", _journal));

    [Fact]
    public async Task AValidReport_IsNotedAsValidated_AndPassedOn()
    {
        var context = Context();

        await ChainWithAFollower().HandleAsync(context);

        Assert.Equal(new[] { "Validada" }, context.Steps);
        Assert.Equal(new[] { "siguiente" }, _journal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ABlankDescription_IsRejected(string description)
    {
        var error = await Assert.ThrowsAsync<AlertRejectedException>(() => ChainWithAFollower().HandleAsync(Context(description: description)));

        Assert.Equal("La descripción de la alerta es obligatoria.", error.Message);
    }

    [Fact]
    public async Task AMissingDescription_IsRejected_LikeABlankOne()
        => await Assert.ThrowsAsync<AlertRejectedException>(() => ChainWithAFollower().HandleAsync(Context(description: null!)));

    [Fact]
    public async Task ADescriptionOverTheLimit_IsRejected_AndTheMessageSaysTheLimitAndTheLength()
    {
        var tooLong = new string('x', _options.MaxDescriptionLength + 1);

        var error = await Assert.ThrowsAsync<AlertRejectedException>(() => ChainWithAFollower().HandleAsync(Context(description: tooLong)));

        Assert.Contains($"{_options.MaxDescriptionLength} caracteres", error.Message);
        Assert.Contains($"tiene {_options.MaxDescriptionLength + 1}", error.Message);
    }

    [Fact]
    public async Task ADescriptionOfExactlyTheLimit_IsAccepted()
    {
        var context = Context(description: new string('x', _options.MaxDescriptionLength));

        await ChainWithAFollower().HandleAsync(context);

        Assert.Equal(new[] { "Validada" }, context.Steps);
    }

    [Fact]
    public async Task TheDescriptionLimit_ComesFromTheOptions_NotFromTheHandler()
    {
        _options.MaxDescriptionLength = 10;

        await Assert.ThrowsAsync<AlertRejectedException>(() => ChainWithAFollower().HandleAsync(Context(description: "once chars!")));
        await ChainWithAFollower().HandleAsync(Context(description: "diez chars"));
    }

    [Fact]
    public async Task ADelayOverTheLimit_IsRejected_AndTheMessageSaysTheLimit()
    {
        var error = await Assert.ThrowsAsync<AlertRejectedException>(() => ChainWithAFollower().HandleAsync(Context(delayMinutes: _options.MaxDelayMinutes + 1)));

        Assert.Contains($"{_options.MaxDelayMinutes} minutos", error.Message);
        Assert.Contains($"se indicaron {_options.MaxDelayMinutes + 1}", error.Message);
    }

    [Fact]
    public async Task ADelayOfExactlyTheLimit_IsAccepted()
    {
        var context = Context(delayMinutes: _options.MaxDelayMinutes);

        await ChainWithAFollower().HandleAsync(context);

        Assert.Equal(new[] { "Validada" }, context.Steps);
    }

    [Fact]
    public async Task TheDelayLimit_ComesFromTheOptions_NotFromTheHandler()
    {
        _options.MaxDelayMinutes = 30;

        await Assert.ThrowsAsync<AlertRejectedException>(() => ChainWithAFollower().HandleAsync(Context(delayMinutes: 31)));
        await ChainWithAFollower().HandleAsync(Context(delayMinutes: 30));
    }

    [Fact]
    public async Task ANegativeDelay_IsNotRejectedHere_TheAlertServiceTreatsItAsZero()
    {
        var context = Context(delayMinutes: -5);

        await ChainWithAFollower().HandleAsync(context);

        Assert.Equal(new[] { "Validada" }, context.Steps);
    }

    [Fact]
    public async Task ARejectedReport_CutsTheChain_SoNothingAfterwardsRuns()
    {
        await Assert.ThrowsAsync<AlertRejectedException>(() => ChainWithAFollower().HandleAsync(Context(description: "")));

        Assert.Empty(_journal);
    }

    [Fact]
    public void ARejection_IsAnArgumentException_SoTheApiAnswers400LikeForAnyOtherInvalidData()
    {
        var rejection = new AlertRejectedException("motivo");

        Assert.IsAssignableFrom<ArgumentException>(rejection);
    }
}
