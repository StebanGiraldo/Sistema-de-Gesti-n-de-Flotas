namespace FleetManagement.Application.Interpreter;

/// <summary>
/// Una expresión del lenguaje de reglas: una sola operación, interpretarse contra un contexto. El tipo del resultado deja la
/// gramática en el sistema de tipos: los terminales (<see cref="LiteralExpression"/>, <see cref="VariableExpression"/>) dan un
/// <see cref="RuleValue"/>; las comparaciones y los operadores lógicos dan <c>bool</c>. Así un árbol mal armado (un <c>AND</c> sobre
/// números) ni siquiera compila.
/// </summary>
public interface IExpression<out TResult>
{
    TResult Interpret(IRuleContext context);
}
