using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Domain.Entities;

namespace FleetManagement.Infrastructure.Persistence.Demo;

/// <summary>Da de alta los vehículos de demostración, con su ubicación y, si van en viaje, su conductor.</summary>
public sealed class DemoVehicleSeeder
{
    private readonly IVehicleRepository _vehicles;
    private readonly DemoLookup _lookup;

    public DemoVehicleSeeder(IVehicleRepository vehicles, DemoLookup lookup)
    {
        _vehicles = vehicles;
        _lookup = lookup;
    }

    public async Task SeedAsync(IEnumerable<DemoVehicle> vehicles)
    {
        foreach (var spec in vehicles)
            await _vehicles.AddAsync(await BuildAsync(spec));
    }

    private async Task<Vehicle> BuildAsync(DemoVehicle spec)
    {
        var place = DemoPlaces.Of(spec.Place);
        var vehicle = new Vehicle
        {
            LicensePlate = spec.Plate,
            Brand = spec.Brand,
            Model = spec.Model,
            Year = spec.Year,
            Type = spec.Type,
            CapacityKg = spec.CapacityKg,
            CapacityM3 = spec.CapacityM3,
            Status = spec.Status,
            MileageKm = spec.MileageKm,
            AssignedDriverId = spec.DriverUsername is null ? null : await _lookup.DriverIdAsync(spec.DriverUsername)
        };
        vehicle.UpdateLocation(place.Latitude, place.Longitude);
        return vehicle;
    }
}
