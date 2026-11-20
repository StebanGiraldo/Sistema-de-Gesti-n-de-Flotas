using FleetManagement.Application.Interpreter;
using Xunit;

namespace FleetManagement.Application.Tests.Interpreter;

/// <summary>El parser construye el árbol de la gramática; la precedencia sale de la forma del árbol, no de las expresiones.</summary>
public class RuleParserTests
{
    private static IExpression<RuleValue> Variable(string name) => new VariableExpression(name);

    private static IExpression<RuleValue> Number(double number) => new LiteralExpression(RuleValue.FromNumber(number));

    private static IExpression<RuleValue> Text(string text) => new LiteralExpression(RuleValue.FromText(text));

    private static IExpression<bool> Compare(IExpression<RuleValue> left, string symbol, IExpression<RuleValue> right)
        => new ComparisonExpression(left, ComparisonOperators.Get(symbol), right);

    private static FakeRuleContext Context(double a, double b, double c) => new FakeRuleContext().With("a", a).With("b", b).With("c", c);

    // ---------- Reglas válidas ----------

    [Theory]
    [InlineData("mileageKm >= 8000")]
    [InlineData("status = \"Available\" AND type = \"Truck\"")]
    [InlineData("kmSinceLastService >= 8000 OR mileageKm > 100000")]
    [InlineData("NOT status = \"OutOfService\"")]
    [InlineData("(a = 1 OR b = 2) AND NOT c = 3")]
    [InlineData("((a = 1))")]
    [InlineData("5 > 3")]
    [InlineData("\"x\" = status")]
    [InlineData("a=1 and b!=2 or c<=3")]
    [InlineData("  a   =   1  ")]
    [InlineData("mileageKm > -5.5")]
    [InlineData("MILEAGEKM > 1")]
    public void AValidRule_BecomesATree(string rule)
        => Assert.NotNull(RuleTestData.Parse(rule));

    [Fact]
    public void AComparison_BecomesAComparisonOverAVariableAndALiteral()
    {
        var tree = RuleTestData.Parse("mileageKm >= 8000");

        Assert.Equal(Compare(Variable("mileageKm"), ">=", Number(8000)), tree);
    }

    [Fact]
    public void TheTextOfALiteral_KeepsItsCase()
    {
        var tree = RuleTestData.Parse("status = \"Available\"");

        Assert.Equal(Compare(Variable("status"), "=", Text("Available")), tree);
    }

    // ---------- Precedencia NOT > AND > OR ----------

    [Fact]
    public void And_BindsTighterThanOr()
    {
        var tree = RuleTestData.Parse("a = 1 OR b = 1 AND c = 1");

        var expected = new OrExpression(
            Compare(Variable("a"), "=", Number(1)),
            new AndExpression(Compare(Variable("b"), "=", Number(1)), Compare(Variable("c"), "=", Number(1))));
        Assert.Equal(expected, tree);
        Assert.True(tree.Interpret(Context(a: 1, b: 0, c: 0)));    // si se evaluara de izquierda a derecha daría falso
    }

    [Fact]
    public void Not_BindsTighterThanAnd()
    {
        var tree = RuleTestData.Parse("NOT a = 1 AND b = 1");

        var expected = new AndExpression(
            new NotExpression(Compare(Variable("a"), "=", Number(1))),
            Compare(Variable("b"), "=", Number(1)));
        Assert.Equal(expected, tree);
        Assert.False(tree.Interpret(Context(a: 1, b: 0, c: 0)));   // NOT sobre toda la conjunción daría verdadero
    }

    [Fact]
    public void Parentheses_OverrideThePrecedence()
    {
        var withoutParentheses = RuleTestData.Parse("a = 1 OR b = 1 AND c = 1");
        var withParentheses = RuleTestData.Parse("(a = 1 OR b = 1) AND c = 1");

        var context = Context(a: 1, b: 0, c: 0);
        Assert.True(withoutParentheses.Interpret(context));
        Assert.False(withParentheses.Interpret(context));
    }

    [Fact]
    public void AndAndOr_GroupFromTheLeft()
    {
        var and = RuleTestData.Parse("a = 1 AND b = 1 AND c = 1");
        var or = RuleTestData.Parse("a = 1 OR b = 1 OR c = 1");

        Assert.Equal(
            new AndExpression(
                new AndExpression(Compare(Variable("a"), "=", Number(1)), Compare(Variable("b"), "=", Number(1))),
                Compare(Variable("c"), "=", Number(1))),
            and);
        Assert.Equal(
            new OrExpression(
                new OrExpression(Compare(Variable("a"), "=", Number(1)), Compare(Variable("b"), "=", Number(1))),
                Compare(Variable("c"), "=", Number(1))),
            or);
    }

