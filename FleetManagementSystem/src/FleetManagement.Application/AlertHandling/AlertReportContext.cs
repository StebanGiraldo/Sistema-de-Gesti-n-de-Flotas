using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.AlertHandling;

/// <summary>
/// Lo que va de mano en mano por la cadena: el reporte del operador y lo que cada manejador le va agregando (la alerta ya
/// registrada, su gravedad y una línea por cada paso que se dio, útil para entender qué pasó). Cada manejador solo puede
/// agregar su parte, y pedir algo que todavía no existe es un error de armado de la cadena, no un valor vacío.
/// </summary>
public sealed class AlertReportContext
{
    private readonly List<string> _steps = new();
    private TripAlertDto? _alert;
    private AlertSeverity? _severity;

    public AlertReportContext(CreateTripAlertRequest request)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
    }

    public CreateTripAlertRequest Request { get; }

    public TripAlertDto Alert => _alert ?? throw new InvalidOperationException("La alerta todavía no se registró: falta el manejador que la registra antes de este.");

    public AlertSeverity Severity => _severity ?? throw new InvalidOperationException("La alerta todavía no se clasificó: falta el manejador que la clasifica antes de este.");

    public IReadOnlyList<string> Steps => _steps;

    public void Register(TripAlertDto alert) => _alert = alert;

    public void Classify(AlertSeverity severity) => _severity = severity;

    public void Note(string step) => _steps.Add(step);
}
