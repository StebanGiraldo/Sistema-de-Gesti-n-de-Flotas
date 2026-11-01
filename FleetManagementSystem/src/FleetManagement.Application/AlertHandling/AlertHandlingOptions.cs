using System.ComponentModel.DataAnnotations;

namespace FleetManagement.Application.AlertHandling;

/// <summary>
/// Parámetros del manejo de alertas, para que ninguna regla quede escrita dentro de un manejador: cada valor tiene nombre,
/// valor por defecto y regla de validez. Se leen de la sección <c>"AlertHandling"</c> de la configuración y se validan al
/// arrancar (igual que <c>Simulation</c> y <c>NavigationCache</c>).
/// </summary>
public sealed class AlertHandlingOptions
{
    public const string SectionName = "AlertHandling";

    /// <summary>Máximo de caracteres de la descripción que escribe el operador.</summary>
    [Range(1, 10000, ErrorMessage = "AlertHandling:MaxDescriptionLength debe estar entre 1 y 10000.")]
    public int MaxDescriptionLength { get; set; } = 500;

    /// <summary>Máximo de minutos de retraso que se acepta en una sola alerta (1440 = un día).</summary>
    [Range(1, 100000, ErrorMessage = "AlertHandling:MaxDelayMinutes debe estar entre 1 y 100000.")]
    public int MaxDelayMinutes { get; set; } = 1440;

    /// <summary>Desde cuántos minutos de retraso una alerta, sea del tipo que sea, se considera grave.</summary>
    [Range(1, 100000, ErrorMessage = "AlertHandling:SeriousDelayMinutes debe estar entre 1 y 100000.")]
    public int SeriousDelayMinutes { get; set; } = 60;

    /// <summary>A quién se escalan las alertas graves (avería, o retraso desde <see cref="SeriousDelayMinutes"/>).</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "AlertHandling:SupervisorRecipient es obligatorio.")]
    public string SupervisorRecipient { get; set; } = "supervisor@fleetcontrol.demo";

    /// <summary>A quién se escalan las alertas críticas (accidente).</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "AlertHandling:AdministratorRecipient es obligatorio.")]
    public string AdministratorRecipient { get; set; } = "administrador@fleetcontrol.demo";
}
