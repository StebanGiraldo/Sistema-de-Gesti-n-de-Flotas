using FleetManagement.Application.Interfaces;

namespace FleetManagement.Application.AlertHandling;

/// <summary>Último eslabón: lo que ningún manejador anterior atendió (las alertas leves) se deja registrado y abierto, sin escalarlo y sin ocultarlo.</summary>
public sealed class UnescalatedAlertHandler : AlertHandler
{
    private const string AuditCategory = "Alerta";

    private readonly IFleetAuditLogger _audit;

    public UnescalatedAlertHandler(IFleetAuditLogger audit)
    {
        _audit = audit;
    }

    protected override bool ConsumesTheReport => true;

    protected override Task ProcessAsync(AlertReportContext context)
    {
        var alert = context.Alert;
        _audit.LogEvent(AuditCategory, $"Alerta {alert.Id} ({alert.Type}, gravedad {context.Severity}) no requiere escalamiento: queda abierta.");
        context.Note("Sin escalamiento");
        return Task.CompletedTask;
    }
}
