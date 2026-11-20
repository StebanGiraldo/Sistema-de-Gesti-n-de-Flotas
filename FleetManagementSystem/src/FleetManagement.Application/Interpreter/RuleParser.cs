namespace FleetManagement.Application.Interpreter;

/// <summary>Convierte el texto de una regla en un árbol de expresiones. Un error de sintaxis lanza <see cref="RuleSyntaxException"/>.</summary>
public interface IRuleParser
{
    IExpression<bool> Parse(string rule, RuleVocabulary vocabulary);
}

/// <summary>
/// Aplica los límites (longitud y anidamiento), tokeniza y deja que <c>RuleGrammar</c> arme el árbol. No tiene estado: una
/// instancia sirve para todas las peticiones. No evalúa nada ni usa reflexión, <c>eval</c> ni compilación dinámica: lo único
/// que puede producir son las seis clases de expresión de este espacio de nombres.
/// </summary>
public sealed class RuleParser : IRuleParser
{
    private readonly RuleLimits _limits;

    public RuleParser(RuleLimits? limits = null)
    {
        _limits = limits ?? RuleLimits.Default;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_limits.MaxLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_limits.MaxDepth);
    }

    public IExpression<bool> Parse(string rule, RuleVocabulary vocabulary)
    {
        EnsureUsable(rule);
        var tokens = new RuleTokenizer(rule).Tokenize();
        return new RuleGrammar(tokens, vocabulary, _limits.MaxDepth).ParseRule();
    }

    private void EnsureUsable(string? rule)
    {
        if (string.IsNullOrWhiteSpace(rule))
            throw new RuleSyntaxException("La regla está vacía.", 1);

        if (rule.Length > _limits.MaxLength)
            throw new RuleSyntaxException($"La regla supera el máximo de {_limits.MaxLength} caracteres.", _limits.MaxLength + 1);
    }
}
