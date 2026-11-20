using System.Globalization;

namespace FleetManagement.Application.Interpreter;

/// <summary>
/// Descenso recursivo de la gramática, un método corto por producción:
/// <code>
/// rule       := or
/// or         := and ("OR" and)*
/// and        := not ("AND" not)*
/// not        := "NOT" not | comparison | "(" rule ")"
/// comparison := value op value
/// op         := = | != | &gt; | &gt;= | &lt; | &lt;=
/// value      := número | "texto" | variable
/// </code>
/// De ahí sale la precedencia: NOT se une antes que AND y AND antes que OR. Es quien construye el árbol; las expresiones solo
/// lo interpretan. Guarda la posición de lectura y la profundidad, así que se crea una vez por regla.
/// </summary>
internal sealed class RuleGrammar
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly RuleVocabulary _vocabulary;
    private readonly int _maxDepth;
    private readonly IReadOnlyDictionary<TokenKind, Func<Token, IExpression<RuleValue>>> _values;
    private int _index;
    private int _depth;

    public RuleGrammar(IReadOnlyList<Token> tokens, RuleVocabulary vocabulary, int maxDepth)
    {
        _tokens = tokens;
        _vocabulary = vocabulary;
        _maxDepth = maxDepth;
        _values = new Dictionary<TokenKind, Func<Token, IExpression<RuleValue>>>
        {
            [TokenKind.Number] = ToNumber,
            [TokenKind.Text] = ToText,
            [TokenKind.Identifier] = ToVariable,
        };
    }

    // rule := or
    public IExpression<bool> ParseRule()
    {
        var rule = ParseOr();
        if (Current.Kind != TokenKind.End)
            throw new RuleSyntaxException($"Sobra {Describe(Current)} después de la regla.", Current.Position);

        return rule;
    }

    // or := and ("OR" and)*
    private IExpression<bool> ParseOr()
        => ParseChain(TokenKind.Or, ParseAnd, (left, right) => new OrExpression(left, right));

    // and := not ("AND" not)*
    private IExpression<bool> ParseAnd()
        => ParseChain(TokenKind.And, ParseNot, (left, right) => new AndExpression(left, right));

    // not := "NOT" not | comparison | "(" rule ")"
    private IExpression<bool> ParseNot()
    {
        if (Match(TokenKind.Not)) return Nested(() => new NotExpression(ParseNot()));
        if (Match(TokenKind.OpenParen)) return Nested(ParseGroup);
        return ParseComparison();
    }

    // "(" rule ")"  (el paréntesis de apertura ya se consumió)
    private IExpression<bool> ParseGroup()
    {
        var inner = ParseOr();
        if (!Match(TokenKind.CloseParen))
            throw Expected("')' para cerrar el paréntesis");

        return inner;
    }

    // comparison := value op value
    private IExpression<bool> ParseComparison()
    {
        var left = ParseValue();
        var comparison = ParseOperator();
        var right = ParseValue();
        return new ComparisonExpression(left, comparison, right);
    }

    // op := = | != | > | >= | < | <=
    private ComparisonOperator ParseOperator()
    {
        if (Current.Kind != TokenKind.Operator)
            throw Expected($"un operador de comparación ({string.Join(", ", ComparisonOperators.Symbols)})");

        return ComparisonOperators.Get(Advance().Text);
    }

    // value := número | "texto" | variable
    private IExpression<RuleValue> ParseValue()
    {
        if (!_values.TryGetValue(Current.Kind, out var build))
            throw Expected("un valor (número, texto entre comillas o variable)");

        return build(Advance());
    }

    private IExpression<bool> ParseChain(
        TokenKind separator,
        Func<IExpression<bool>> operand,
        Func<IExpression<bool>, IExpression<bool>, IExpression<bool>> combine)
    {
        var result = operand();
        while (Match(separator))
            result = combine(result, operand());

        return result;
    }

    private IExpression<bool> Nested(Func<IExpression<bool>> parse)
    {
        if (++_depth > _maxDepth)
            throw new RuleSyntaxException($"La regla supera el máximo de {_maxDepth} niveles de anidamiento.", Previous.Position);

        var result = parse();
        _depth--;
        return result;
    }

    private static IExpression<RuleValue> ToNumber(Token token)
        => new LiteralExpression(RuleValue.FromNumber(double.Parse(token.Text, NumberStyles.Float, CultureInfo.InvariantCulture)));

    private static IExpression<RuleValue> ToText(Token token) => new LiteralExpression(RuleValue.FromText(token.Text));

    private IExpression<RuleValue> ToVariable(Token token)
    {
        if (!_vocabulary.Contains(token.Text))
            throw new RuleSyntaxException(
                $"La variable '{token.Text}' no existe. Variables permitidas: {_vocabulary.Describe()}.", token.Position);

        return new VariableExpression(token.Text);
    }

    private Token Current => _tokens[_index];

    private Token Previous => _tokens[_index - 1];

    private Token Advance()
    {
        var token = Current;
        if (token.Kind != TokenKind.End) _index++;
        return token;
    }

    private bool Match(TokenKind kind)
    {
        if (Current.Kind != kind) return false;

        Advance();
        return true;
    }

    private RuleSyntaxException Expected(string what)
        => new($"Se esperaba {what} pero se encontró {Describe(Current)}.", Current.Position);

    private static string Describe(Token token) => token.Kind == TokenKind.End ? "el final de la regla" : $"'{token.Text}'";
}
