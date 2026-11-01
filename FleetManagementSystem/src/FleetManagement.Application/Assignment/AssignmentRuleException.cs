namespace FleetManagement.Application.Assignment;

/// <summary>
/// Se lanza cuando una asignación de vehículo, conductor o carga rompe una regla del negocio (por ejemplo, la carga no
/// cabe en el vehículo). Hereda de <see cref="InvalidOperationException"/> a propósito: los controladores de rutas ya
/// responden 400 a esa excepción, así que el motivo llega al cliente sin tocar nada más.
/// </summary>
public sealed class AssignmentRuleException : InvalidOperationException
{
    public AssignmentRuleException(IReadOnlyList<string> violations)
        : base(string.Join(" ", violations))
    {
        Violations = violations;
    }

    /// <summary>Cada regla incumplida, en una frase. Siempre hay al menos una.</summary>
    public IReadOnlyList<string> Violations { get; }
}
