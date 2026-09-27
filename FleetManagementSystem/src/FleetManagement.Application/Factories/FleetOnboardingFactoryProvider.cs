using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Factories;

/// <summary>Resuelve la fábrica abstracta adecuada según el tipo de vehículo, análoga a VehicleFactoryProvider pero para la familia de mantenimiento/navegación.</summary>
public class FleetOnboardingFactoryProvider
{
    private readonly Dictionary<VehicleType, IFleetOnboardingAbstractFactory> _factories;

    public FleetOnboardingFactoryProvider()
    {
        _factories = new Dictionary<VehicleType, IFleetOnboardingAbstractFactory>
        {
            [VehicleType.Truck] = new TruckOnboardingFactory(),
            [VehicleType.Van] = new VanOnboardingFactory(),
            [VehicleType.Car] = new CarOnboardingFactory(),
            [VehicleType.Motorcycle] = new MotorcycleOnboardingFactory()
        };
    }

    public IFleetOnboardingAbstractFactory GetFactory(VehicleType type)
    {
        if (!_factories.TryGetValue(type, out var factory))
        {
            throw new NotSupportedException($"No existe una fábrica de incorporación registrada para el tipo de vehículo '{type}'.");
        }
        return factory;
    }
}
