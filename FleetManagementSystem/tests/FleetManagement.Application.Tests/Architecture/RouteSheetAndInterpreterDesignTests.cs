using System.Reflection;
using System.Runtime.CompilerServices;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Interpreter;
using FleetManagement.Application.RouteSheets;
using FleetManagement.Application.Simulation;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Infrastructure.Navigation;
using Xunit;

namespace FleetManagement.Application.Tests.Architecture;

/// <summary>
/// Vigilan, sobre los ensamblados ya compilados, las reglas de diseño de Interpreter y del Template Method de la hoja de ruta:
/// el código que ya existía no sabe que existen, ambos dependen solo del dominio y de abstracciones de la aplicación, las
/// interfaces siguen siendo mínimas, las expresiones son inmutables y las variantes solo sobrescriben miembros protegidos.
///
/// Límite honesto (el mismo de <see cref="CommandAndChainDesignTests"/>): se revisan las FIRMAS de los tipos, no el cuerpo de cada
/// método. Que el intérprete no evalúe código (sin reflexión ni compilación dinámica) se vigila a nivel de ensamblado y se
/// comprueba, además, con las pruebas de comportamiento del analizador. El Template Method que ya existía en
/// <c>Application/Commands/RouteCreationCommand</c> es otro patrón y no se menciona aquí porque no se toca.
/// </summary>
public class RouteSheetAndInterpreterDesignTests
{
    private static readonly Assembly ApplicationAssembly = typeof(IVehicleMovementSimulator).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(GoogleMapsAdapter).Assembly;

    private static readonly string InterpreterNamespace = typeof(IExpression<>).Namespace!;
    private static readonly string RouteSheetsNamespace = typeof(RouteSheetTemplate).Namespace!;
    private static readonly string DomainRoot = typeof(Vehicle).Namespace!.Split('.').Take(2).Aggregate((a, b) => $"{a}.{b}");

    private static readonly string RepositoriesNamespace = typeof(IDeliveryRouteRepository).Namespace!;
    private static readonly string ServicesNamespace = typeof(INavigationService).Namespace!;
    private static readonly string DtosNamespace = typeof(TripProgressDto).Namespace!;
    private static readonly string SimulationNamespace = typeof(RoutePath).Namespace!;

    private const BindingFlags EverythingDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    // ---------- Se agregaron sin modificar lo que ya existía ----------

    [Fact]
    public void TheExistingCode_DoesNotKnowTheInterpreterNorTheRouteSheets()
    {
        var newNamespaces = new[] { InterpreterNamespace, RouteSheetsNamespace };
        var existing = new[] { ApplicationAssembly, InfrastructureAssembly }
            .SelectMany(assembly => SignatureDependencies.TypesOutside(assembly, newNamespaces))
            .ToList();
        Assert.NotEmpty(existing);

        var offenders = existing
            .SelectMany(type => SignatureDependencies.Of(type).Where(used => newNamespaces.Contains(used.Namespace)).Select(used => $"{type.FullName} conoce a {used.FullName}"))
            .ToList();

        Assert.Empty(offenders);
    }

    // ---------- Dependen solo del dominio y de abstracciones ----------

    [Fact]
    public void TheInterpreter_DependsOnlyOnTheDomainAndOnTheRepositoryAbstractions()
        => AssertDependsOnlyOn(InterpreterNamespace, RepositoriesNamespace);

    [Fact]
    public void TheRouteSheets_DependOnlyOnTheDomainAndOnApplicationAbstractionsAndData()
        => AssertDependsOnlyOn(RouteSheetsNamespace, RepositoriesNamespace, ServicesNamespace, DtosNamespace, SimulationNamespace);

    [Fact]
    public void TheRouteSheetTemplate_ReceivesItsCollaboratorsOnlyThroughInterfaces()
    {
        var collaborators = typeof(RouteSheetCollaborators).GetConstructors().Single().GetParameters();

        Assert.Equal(3, collaborators.Length);
        Assert.All(collaborators, parameter => Assert.True(parameter.ParameterType.IsInterface, parameter.Name));
        Assert.Equal(typeof(RouteSheetCollaborators), Assert.Single(typeof(RouteSheetTemplate).GetConstructors(EverythingDeclared)).GetParameters()[1].ParameterType);
    }

