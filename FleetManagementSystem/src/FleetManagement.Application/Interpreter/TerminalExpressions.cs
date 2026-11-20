namespace FleetManagement.Application.Interpreter;

/// <summary>
/// Terminal de un valor escrito en la regla: sirve igual para un número que para un texto (no hay una clase por literal).
/// Es inmutable.
/// </summary>
public sealed record LiteralExpression(RuleValue Value) : IExpression<RuleValue>
{
    public RuleValue Interpret(IRuleContext context) => Value;
}

/// <summary>Terminal de una variable: su valor lo da el contexto. Una sola clase para todas las variables.</summary>
public sealed record VariableExpression(string Name) : IExpression<RuleValue>
{
    public RuleValue Interpret(IRuleContext context) => context.Resolve(Name);
}
