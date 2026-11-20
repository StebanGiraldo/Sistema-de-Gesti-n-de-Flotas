namespace FleetManagement.Application.Interpreter;

/// <summary>
/// Compara dos valores con un operador de la tabla <see cref="ComparisonOperators"/>. Si los valores no se pueden comparar
/// (una variable sin dato, o un número frente a un texto) el resultado es falso con cualquier operador, <c>!=</c> incluido:
/// sin dato no se afirma nada.
/// </summary>
public sealed record ComparisonExpression(IExpression<RuleValue> Left, ComparisonOperator Operator, IExpression<RuleValue> Right)
    : IExpression<bool>
{
    public bool Interpret(IRuleContext context)
        => Left.Interpret(context).CompareTo(Right.Interpret(context)) is { } sign && Operator.Holds(sign);
}