    [Fact]
    public void NotMayBeChained()
    {
        var tree = RuleTestData.Parse("NOT NOT a = 1");

        Assert.Equal(new NotExpression(new NotExpression(Compare(Variable("a"), "=", Number(1)))), tree);
    }

    [Fact]
    public void KeywordsAndVariables_DoNotDistinguishCase()
    {
        var tree = RuleTestData.Parse("A = 1 and B = 2");

        Assert.True(tree.Interpret(Context(a: 1, b: 2, c: 0)));
        Assert.False(tree.Interpret(Context(a: 1, b: 3, c: 0)));
    }

    [Theory]
    [InlineData("a = 1 OR b = 1 AND c = 1", 1, 0, 0, true)]
    [InlineData("a = 1 OR b = 1 AND c = 1", 0, 1, 0, false)]
    [InlineData("a = 1 OR b = 1 AND c = 1", 0, 1, 1, true)]
    [InlineData("(a = 1 OR b = 1) AND c = 1", 1, 0, 1, true)]
    [InlineData("NOT (a = 1 OR b = 1)", 0, 0, 0, true)]
    [InlineData("NOT (a = 1 OR b = 1)", 0, 1, 0, false)]
    [InlineData("NOT a = 1 OR b = 1", 1, 0, 0, false)]
    [InlineData("NOT a = 1 OR b = 1", 0, 0, 0, true)]
    [InlineData("a > b AND b > c", 3, 2, 1, true)]
    [InlineData("a > b AND b > c", 3, 3, 1, false)]
    public void TheTruthTable_OfComposedRules(string rule, double a, double b, double c, bool expected)
        => Assert.Equal(expected, RuleTestData.Evaluate(rule, Context(a, b, c)));

    // ---------- Reglas inválidas ----------

    [Theory]
    [InlineData("", 1, "La regla está vacía")]
    [InlineData("   ", 1, "La regla está vacía")]
    [InlineData("mileageKm >", 12, "Se esperaba un valor (número, texto entre comillas o variable) pero se encontró el final de la regla")]
    [InlineData("mileageKm 5", 11, "Se esperaba un operador de comparación")]
    [InlineData("(mileageKm > 5", 15, "Se esperaba ')' para cerrar el paréntesis pero se encontró el final de la regla")]
    [InlineData("mileageKm > 5)", 14, "Sobra ')' después de la regla")]
    [InlineData("mileageKm > 5 AND", 18, "Se esperaba un valor")]
    [InlineData("AND mileageKm > 5", 1, "pero se encontró 'AND'")]
    [InlineData("mileageKm > > 5", 13, "pero se encontró '>'")]
    [InlineData("mileageKm = 5 mileageKm = 6", 15, "Sobra 'mileageKm'")]
    [InlineData("NOT", 4, "Se esperaba un valor")]
    [InlineData("()", 2, "pero se encontró ')'")]
    [InlineData("fuel > 5", 1, "La variable 'fuel' no existe")]
    [InlineData("mileageKm = 5 AND fuel < 3", 19, "La variable 'fuel' no existe")]
    [InlineData("mileageKm = = 5", 13, "Se esperaba un valor")]
    public void AnInvalidRule_FailsWithItsPositionAndAMessageInSpanish(string rule, int position, string fragment)
    {
        var error = Assert.Throws<RuleSyntaxException>(() => RuleTestData.Parse(rule));

        Assert.Equal(position, error.Position);
        Assert.Contains(fragment, error.Message);
        Assert.EndsWith($"(posición {position})", error.Message);
    }

    [Fact]
    public void ANullRule_IsRejectedLikeAnEmptyOne()
    {
        var error = Assert.Throws<RuleSyntaxException>(() => new RuleParser().Parse(null!, RuleTestData.Vocabulary));

        Assert.Equal(1, error.Position);
    }

    [Fact]
    public void AnUnknownVariable_ListsTheAllowedOnes_SoTheUserCanFixTheRule()
    {
        var error = Assert.Throws<RuleSyntaxException>(() => RuleTestData.Parse("fuel > 5"));

        Assert.Contains("Variables permitidas: a, b, c, kmSinceLastService, mileageKm, status, type", error.Message);
    }

