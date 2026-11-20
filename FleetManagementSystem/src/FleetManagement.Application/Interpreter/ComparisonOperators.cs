namespace FleetManagement.Application.Interpreter;

/// <summary>Un operador de comparación: su símbolo y la condición que debe cumplir el signo de (izquierda − derecha).</summary>
public sealed record ComparisonOperator(string Symbol, Func<int, bool> Holds);

/// <summary>
/// Tabla símbolo → operador. Es la única fuente de los operadores: el tokenizador la usa para reconocer símbolos y la
/// comparación para decidir. Agregar un operador es agregar una fila; no hay un <c>switch</c> que mantener.
/// </summary>
public static class ComparisonOperators
{
    private static readonly IReadOnlyDictionary<string, ComparisonOperator> BySymbol = new[]
    {
        new ComparisonOperator("=", sign => sign == 0),
        new ComparisonOperator("!=", sign => sign != 0),
        new ComparisonOperator(">", sign => sign > 0),
        new ComparisonOperator(">=", sign => sign >= 0),
        new ComparisonOperator("<", sign => sign < 0),
        new ComparisonOperator("<=", sign => sign <= 0),
    }.ToDictionary(comparison => comparison.Symbol);

    /// <summary>Los símbolos, los más largos primero (<c>&gt;=</c> antes que <c>&gt;</c>), para reconocerlos sin ambigüedad.</summary>
    public static IReadOnlyList<string> Symbols { get; } = BySymbol.Keys
        .OrderByDescending(symbol => symbol.Length)
        .ThenBy(symbol => symbol, StringComparer.Ordinal)
        .ToList();

    public static ComparisonOperator Get(string symbol) => BySymbol[symbol];
}
