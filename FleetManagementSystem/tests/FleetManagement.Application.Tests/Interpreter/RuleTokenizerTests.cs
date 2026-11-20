using FleetManagement.Application.Interpreter;
using Xunit;

namespace FleetManagement.Application.Tests.Interpreter;

/// <summary>El tokenizador solo reconoce piezas: no conoce el árbol ni las variables permitidas.</summary>
public class RuleTokenizerTests
{
    private static IReadOnlyList<Token> Tokens(string rule) => new RuleTokenizer(rule).Tokenize();

    [Fact]
    public void ASimpleComparison_IsThreePiecesAndTheEnd_WithTheirPositions()
    {
        var tokens = Tokens("mileageKm >= 8000");

        Assert.Equal(new[] { TokenKind.Identifier, TokenKind.Operator, TokenKind.Number, TokenKind.End }, tokens.Select(token => token.Kind));
        Assert.Equal(new[] { "mileageKm", ">=", "8000", string.Empty }, tokens.Select(token => token.Text));
        Assert.Equal(new[] { 1, 11, 14, 18 }, tokens.Select(token => token.Position));
    }

    [Fact]
    public void TheKeywords_AreRecognizedWithoutCaseSensitivity()
    {
        var kinds = Tokens("a = 1 and b = 2 Or NOT c = 3").Select(token => token.Kind).Where(kind => kind is TokenKind.And or TokenKind.Or or TokenKind.Not);

        Assert.Equal(new[] { TokenKind.And, TokenKind.Or, TokenKind.Not }, kinds);
    }

    [Theory]
    [InlineData("a=1", "=")]
    [InlineData("a!=1", "!=")]
    [InlineData("a>1", ">")]
    [InlineData("a>=1", ">=")]
    [InlineData("a<1", "<")]
    [InlineData("a<=1", "<=")]
    public void EveryOperator_IsRecognized_EvenWithoutSpaces_PreferringTheLongestSymbol(string rule, string symbol)
    {
        var tokens = Tokens(rule);

        Assert.Equal(new[] { TokenKind.Identifier, TokenKind.Operator, TokenKind.Number, TokenKind.End }, tokens.Select(token => token.Kind));
        Assert.Equal(symbol, tokens[1].Text);
    }

    [Fact]
    public void ATextIsDelimitedByQuotes_ItsTextHasNoQuotes_AndItsPositionIsTheOpeningQuote()
    {
        var text = Tokens("status = \"Available\"")[2];

        Assert.Equal(TokenKind.Text, text.Kind);
        Assert.Equal("Available", text.Text);
        Assert.Equal(10, text.Position);
    }

    [Fact]
    public void ATextMayContainSpacesKeywordsAndSymbols_ThatAreNotInterpreted()
    {
        var text = Tokens("status = \"a AND b >= (c)\"")[2];

        Assert.Equal(TokenKind.Text, text.Kind);
        Assert.Equal("a AND b >= (c)", text.Text);
    }

    [Theory]
    [InlineData("a = 12", "12")]
    [InlineData("a = 12.5", "12.5")]
    [InlineData("a = -3", "-3")]
    [InlineData("a = -0.25", "-0.25")]
    public void Numbers_MayHaveASignAndDecimals(string rule, string number)
    {
        var token = Tokens(rule)[2];

        Assert.Equal(TokenKind.Number, token.Kind);
        Assert.Equal(number, token.Text);
    }

    [Fact]
    public void Parentheses_AreTheirOwnPieces()
    {
        var kinds = Tokens("(a=1)").Select(token => token.Kind);

        Assert.Equal(
            new[] { TokenKind.OpenParen, TokenKind.Identifier, TokenKind.Operator, TokenKind.Number, TokenKind.CloseParen, TokenKind.End },
            kinds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankRule_HasOnlyTheEnd(string rule)
    {
        var token = Assert.Single(Tokens(rule));

        Assert.Equal(TokenKind.End, token.Kind);
        Assert.Equal(rule.Length + 1, token.Position);
    }

    [Fact]
    public void TokenizingTwice_GivesTheSamePieces()
    {
        var tokenizer = new RuleTokenizer("a >= 1 OR b < 2");

        Assert.Equal(tokenizer.Tokenize(), tokenizer.Tokenize());
    }

    [Theory]
    [InlineData("a = \"abc", 5, "comilla de cierre")]
    [InlineData("a = #", 5, "Carácter inesperado '#'")]
    [InlineData("a ! b", 3, "Carácter inesperado '!'")]
    [InlineData("a = 1.", 6, "punto decimal")]
    [InlineData("a = 1; DROP", 6, "Carácter inesperado ';'")]
    [InlineData("System.Environment = 1", 7, "Carácter inesperado '.'")]
    [InlineData("_hidden = 1", 1, "Carácter inesperado '_'")]
    public void AnUnrecognizableCharacter_IsReportedWithItsPosition_InSpanish(string rule, int position, string fragment)
    {
        var error = Assert.Throws<RuleSyntaxException>(() => Tokens(rule));

        Assert.Equal(position, error.Position);
        Assert.Contains(fragment, error.Message);
        Assert.Contains($"posición {position}", error.Message);
    }
}
