namespace FleetManagement.Application.AlertHandling;

/// <summary>La alerta no cumple las reglas y la cadena se corta sin registrarla. Es un <see cref="ArgumentException"/>: la API ya la responde con 400.</summary>
public sealed class AlertRejectedException : ArgumentException
{
    public AlertRejectedException(string message) : base(message)
    {
    }
}
