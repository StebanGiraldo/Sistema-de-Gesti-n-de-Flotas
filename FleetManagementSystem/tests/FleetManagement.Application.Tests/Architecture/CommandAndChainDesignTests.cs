using System.Reflection;
using System.Runtime.CompilerServices;
using FleetManagement.Application.AlertHandling;
using FleetManagement.Application.Commands;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Simulation;
using FleetManagement.Infrastructure.Commands;
using FleetManagement.Infrastructure.Navigation;
using Xunit;

namespace FleetManagement.Application.Tests.Architecture;

/// <summary>
/// Vigilan, sobre los ensamblados ya compilados, las reglas de diseño de Command y Chain of Responsibility que el README
/// promete: el invocador no conoce ninguna orden concreta (abierto a nuevas órdenes), las órdenes y los manejadores solo
/// reciben interfaces y datos (inversión de dependencias), las interfaces nuevas siguen siendo pequeñas y el código que ya
/// existía no sabe que estos patrones existen (se agregaron sin modificarlo).
///
/// Límite honesto: se revisan las FIRMAS de los tipos (base, interfaces, campos, propiedades, constructores y métodos), no el
/// cuerpo de cada método. Una dependencia que solo aparezca dentro de un método (por ejemplo un <c>new</c>) no la detecta
/// esta prueba; para eso están las pruebas de comportamiento de cada patrón.
/// </summary>
public class CommandAndChainDesignTests
{
    private static readonly Assembly ApplicationAssembly = typeof(IVehicleMovementSimulator).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(GoogleMapsAdapter).Assembly;

    private static readonly string CommandsNamespace = typeof(ICommandInvoker).Namespace!;
    private static readonly string AlertHandlingNamespace = typeof(IAlertHandler).Namespace!;
    private static readonly string InfrastructureCommandsNamespace = typeof(InMemoryCommandHistoryStore).Namespace!;
    private static readonly string RepositoriesNamespace = typeof(IDeliveryRouteRepository).Namespace!;
    private static readonly string DtosNamespace = typeof(CommandEntryDto).Namespace!;

    private const BindingFlags EverythingDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    // ---------- Command ----------

