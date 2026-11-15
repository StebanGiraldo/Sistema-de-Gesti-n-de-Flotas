using System.Reflection;
using System.Runtime.CompilerServices;
using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Flyweights;
using FleetManagement.Application.Mediators;
using FleetManagement.Application.Monitoring;
using FleetManagement.Application.Simulation;
using FleetManagement.Application.Tests.Support;
using Xunit;

namespace FleetManagement.Application.Tests.Observer;

/// <summary>
/// Pruebas de DISEÑO del patrón Observer: que cada pieza viva donde debe y dependa solo de lo que debe. No verifican que los
/// eventos lleguen (eso lo hacen las demás pruebas de esta carpeta) sino que el patrón no se convierta en otro acoplamiento:
/// no duplica al Bridge (las notificaciones a personas), no reemplaza al Mediator (la coordinación ruta ↔ vehículo) y el
/// simulador solo conoce el puerto estrecho para publicar.
/// </summary>
public class ObserverDesignTests
{
    private const string Monitoring = "FleetManagement.Application.Monitoring";
    private const string Simulation = "FleetManagement.Application.Simulation";
    private const string Mediators = "FleetManagement.Application.Mediators";
    private const string Notifications = "FleetManagement.Application.Notifications";

    private static readonly Assembly Application = typeof(ITelemetryObserver).Assembly;

    private static IEnumerable<string> NamespacesUsedBy(IEnumerable<Type> types)
        => types.SelectMany(SignatureDependencies.Of).Select(dependency => dependency.Namespace!).Distinct().Order();

    // ---------------- Dónde vive cada pieza ----------------

    [Fact]
    public void Monitoring_DependsOnlyOnTheAuditPortAndTheLiveStatusDto()
    {
        var used = NamespacesUsedBy(SignatureDependencies.TypesIn(Application, Monitoring)).ToArray();

        // Sale de sí mismo hacia la bitácora (puerto) y hacia el DTO del tablero; nada de rutas, simulación, notificaciones ni servicios.
        Assert.Equal(
            new[] { Monitoring, "FleetManagement.Application.DTOs", "FleetManagement.Application.Interfaces" }.Order().ToArray(),
            used);
    }

    [Fact]
    public void OnlyTheSimulationSide_KnowsTheMonitoringTypes()
    {
        var knowing = SignatureDependencies.TypesOutside(Application, Monitoring)
            .Where(type => SignatureDependencies.Of(type).Any(dependency => dependency.Namespace == Monitoring))
            .Select(type => $"{type.Namespace}.{type.Name}")
            .Order()
            .ToArray();

        Assert.Equal(new[] { $"{Simulation}.{nameof(TripTelemetryEvents)}", $"{Simulation}.{nameof(VehicleMovementSimulator)}" }, knowing);
    }

    [Fact]
    public void TheMediator_AndTheBridge_KnowNothingOfTheObserver()
    {
        var mediatorSide = SignatureDependencies.TypesIn(Application, Mediators);
        var bridgeSide = SignatureDependencies.TypesIn(Application, Notifications);

        Assert.DoesNotContain(NamespacesUsedBy(mediatorSide), @namespace => @namespace == Monitoring);
        Assert.DoesNotContain(NamespacesUsedBy(bridgeSide), @namespace => @namespace == Monitoring);
    }

    [Fact]
    public void NoObserver_IsAMediatorParticipant_OrANotificationChannel()
    {
        var observers = Application.GetTypes().Where(type => typeof(ITelemetryObserver).IsAssignableFrom(type) && type.IsClass).ToList();

        Assert.NotEmpty(observers);
        foreach (var observer in observers)
        {
            Assert.False(typeof(IRouteColleague).IsAssignableFrom(observer), $"{observer.Name} no debe ser un participante del Mediator");
            Assert.False(typeof(IVehicleColleague).IsAssignableFrom(observer), $"{observer.Name} no debe ser un participante del Mediator");
            Assert.False(typeof(IFleetLifecycleMediator).IsAssignableFrom(observer), $"{observer.Name} no debe reemplazar al Mediator");
            Assert.False(typeof(INotificationChannel).IsAssignableFrom(observer), $"{observer.Name} no debe duplicar al Bridge");
        }
    }

    [Fact]
    public void TheTwoConcreteObservers_AreTheOnesThatWereDesigned()
    {
        var observers = Application.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && !type.IsNested && typeof(ITelemetryObserver).IsAssignableFrom(type))
            .Select(type => type.Name)
            .Order()
            .ToArray();