    [Fact]
    public void TheInterpreterAssembly_DoesNotLoadRoslynNorDynamicCodeGeneration()
    {
        var referenced = ApplicationAssembly.GetReferencedAssemblies().Select(name => name.Name!).ToList();

        Assert.DoesNotContain(referenced, name => name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, name => name.StartsWith("System.Reflection.Emit", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, name => name.StartsWith("System.CodeDom", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, name => name.StartsWith("System.Linq.Dynamic", StringComparison.Ordinal));
    }

    // ---------- Interfaces mínimas ----------

    [Fact]
    public void TheInterpreterContracts_StaySmall()
    {
        Assert.Equal(1, MemberCountOf(typeof(IExpression<>)));      // Interpret
        Assert.Equal(1, MemberCountOf(typeof(IRuleContext)));       // Resolve
        Assert.Equal(1, MemberCountOf(typeof(IRuleParser)));        // Parse
        Assert.Equal(2, MemberCountOf(typeof(IRuleScope)));         // Vocabulary y LoadSubjectsAsync
        Assert.Equal(2, MemberCountOf(typeof(IRuleEvaluationService)));
    }

    [Fact]
    public void TheRouteSheetContracts_StaySmall()
    {
        Assert.Equal(1, MemberCountOf(typeof(IRouteSheetProvider)));       // For
        Assert.Equal(1, MemberCountOf(typeof(IRouteSheetSourceLoader)));   // LoadAsync
    }

    // ---------- Interpreter: expresiones inmutables, una operación, sin estado compartido ----------

    [Fact]
    public void EveryExpression_HasASingleOperation_AndIsImmutable()
    {
        var expressions = ExpressionTypes().ToList();
        Assert.Equal(6, expressions.Count);   // Literal, Variable, And, Or, Not, Comparison

        var offenders = new List<string>();
        foreach (var type in expressions)
        {
            offenders.AddRange(MutableMembersOf(type).Select(member => $"{type.Name}.{member}"));
            var operations = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(IsOperation).ToList();
            if (operations.Count != 1 || operations[0].Name != nameof(IExpression<bool>.Interpret))
                offenders.Add($"{type.Name} tiene operaciones públicas: {string.Join(", ", operations.Select(method => method.Name))}");
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheValuesTheRuleHandles_AreImmutable()
    {
        var values = TypesIn(InterpreterNamespace).Where(type => type.IsSubclassOf(typeof(RuleValue)) || type == typeof(RuleValue)).ToList();
        Assert.True(values.Count >= 3);   // RuleValue, el número, el texto (y el «sin dato» anidado)

        Assert.Empty(values.SelectMany(type => MutableMembersOf(type).Select(member => $"{type.Name}.{member}")));
    }

    [Fact]
    public void OnlyTwoExpressionsAreTerminal_AndTheOthersAreBuiltFromExpressions()
    {
        var terminals = ExpressionTypes()
            .Where(type => !type.GetConstructors().Single().GetParameters().Any(parameter => IsExpression(parameter.ParameterType)))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(new[] { nameof(LiteralExpression), nameof(VariableExpression) }, terminals);
    }

    [Fact]
    public void TheNewNamespaces_HaveNoMutableStaticState()
    {
        var mutable = TypesIn(InterpreterNamespace).Concat(TypesIn(RouteSheetsNamespace))
            .SelectMany(type => type.GetFields(EverythingDeclared | BindingFlags.FlattenHierarchy))
            .Where(field => field.IsStatic && !field.IsInitOnly && !field.IsLiteral)
            .Select(field => $"{field.DeclaringType!.Name}.{field.Name}");

        Assert.Empty(mutable);
    }

    // ---------- Template Method: esqueleto fijo, variantes que solo completan pasos protegidos ----------

    [Fact]
    public void TheTemplate_ExposesOnlyItsSkeleton_AndItsExtensionPointsAreProtected()
    {
        var template = typeof(RouteSheetTemplate);

        var publicOperations = template.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(IsOperation).Select(method => method.Name);
        var publicProperties = template.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(property => property.Name);
        var extensionPoints = template.GetMethods(EverythingDeclared).Where(method => method.IsVirtual && !method.IsFinal).ToList();

        Assert.Equal(new[] { nameof(RouteSheetTemplate.BuildAsync) }, publicOperations);
        Assert.Equal(new[] { nameof(RouteSheetTemplate.Audience) }, publicProperties);
        Assert.All(extensionPoints, method => Assert.True(method.IsFamily, $"{method.Name} no es protected"));
        Assert.InRange(extensionPoints.Count, 1, 6);   // el esqueleto tiene pocos pasos que varían
    }

    [Fact]
    public void OnlyTheStepsEveryAudienceMustDecide_AreAbstract()
    {
        var abstractSteps = typeof(RouteSheetTemplate).GetMethods(EverythingDeclared).Where(method => method.IsAbstract).Select(method => method.Name);

        Assert.Equal(new[] { "BuildHeader", "BuildProgress" }, abstractSteps.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TheVariants_AreSealed_AndOverrideOnlyProtectedMembers()
    {
        var variants = ApplicationAssembly.GetTypes().Where(type => type.IsSubclassOf(typeof(RouteSheetTemplate))).ToList();
        Assert.NotEmpty(variants);

        var offenders = new List<string>();
        foreach (var variant in variants)
        {
            if (!variant.IsSealed)
                offenders.Add($"{variant.Name} no es sealed");

            offenders.AddRange(variant.GetMethods(EverythingDeclared)
                .Where(method => method.GetBaseDefinition().DeclaringType != variant && !method.IsFamily)
                .Select(method => $"{variant.Name}.{method.Name} sobrescribe un miembro que no es protected"));
        }

        Assert.Empty(offenders);
    }

    // ---------- Ayudas ----------

    private static IReadOnlyList<Type> TypesIn(string @namespace)
        => ApplicationAssembly.GetTypes()
            .Where(type => type.Namespace == @namespace && type.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .ToList();

    private static void AssertDependsOnlyOn(string @namespace, params string[] allowedNamespaces)
    {
        var offenders = new List<string>();
        foreach (var type in TypesIn(@namespace))
            foreach (var used in SignatureDependencies.Of(type))
            {
                var allowed = used.Namespace == @namespace
                              || used.Namespace!.StartsWith(DomainRoot, StringComparison.Ordinal)
                              || allowedNamespaces.Contains(used.Namespace);
                if (!allowed)
                    offenders.Add($"{type.Name} conoce a {used.FullName}");
            }

        Assert.Empty(offenders);
    }

    private static int MemberCountOf(Type contract)
        => contract.GetMethods().Count(method => !method.IsSpecialName) + contract.GetProperties().Length;

    private static bool IsOperation(MethodInfo method)
        => !method.IsSpecialName && method.DeclaringType != typeof(object) && !IsRecordPlumbing(method);

    // Los métodos que el compilador genera en cada record (comparación, copia, texto) no son operaciones del patrón.
    private static bool IsRecordPlumbing(MethodInfo method)
        => method.Name is "Equals" or "GetHashCode" or "ToString" or "Deconstruct" or "PrintMembers" or "<Clone>$";

    private static bool IsExpression(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IExpression<>);

    private static IEnumerable<Type> ExpressionTypes()
        => TypesIn(InterpreterNamespace).Where(type => type.IsClass && !type.IsAbstract && type.GetInterfaces().Any(IsExpression));

    // Una propiedad con set normal, o un campo escrito a mano que no sea de solo lectura, es estado que puede cambiar.
    private static IEnumerable<string> MutableMembersOf(Type type)
    {
        var mutableProperties = type.GetProperties(EverythingDeclared)
            .Where(property => property.SetMethod is { } setter && !setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))
            .Select(property => property.Name);
        var mutableFields = type.GetFields(EverythingDeclared)
            .Where(field => !field.IsStatic && !field.IsInitOnly && field.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .Select(field => field.Name);

        return mutableProperties.Concat(mutableFields);
    }
}
