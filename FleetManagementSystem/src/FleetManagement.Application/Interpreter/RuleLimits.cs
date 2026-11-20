namespace FleetManagement.Application.Interpreter;

/// <summary>
/// Límites contra el abuso: una regla escrita por un usuario no puede ser arbitrariamente larga ni arbitrariamente anidada.
/// La profundidad cuenta los paréntesis y los <c>NOT</c> encadenados (lo que hace crecer la recursión del parser).
/// </summary>
public sealed record RuleLimits(int MaxLength = RuleLimits.DefaultMaxLength, int MaxDepth = RuleLimits.DefaultMaxDepth)
{
    public const int DefaultMaxLength = 500;
    public const int DefaultMaxDepth = 10;

    public static RuleLimits Default { get; } = new();
}
