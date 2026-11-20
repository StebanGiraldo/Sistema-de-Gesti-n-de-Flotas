namespace FleetManagement.Application.Interpreter;

/// <summary>
/// Lo que una regla puede ver: de solo lectura y con un único método. Una variable que el contexto no conoce o de la que no
/// tiene dato se resuelve como <see cref="RuleValue.Missing"/> (nunca <c>null</c> ni un 0 inventado).
/// </summary>
public interface IRuleContext
{
    RuleValue Resolve(string variable);
}
