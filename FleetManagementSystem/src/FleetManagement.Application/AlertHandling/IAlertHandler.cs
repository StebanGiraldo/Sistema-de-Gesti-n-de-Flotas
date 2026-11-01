namespace FleetManagement.Application.AlertHandling;

/// <summary>
/// PATRÓN CHAIN OF RESPONSIBILITY — el manejador: recibe el reporte, hace lo suyo si le corresponde y decide si lo pasa al
/// siguiente o lo da por atendido. Quien reporta solo conoce al primero de la cadena.
/// </summary>
public interface IAlertHandler
{
    /// <summary>Enlaza al sucesor y lo devuelve, para poder encadenar: <c>a.SetNext(b).SetNext(c)</c>.</summary>
    IAlertHandler SetNext(IAlertHandler next);

    Task HandleAsync(AlertReportContext context);
}
