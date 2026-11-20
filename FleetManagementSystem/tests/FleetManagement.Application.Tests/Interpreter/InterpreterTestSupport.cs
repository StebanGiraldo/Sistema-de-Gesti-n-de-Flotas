using FleetManagement.Application.Interpreter;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Tests.Interpreter;

/// <summary>
/// Contexto de prueba (test double escrito a mano, sin librerías de mocking): devuelve lo que la prueba le pone y «sin dato»
/// para todo lo demás. Anota qué variables le preguntaron.
/// </summary>
public sealed class FakeRuleContext : IRuleContext
{
    private readonly Dictionary<string, RuleValue> _values = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Asked { get; } = new();

    public FakeRuleContext With(string name, double number)
    {
        _values[name] = RuleValue.FromNumber(number);
        return this;
    }

    public FakeRuleContext With(string name, string text)
    {
        _values[name] = RuleValue.FromText(text);
        return this;
    }

    public RuleValue Resolve(string variable)
    {
        Asked.Add(variable);
        return _values.GetValueOrDefault(variable, RuleValue.Missing);
    }
}

/// <summary>Expresión booleana de prueba que cuenta cuántas veces se la interpretó (para comprobar el cortocircuito).</summary>
public sealed class CountingExpression : IExpression<bool>
{
    private readonly bool _result;

    public CountingExpression(bool result)
    {
        _result = result;
    }

    public int Calls { get; private set; }

    public bool Interpret(IRuleContext context)
    {
        Calls++;
        return _result;
    }
}

/// <summary>Datos y atajos comunes de las pruebas del Interpreter.</summary>
public static class RuleTestData
{
    /// <summary>Variables de prueba para las pruebas del parser (no pertenecen a ningún contexto real).</summary>
    public static readonly RuleVocabulary Vocabulary = new(new[] { "a", "b", "c", "mileageKm", "status", "type", "kmSinceLastService" });

    public static IExpression<bool> Parse(string rule) => new RuleParser().Parse(rule, Vocabulary);

    public static bool Evaluate(string rule, IRuleContext context) => Parse(rule).Interpret(context);

    public static Vehicle Vehicle(
        string plate = "TRK-001",
        VehicleType type = VehicleType.Truck,
        VehicleStatus status = VehicleStatus.Available,
        double mileageKm = 10000,
        double capacityKg = 8000,
        int year = 2020,
        Guid? driverId = null)
        => new()
        {
            LicensePlate = plate,
            Type = type,
            Status = status,
            MileageKm = mileageKm,
            CapacityKg = capacityKg,
            Year = year,
            AssignedDriverId = driverId
        };
}