    [Fact]
    public void TheVocabularyIsAWhitelist_AVariableOfAnotherContextIsRejected()
    {
        var onlyStatus = new RuleVocabulary(new[] { "status" });

        Assert.NotNull(new RuleParser().Parse("status = \"Available\"", onlyStatus));
        Assert.Throws<RuleSyntaxException>(() => new RuleParser().Parse("mileageKm > 5", onlyStatus));
    }

    // ---------- Límites ----------

    [Fact]
    public void ARuleLongerThanTheLimit_IsRejected_ButOneOfExactlyTheLimitIsAccepted()
    {
        var parser = new RuleParser(new RuleLimits(MaxLength: 10, MaxDepth: 5));

        Assert.NotNull(parser.Parse("a = 1".PadRight(10), RuleTestData.Vocabulary));
        var error = Assert.Throws<RuleSyntaxException>(() => parser.Parse("a = 1".PadRight(11), RuleTestData.Vocabulary));
        Assert.Equal(11, error.Position);
        Assert.Contains("máximo de 10 caracteres", error.Message);
    }

    [Fact]
    public void TheDefaultLimit_IsFiveHundredCharacters_AndALongChainStillInterprets()
    {
        var atTheLimit = string.Concat(Enumerable.Repeat("1 = 1 OR ", 55)) + "1 = 1";   // 56 comparaciones, 500 caracteres
        var overTheLimit = atTheLimit + " OR 1 = 1";

        Assert.Equal(RuleLimits.DefaultMaxLength, atTheLimit.Length);
        Assert.True(RuleTestData.Evaluate(atTheLimit, new FakeRuleContext()));
        Assert.Throws<RuleSyntaxException>(() => RuleTestData.Parse(overTheLimit));
    }

    [Theory]
    [InlineData("((( a = 1 )))", true)]
    [InlineData("(((( a = 1 ))))", false)]
    [InlineData("NOT NOT NOT a = 1", true)]
    [InlineData("NOT NOT NOT NOT a = 1", false)]
    [InlineData("NOT (NOT a = 1)", true)]
    [InlineData("NOT (NOT (NOT a = 1))", false)]
    public void TheNestingLimit_CountsParenthesesAndNotsTogether(string rule, bool accepted)
    {
        var parser = new RuleParser(new RuleLimits(MaxLength: 100, MaxDepth: 3));

        if (accepted)
            Assert.NotNull(parser.Parse(rule, RuleTestData.Vocabulary));
        else
            Assert.Contains("máximo de 3 niveles de anidamiento", Assert.Throws<RuleSyntaxException>(() => parser.Parse(rule, RuleTestData.Vocabulary)).Message);
    }

    [Fact]
    public void TheNestingError_PointsAtTheTokenThatOpenedTheLevelThatWasTooMuch()
    {
        var parser = new RuleParser(new RuleLimits(MaxLength: 100, MaxDepth: 3));

        var error = Assert.Throws<RuleSyntaxException>(() => parser.Parse("(((( a = 1 ))))", RuleTestData.Vocabulary));

        Assert.Equal(4, error.Position);
    }

    [Fact]
    public void ADeepRuleWithinTheDefaultLimit_DoesNotOverflowTheStack()
    {
        var rule = new string('(', RuleLimits.DefaultMaxDepth) + "a = 1" + new string(')', RuleLimits.DefaultMaxDepth);

        Assert.True(RuleTestData.Evaluate(rule, Context(a: 1, b: 0, c: 0)));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    [InlineData(-1, 5)]
    public void InvalidLimits_AreRejectedWhenTheParserIsCreated(int maxLength, int maxDepth)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new RuleParser(new RuleLimits(maxLength, maxDepth)));

    // ---------- Sin estado ----------

    [Fact]
    public void OneParser_ServesManyRulesAtTheSameTime_WithoutMixingThem()
    {
        var parser = new RuleParser();
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, 200, index =>
        {
            var rule = $"a = {index} OR b > {index}";
            var tree = parser.Parse(rule, RuleTestData.Vocabulary);
            var matches = tree.Interpret(new FakeRuleContext().With("a", index).With("b", 0));
            if (!matches) failures.Add(rule);
        });

        Assert.Empty(failures);
    }
}
