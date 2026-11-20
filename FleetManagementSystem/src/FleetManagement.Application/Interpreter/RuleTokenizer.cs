namespace FleetManagement.Application.Interpreter;

/// <summary>
/// Parte el texto de una regla en <see cref="Token"/>s. No conoce el árbol ni las variables: solo reconoce piezas. Prueba, en
/// orden, un lector por clase de pieza (texto, número, palabra, operador, paréntesis); el primero que reconoce el carácter
/// actual gana. Es de un solo uso por regla (guarda la posición de lectura).
/// </summary>
public sealed class RuleTokenizer
{
    private const char Quote = '"';
    private const char Minus = '-';
    private const char DecimalPoint = '.';
    private const char WordSeparator = '_';

    private static readonly IReadOnlyDictionary<string, TokenKind> Keywords = new Dictionary<string, TokenKind>(StringComparer.OrdinalIgnoreCase)
    {
        ["AND"] = TokenKind.And,
        ["OR"] = TokenKind.Or,
        ["NOT"] = TokenKind.Not,
    };

    private static readonly IReadOnlyDictionary<char, TokenKind> Punctuation = new Dictionary<char, TokenKind>
    {
        ['('] = TokenKind.OpenParen,
        [')'] = TokenKind.CloseParen,
    };

    private readonly string _text;
    private readonly IReadOnlyList<Func<Token?>> _readers;
    private int _index;

    public RuleTokenizer(string rule)
    {
        _text = rule;
        _readers = new Func<Token?>[] { ReadText, ReadNumber, ReadWord, ReadOperator, ReadPunctuation };
    }

    /// <summary>Todas las piezas de la regla, terminadas siempre por una de clase <see cref="TokenKind.End"/>.</summary>
    public IReadOnlyList<Token> Tokenize()
    {
        _index = 0;
        var tokens = new List<Token>();
        for (SkipSpaces(); !AtEnd; SkipSpaces())
            tokens.Add(NextToken());

        tokens.Add(new Token(TokenKind.End, string.Empty, _text.Length + 1));
        return tokens;
    }

    private bool AtEnd => _index >= _text.Length;

    private char Current => _text[_index];

    private Token NextToken()
    {
        foreach (var read in _readers)
            if (read() is { } token)
                return token;

        throw new RuleSyntaxException($"Carácter inesperado '{Current}'.", _index + 1);
    }

    private Token? ReadText()
    {
        if (Current != Quote) return null;

        var start = _index;
        var closing = _text.IndexOf(Quote, start + 1);
        if (closing < 0)
            throw new RuleSyntaxException("El texto no tiene comilla de cierre.", start + 1);

        _index = closing + 1;
        return new Token(TokenKind.Text, _text[(start + 1)..closing], start + 1);
    }

    private Token? ReadNumber()
    {
        if (!StartsNumber()) return null;

        var start = _index;
        if (Current == Minus) _index++;
        Consume(char.IsAsciiDigit);
        if (!AtEnd && Current == DecimalPoint) ConsumeFraction();
        return new Token(TokenKind.Number, _text[start.._index], start + 1);
    }

    private bool StartsNumber()
        => char.IsAsciiDigit(Current)
           || (Current == Minus && _index + 1 < _text.Length && char.IsAsciiDigit(_text[_index + 1]));

    private void ConsumeFraction()
    {
        var point = _index++;
        if (AtEnd || !char.IsAsciiDigit(Current))
            throw new RuleSyntaxException("El número tiene un punto decimal sin dígitos después.", point + 1);

        Consume(char.IsAsciiDigit);
    }

    private Token? ReadWord()
    {
        if (!char.IsAsciiLetter(Current)) return null;

        var start = _index;
        Consume(IsWordCharacter);
        var word = _text[start.._index];
        return new Token(Keywords.GetValueOrDefault(word, TokenKind.Identifier), word, start + 1);
    }

    private Token? ReadOperator()
    {
        var symbol = ComparisonOperators.Symbols.FirstOrDefault(candidate => StartsWith(candidate));
        if (symbol is null) return null;

        var token = new Token(TokenKind.Operator, symbol, _index + 1);
        _index += symbol.Length;
        return token;
    }

    private Token? ReadPunctuation()
    {
        if (!Punctuation.TryGetValue(Current, out var kind)) return null;

        var token = new Token(kind, Current.ToString(), _index + 1);
        _index++;
        return token;
    }

    private bool StartsWith(string symbol) => string.CompareOrdinal(_text, _index, symbol, 0, symbol.Length) == 0;

    private static bool IsWordCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character == WordSeparator;

    private void Consume(Func<char, bool> belongs)
    {
        while (!AtEnd && belongs(Current))
            _index++;
    }

    private void SkipSpaces() => Consume(char.IsWhiteSpace);
}
