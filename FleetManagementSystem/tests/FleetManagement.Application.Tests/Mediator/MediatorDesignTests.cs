using System.Reflection;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Mediators;
using FleetManagement.Application.Services;
using Xunit;

namespace FleetManagement.Application.Tests.Mediator;

/// <summary>
/// Vigilan, sobre el ensamblado ya compilado, las reglas de diseño del Mediator que la documentación promete: los dos
/// participantes no se conocen entre sí, el mediador sólo conoce al de vehículos (al de rutas lo recibe en cada mensaje, así
/// que no hay referencias circulares), los servicios sólo hablan con el participante de rutas, ningún servicio depende de
/// otro servicio y las interfaces son pequeñas.
///
/// Límite honesto: se revisan las FIRMAS de los tipos (base, interfaces, campos, propiedades, constructores y métodos), no
/// el cuerpo de cada método. Lo que un método haga por dentro lo cubren las pruebas de comportamiento de esta carpeta.
/// </summary>
public class MediatorDesignTests
{
    private const BindingFlags EverythingDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Type[] MediatorTypes =
    {
        typeof(IFleetLifecycleMediator), typeof(FleetLifecycleMediator),
        typeof(IRouteColleague), typeof(RouteColleague),
        typeof(IVehicleColleague), typeof(VehicleColleague),
    };

    private static IReadOnlyCollection<Type> TypesInTheSignatureOf(Type type)
    {
        var found = new HashSet<Type>();

        void Add(Type candidate)
        {
            if (candidate.IsGenericType)
            {
                found.Add(candidate.GetGenericTypeDefinition());
                foreach (var argument in candidate.GetGenericArguments())
                    Add(argument);
            }
            else if (candidate.IsArray)
                Add(candidate.GetElementType()!);
            else
                found.Add(candidate);
        }

        if (type.BaseType is not null) Add(type.BaseType);
        foreach (var contract in type.GetInterfaces()) Add(contract);
        foreach (var field in type.GetFields(EverythingDeclared)) Add(field.FieldType);
        foreach (var property in type.GetProperties(EverythingDeclared)) Add(property.PropertyType);
        foreach (var constructor in type.GetConstructors(EverythingDeclared))
            foreach (var parameter in constructor.GetParameters()) Add(parameter.ParameterType);
        foreach (var method in type.GetMethods(EverythingDeclared))
        {
            Add(method.ReturnType);
            foreach (var parameter in method.GetParameters()) Add(parameter.ParameterType);
        }

        return found;
    }

    private static IEnumerable<Type> ConstructorParameterTypes(Type type)
        => type.GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType);

    [Fact]
    public void AllThePiecesLiveInTheMediatorsNamespace()
    {
        Assert.All(MediatorTypes, type => Assert.Equal("FleetManagement.Application.Mediators", type.Namespace));
    }

    [Fact]
    public void TheRouteParticipant_DoesNotKnowTheVehicleParticipant()
    {
        var known = TypesInTheSignatureOf(typeof(IRouteColleague)).Concat(TypesInTheSignatureOf(typeof(RouteColleague))).ToHashSet();

        Assert.DoesNotContain(typeof(IVehicleColleague), known);
        Assert.DoesNotContain(typeof(VehicleColleague), known);
    }

    [Fact]
    public void TheVehicleParticipant_DoesNotKnowTheRouteParticipantNorTheMediator()
    {
        var known = TypesInTheSignatureOf(typeof(IVehicleColleague)).Concat(TypesInTheSignatureOf(typeof(VehicleColleague))).ToHashSet();

        Assert.DoesNotContain(typeof(IRouteColleague), known);
        Assert.DoesNotContain(typeof(RouteColleague), known);
        Assert.DoesNotContain(typeof(IFleetLifecycleMediator), known);
        Assert.DoesNotContain(typeof(FleetLifecycleMediator), known);
    }

    [Fact]
    public void TheMediator_HoldsOnlyTheVehicleParticipant_TheRouteOneArrivesInsideEachMessage()
    {
        var fields = typeof(FleetLifecycleMediator).GetFields(EverythingDeclared).Select(field => field.FieldType).ToList();
        Assert.Equal(new[] { typeof(IVehicleColleague) }, fields.ToArray());

        // Sin referencia guardada al de rutas no hay ciclo mediador <-> participante: llega como "remitente" en el mensaje.
        var messages = typeof(IFleetLifecycleMediator).GetMethods();
        Assert.All(messages, message => Assert.Contains(message.GetParameters(), parameter => parameter.ParameterType == typeof(IRouteColleague)));
    }

    [Fact]
    public void TheMediator_IsNotADataAccessOrchestrator_ItKnowsNeitherRepositoriesNorServices()
    {
        var repositories = typeof(IDeliveryRouteRepository).Namespace;
        var services = typeof(IVehicleService).Namespace;

        var offenders = TypesInTheSignatureOf(typeof(FleetLifecycleMediator))
            .Concat(TypesInTheSignatureOf(typeof(IFleetLifecycleMediator)))
            .Where(type => type.Namespace == repositories || type.Namespace == services)
            .Select(type => type.Name)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheServices_OnlyKnowTheRouteParticipant_NotTheVehicleSideNorTheMediatorNorAnyConcreteClass()
    {
        foreach (var service in new[] { typeof(DeliveryRouteService), typeof(TripAlertService) })
        {
            var dependencies = ConstructorParameterTypes(service).ToList();

            Assert.Contains(typeof(IRouteColleague), dependencies);
            Assert.DoesNotContain(typeof(IVehicleColleague), dependencies);
            Assert.DoesNotContain(typeof(IFleetLifecycleMediator), dependencies);
            Assert.DoesNotContain(typeof(RouteColleague), dependencies);
            Assert.DoesNotContain(typeof(VehicleColleague), dependencies);
            Assert.DoesNotContain(typeof(FleetLifecycleMediator), dependencies);
        }
    }

    [Fact]
    public void NoServiceDependsOnAnotherService_SoTheyDoNotCoordinateByCallingEachOther()
    {
        var services = typeof(IVehicleService).Namespace;
        var offenders = new List<string>();

        foreach (var service in new[] { typeof(DeliveryRouteService), typeof(VehicleService), typeof(TripAlertService), typeof(MaintenanceService) })
            foreach (var dependency in ConstructorParameterTypes(service).Where(type => type.Namespace == services))
                offenders.Add($"{service.Name} depende de {dependency.Name}");

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheInterfaces_StayHandy_AFewMessagesEach()
    {
        Assert.Equal(2, typeof(IFleetLifecycleMediator).GetMethods().Length);
        Assert.Equal(3, typeof(IRouteColleague).GetMethods().Length);
        Assert.Equal(2, typeof(IVehicleColleague).GetMethods().Length);
    }

    [Fact]
    public void TheConcreteClasses_AreSealed_ThereIsOneWayToCoordinateAndItIsNotMeantToBeSubclassed()
    {
        Assert.All(new[] { typeof(FleetLifecycleMediator), typeof(RouteColleague), typeof(VehicleColleague) }, type => Assert.True(type.IsSealed));
    }
}