    [Fact]
    public void TheInvoker_TheHistory_AndTheStore_DoNotKnowAnyConcreteCommand()
    {
        // Si el invocador conociera una orden concreta, cada orden nueva obligaría a modificarlo (rompe abierto/cerrado).
        var concreteCommands = ConcreteClassesImplementing(ApplicationAssembly, typeof(IUndoableAction)).ToHashSet();
        Assert.NotEmpty(concreteCommands);

        var generic = new[]
        {
            typeof(ICommandInvoker), typeof(CommandInvoker), typeof(CommandHistory), typeof(HistoryEntry),
            typeof(ICommandHistoryStore), typeof(InMemoryCommandHistoryStore),
        };

        var offenders = new List<string>();
        foreach (var type in generic)
            foreach (var used in TypesInTheSignatureOf(type).Where(concreteCommands.Contains))
                offenders.Add($"{type.Name} conoce a {used.Name}");

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheConcreteCommands_OnlyReceiveInterfacesAndData_NeverRepositoriesNorConcreteServices()
    {
        // Una orden usa los servicios que ya existían (los receptores) y los datos de la petición; no toca repositorios.
        var commands = ConcreteClassesImplementing(ApplicationAssembly, typeof(IUndoableAction)).ToList();
        Assert.NotEmpty(commands);

        var offenders = new List<string>();
        foreach (var command in commands)
            foreach (var parameter in command.GetConstructors().SelectMany(ctor => ctor.GetParameters()))
            {
                var type = parameter.ParameterType;
                var isRepository = type.Namespace == RepositoriesNamespace;
                var isAbstraction = type.IsInterface;
                var isData = type == typeof(Guid) || type.Namespace == DtosNamespace;

                if (isRepository || !(isAbstraction || isData))
                    offenders.Add($"{command.Name} recibe {parameter.Name}: {type.Name}");
            }

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheCommandContracts_StaySmall_AndSeparate()
    {
        // ICommand<T> solo ejecuta; lo que el historial necesita (sin saber el tipo del resultado) está en IUndoableAction.
        Assert.Equal(1, MemberCountOf(typeof(ICommand<>)));
        Assert.Equal(4, MemberCountOf(typeof(IUndoableAction)));
        Assert.False(typeof(ICommand<>).GetMethods().Any(method => method.Name is "UndoAsync" or "RedoAsync"));
    }

    // ---------- Chain of Responsibility ----------

    [Fact]
    public void TheConcreteHandlers_OnlyReceiveInterfacesEnumsTextAndTheirOwnRules()
    {
        // Un manejador no conoce TripAlertService ni los repositorios: recibe interfaces, valores simples o la política/opciones del patrón.
        var handlers = ConcreteClassesImplementing(ApplicationAssembly, typeof(IAlertHandler)).ToList();
        Assert.NotEmpty(handlers);

        var offenders = new List<string>();
        foreach (var handler in handlers)
            foreach (var parameter in handler.GetConstructors().SelectMany(ctor => ctor.GetParameters()))
            {
                var type = parameter.ParameterType;
                var isRepository = type.Namespace == RepositoriesNamespace;
                var isAllowed = type.IsInterface || type.IsEnum || type == typeof(string) || type.Namespace == AlertHandlingNamespace;

                if (isRepository || !isAllowed)
                    offenders.Add($"{handler.Name} recibe {parameter.Name}: {type.Name}");
            }

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheHandlerContract_HasOnlyTwoMembers()
    {
        // Enlazar al sucesor y atender: nada más (segregación de interfaces).
        Assert.Equal(2, MemberCountOf(typeof(IAlertHandler)));
    }

    [Fact]
    public void TheAlertDecorator_KnowsTheServiceAndTheChain_ThroughInterfacesOnly()
    {
        var parameters = typeof(AlertHandlingTripAlertService).GetConstructors().SelectMany(ctor => ctor.GetParameters()).ToList();

        Assert.Equal(2, parameters.Count);
        Assert.All(parameters, parameter => Assert.True(parameter.ParameterType.IsInterface));
    }

    // ---------- Ambos: se agregaron sin modificar lo que ya existía ----------

    [Fact]
    public void TheExistingCode_DoesNotKnowCommandNorTheAlertChain()
    {
        var newPatterns = new[] { CommandsNamespace, AlertHandlingNamespace, InfrastructureCommandsNamespace };
        var existingTypes = new[] { ApplicationAssembly, InfrastructureAssembly }
            .SelectMany(TypesOf)
            .Where(type => !newPatterns.Contains(type.Namespace))
            .ToList();
        Assert.NotEmpty(existingTypes);

        var offenders = new List<string>();
        foreach (var type in existingTypes)
            foreach (var used in TypesInTheSignatureOf(type).Where(used => newPatterns.Contains(used.Namespace)))
                offenders.Add($"{type.FullName} conoce a {used.FullName}");

        Assert.Empty(offenders);
    }

    // ---------- Ayudas ----------

    private static IEnumerable<Type> ConcreteClassesImplementing(Assembly assembly, Type contract)
        => TypesOf(assembly).Where(type => type.IsClass && !type.IsAbstract && contract.IsAssignableFrom(type));

    /// <summary>Los tipos escritos a mano (sin los que genera el compilador para lambdas y métodos async).</summary>
    private static IEnumerable<Type> TypesOf(Assembly assembly)
    {
        Type?[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types; // los que no se pudieron cargar vienen como null
        }

        return types.OfType<Type>().Where(type => type.GetCustomAttribute<CompilerGeneratedAttribute>() is null);
    }

    private static int MemberCountOf(Type contract)
        => contract.GetMethods().Count(method => !method.IsSpecialName) + contract.GetProperties().Length;

    /// <summary>Todos los tipos que aparecen en la firma de <paramref name="type"/>, incluidos los argumentos genéricos y los arreglos.</summary>
    private static IEnumerable<Type> TypesInTheSignatureOf(Type type)
    {
        var found = new List<Type>();

        if (type.BaseType is not null)
            found.Add(type.BaseType);
        found.AddRange(type.GetInterfaces());

        foreach (var field in type.GetFields(EverythingDeclared))
            found.Add(field.FieldType);
        foreach (var property in type.GetProperties(EverythingDeclared))
            found.Add(property.PropertyType);
        foreach (var constructor in type.GetConstructors(EverythingDeclared))
            found.AddRange(constructor.GetParameters().Select(parameter => parameter.ParameterType));
        foreach (var method in type.GetMethods(EverythingDeclared))
        {
            found.Add(method.ReturnType);
            found.AddRange(method.GetParameters().Select(parameter => parameter.ParameterType));
        }

        return found.SelectMany(Flatten).Distinct();
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;

        if (type.HasElementType)
            foreach (var inner in Flatten(type.GetElementType()!))
                yield return inner;

        if (type.IsGenericType)
            foreach (var argument in type.GetGenericArguments())
                foreach (var inner in Flatten(argument))
                    yield return inner;
    }
}