        Assert.Equal(new[] { nameof(LiveTelemetryBoard), nameof(TripAuditObserver) }, observers);
    }

    // ---------------- El simulador publica por un puerto estrecho ----------------

    [Fact]
    public void TheSimulator_KeepsItsOriginalConstructor_AndOnlyAddsOptionalParametersAtTheEnd()
    {
        var constructor = Assert.Single(typeof(VehicleMovementSimulator).GetConstructors());
        var parameters = constructor.GetParameters();

        Assert.Equal(
            new[]
            {
                typeof(IDeliveryRouteRepository), typeof(IVehicleRepository), typeof(IDeliveryRouteService),
                typeof(ITripProgressTracker), typeof(IVehicleTypeProfileFactory), typeof(SimulationOptions)
            },
            parameters.Take(6).Select(parameter => parameter.ParameterType));
        Assert.All(parameters.Take(6), parameter => Assert.False(parameter.IsOptional));
        Assert.Equal(new[] { typeof(ITelemetryPublisher), typeof(TimeProvider) }, parameters.Skip(6).Select(parameter => parameter.ParameterType));
        Assert.All(parameters.Skip(6), parameter =>
        {
            Assert.True(parameter.IsOptional);
            Assert.Null(parameter.DefaultValue);
        });
    }

    [Fact]
    public void TheSimulator_PublishesThroughTheNarrowPortOnly()
    {
        var dependencies = SignatureDependencies.Of(typeof(VehicleMovementSimulator));

        Assert.Contains(typeof(ITelemetryPublisher), dependencies);
        Assert.DoesNotContain(typeof(ITelemetrySubject), dependencies);       // no suscribe ni cancela observadores
        Assert.DoesNotContain(typeof(ITelemetryObserver), dependencies);
        Assert.DoesNotContain(typeof(ILiveTelemetryBoard), dependencies);
        Assert.DoesNotContain(typeof(LiveTelemetryBoard), dependencies);
        Assert.DoesNotContain(typeof(TripAuditObserver), dependencies);
        Assert.DoesNotContain(typeof(VehicleTelemetryPublisher), dependencies);   // depende de la abstracción, no del publicador concreto
    }

    [Fact]
    public void ThePublishingPort_AndTheSubscriptionPort_AreSeparateSmallContracts()
    {
        Assert.Equal(new[] { "Publish" }, MembersOf(typeof(ITelemetryPublisher)));
        Assert.Equal(new[] { "ObserverCount", "Subscribe", "Unsubscribe" }, MembersOf(typeof(ITelemetrySubject)));
        Assert.Equal(new[] { "OnTelemetry" }, MembersOf(typeof(ITelemetryObserver)));
        Assert.Equal(new[] { "Get", "GetAll" }, MembersOf(typeof(ILiveTelemetryBoard)));

        // El publicador concreto cumple los dos puertos, pero cada cliente ve solo el suyo.
        Assert.True(typeof(ITelemetryPublisher).IsAssignableFrom(typeof(VehicleTelemetryPublisher)));
        Assert.True(typeof(ITelemetrySubject).IsAssignableFrom(typeof(VehicleTelemetryPublisher)));
        Assert.False(typeof(ITelemetrySubject).IsAssignableFrom(typeof(ITelemetryPublisher)));
        Assert.False(typeof(ITelemetryPublisher).IsAssignableFrom(typeof(ITelemetrySubject)));
    }

    [Fact]
    public void TheTelemetryEvents_AreImmutable()
    {
        var eventTypes = Application.GetTypes().Where(type => typeof(TelemetryEvent).IsAssignableFrom(type)).ToList();

        Assert.Equal(4, eventTypes.Count);   // la base y los tres eventos
        foreach (var eventType in eventTypes)
        {
            foreach (var property in eventType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var setter = property.SetMethod;
                var isInitOnly = setter is not null && setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));
                Assert.True(setter is null || isInitOnly || !setter.IsPublic, $"{eventType.Name}.{property.Name} se puede modificar después de publicado");
            }
        }
        Assert.All(eventTypes.Where(type => type != typeof(TelemetryEvent)), type => Assert.True(type.IsSealed));
    }

    [Fact]
    public void TheConcreteObserverTypes_AreSealed_AndTakeOnlyAbstractionsInTheirConstructors()
    {
        var concrete = new[] { typeof(VehicleTelemetryPublisher), typeof(LiveTelemetryBoard), typeof(TripAuditObserver) };

        Assert.All(concrete, type => Assert.True(type.IsSealed, $"{type.Name} no es sealed"));
        foreach (var parameter in concrete.SelectMany(type => type.GetConstructors()).SelectMany(constructor => constructor.GetParameters()))
        {
            var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
            var elementType = type.IsGenericType ? type.GetGenericArguments()[0] : type;   // IEnumerable<ITelemetryObserver>
            Assert.True(elementType.IsInterface, $"El parámetro {parameter.Name} depende de la clase concreta {elementType.Name}");
        }
    }

    private static string[] MembersOf(Type contract)
        => contract.GetProperties().Select(property => property.Name)
            .Concat(contract.GetMethods().Where(method => !method.IsSpecialName).Select(method => method.Name))
            .Order()
            .ToArray();
}
