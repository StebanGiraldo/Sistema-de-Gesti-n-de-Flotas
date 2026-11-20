using FleetManagement.Application.Interpreter;
using Xunit;

namespace FleetManagement.Application.Tests.Interpreter;

/// <summary>Cada expresión por separado, sin parser: se arma el árbol a mano y se interpreta contra un contexto de prueba.</summary>
public class ExpressionTests
{
    private static readonly IRuleContext Empty = new FakeRuleContext();

    private static bool Compare(RuleValue left, string symbol, RuleValue right)
        => new ComparisonExpression(new LiteralExpression(left), ComparisonOperators.Get(symbol), new LiteralExpression(right)).Interpret(Empty);

    // ---------- Terminales ----------

    [Fact]
    public void ALiteral_GivesItsValue_WithoutAskingTheContext()
    {
        var context = new FakeRuleContext();

        var value = new LiteralExpression(RuleValue.FromNumber(42)).Interpret(context);

        Assert.Equal(RuleValue.FromNumber(42), value);
        Assert.Empty(context.Asked);
    }

    [Fact]
    public void AVariable_AsksTheContextForItsName()
    {
        var context = new FakeRuleContext().With("mileageKm", 1234);

        var value = new VariableExpression("mileageKm").Interpret(context);

        Assert.Equal(RuleValue.FromNumber(1234), value);
        Assert.Equal(new[] { "mileageKm" }, context.Asked);
    }

    [Fact]
    public void AVariableTheContextHasNoDataFor_IsMissing_NotZero()
    {
        var value = new VariableExpression("kmSinceLastService").Interpret(Empty);

        Assert.Same(RuleValue.Missing, value);
        Assert.NotEqual(RuleValue.FromNumber(0), value);
    }

    // ---------- Operadores lógicos ----------

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void And_IsTrueOnlyWhenBothSidesAre(bool left, bool right, bool expected)
        => Assert.Equal(expected, new AndExpression(new CountingExpression(left), new CountingExpression(right)).Interpret(Empty));

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void Or_IsTrueWhenEitherSideIs(bool left, bool right, bool expected)
        => Assert.Equal(expected, new OrExpression(new CountingExpression(left), new CountingExpression(right)).Interpret(Empty));

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Not_NegatesItsOperand(bool operand, bool expected)
        => Assert.Equal(expected, new NotExpression(new CountingExpression(operand)).Interpret(Empty));

    [Fact]
    public void And_DoesNotInterpretTheRightSide_WhenTheLeftIsAlreadyFalse()
    {
        var right = new CountingExpression(true);

        new AndExpression(new CountingExpression(false), right).Interpret(Empty);

        Assert.Equal(0, right.Calls);
    }

    [Fact]
    public void Or_DoesNotInterpretTheRightSide_WhenTheLeftIsAlreadyTrue()
    {
        var right = new CountingExpression(false);

        new OrExpression(new CountingExpression(true), right).Interpret(Empty);

        Assert.Equal(0, right.Calls);
    }

    // ---------- Comparaciones ----------

    [Theory]
    [InlineData(5, "=", 5, true)]
    [InlineData(5, "=", 6, false)]
    [InlineData(5, "!=", 6, true)]
    [InlineData(5, "!=", 5, false)]
    [InlineData(6, ">", 5, true)]
    [InlineData(5, ">", 5, false)]
    [InlineData(5, ">=", 5, true)]
    [InlineData(4, ">=", 5, false)]
    [InlineData(4, "<", 5, true)]
    [InlineData(5, "<", 5, false)]
    [InlineData(5, "<=", 5, true)]
    [InlineData(6, "<=", 5, false)]
    public void NumbersAreComparedWithEachOperator(double left, string symbol, double right, bool expected)
        => Assert.Equal(expected, Compare(RuleValue.FromNumber(left), symbol, RuleValue.FromNumber(right)));

    [Theory]
    [InlineData("Available", "=", "Available", true)]
    [InlineData("available", "=", "AVAILABLE", true)]
    [InlineData("Available", "!=", "EnRoute", true)]
    [InlineData("Available", "=", "EnRoute", false)]
    [InlineData("A", "<", "B", true)]
    [InlineData("B", ">=", "A", true)]
    public void TextsAreComparedWithoutCaseSensitivity(string left, string symbol, string right, bool expected)
        => Assert.Equal(expected, Compare(RuleValue.FromText(left), symbol, RuleValue.FromText(right)));

    [Theory]
    [InlineData("=")]
    [InlineData("!=")]
    [InlineData(">")]
    [InlineData(">=")]
    [InlineData("<")]
    [InlineData("<=")]
    public void ANumberAndATextCannotBeCompared_SoTheComparisonIsFalseWithEveryOperator(string symbol)
    {
        Assert.False(Compare(RuleValue.FromNumber(5), symbol, RuleValue.FromText("5")));
        Assert.False(Compare(RuleValue.FromText("5"), symbol, RuleValue.FromNumber(5)));
    }

    [Theory]
    [InlineData("=")]
    [InlineData("!=")]
    [InlineData(">")]
    [InlineData(">=")]
    [InlineData("<")]
    [InlineData("<=")]
    public void AMissingValueIsNeverComparable_NotEvenWithNotEqual(string symbol)
    {
        Assert.False(Compare(RuleValue.Missing, symbol, RuleValue.FromNumber(0)));
        Assert.False(Compare(RuleValue.FromNumber(0), symbol, RuleValue.Missing));
        Assert.False(Compare(RuleValue.Missing, symbol, RuleValue.FromText("")));
        Assert.False(Compare(RuleValue.Missing, symbol, RuleValue.Missing));
    }

    [Fact]
    public void FromOptional_TurnsNullIntoMissing_AndANumberIntoANumber()
    {
        Assert.Same(RuleValue.Missing, RuleValue.FromOptional(null));
        Assert.Equal(RuleValue.FromNumber(7), RuleValue.FromOptional(7));
    }

    // ---------- Tabla de operadores ----------

    [Fact]
    public void TheOperatorTable_HasTheSixSymbols_LongestFirst()
    {
        Assert.Equal(new[] { "!=", "<=", ">=", "<", "=", ">" }, ComparisonOperators.Symbols);   // de dos caracteres primero, y luego los de uno
        Assert.All(ComparisonOperators.Symbols, symbol => Assert.Equal(symbol, ComparisonOperators.Get(symbol).Symbol));
    }

    // ---------- Inmutabilidad ----------

    [Fact]
    public void Expressions_AreValuesThatCompareByStructure_SoTheyAreSafeToShare()
    {
        var first = new AndExpression(
            new NotExpression(new BoolLeaf(true)),
            new ComparisonExpression(new VariableExpression("a"), ComparisonOperators.Get("="), new LiteralExpression(RuleValue.FromNumber(1))));
        var second = new AndExpression(
            new NotExpression(new BoolLeaf(true)),
            new ComparisonExpression(new VariableExpression("a"), ComparisonOperators.Get("="), new LiteralExpression(RuleValue.FromNumber(1))));

        Assert.Equal(first, second);
        Assert.NotEqual(first, new AndExpression(new NotExpression(new BoolLeaf(false)), first.Right));
    }

    // Hoja booleana de prueba que también se compara por valor (el producto no tiene otra expresión booleana terminal).
    private sealed record BoolLeaf(bool Value) : IExpression<bool>
    {
        public bool Interpret(IRuleContext context) => Value;
    }
}
