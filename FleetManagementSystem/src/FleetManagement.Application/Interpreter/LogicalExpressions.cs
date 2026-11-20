namespace FleetManagement.Application.Interpreter;

/// <summary>Verdadera si lo son las dos partes; no evalúa la derecha si la izquierda ya es falsa.</summary>
public sealed record AndExpression(IExpression<bool> Left, IExpression<bool> Right) : IExpression<bool>
{
    public bool Interpret(IRuleContext context) => Left.Interpret(context) && Right.Interpret(context);
}

/// <summary>Verdadera si lo es alguna de las dos partes; no evalúa la derecha si la izquierda ya es verdadera.</summary>
public sealed record OrExpression(IExpression<bool> Left, IExpression<bool> Right) : IExpression<bool>
{
    public bool Interpret(IRuleContext context) => Left.Interpret(context) || Right.Interpret(context);
}

public sealed record NotExpression(IExpression<bool> Operand) : IExpression<bool>
{
    public bool Interpret(IRuleContext context) => !Operand.Interpret(context);
}
