using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Factories;

/// <summary>
/// Resuelve la fábrica concreta adecuada (Factory Method) según el tipo de
/// vehículo solicitado. El resto de la aplicación programa contra
/// IVehicleFactory (DIP) y nunca contra TruckFactory/VanFactory/etc.
/// directamente, de modo que VehicleService no necesita cambiar si se
/// agregan nuevos tipos de vehículo.
/// </summary>
public class VehicleFactoryProvider
{
    private readonly Dictionary<VehicleType, IVehicleFactory> _factories;

    public VehicleFactoryProvider()
    {
        _factories = new Dictionary<VehicleType, IVehicleFactory>
        {
            [VehicleType.Truck] = new TruckFactory(),
            [VehicleType.Van] = new VanFactory(),
            [VehicleType.Car] = new CarFactory(),
            [VehicleType.Motorcycle] = new MotorcycleFactory()
        };
    }

    public IVehicleFactory GetFactory(VehicleType type)
    {
        if (!_factories.TryGetValue(type, out var factory))
        {
            throw new NotSupportedException($"No existe una fábrica registrada para el tipo de vehículo '{type}'.");
        }
        return factory;
    }
}
