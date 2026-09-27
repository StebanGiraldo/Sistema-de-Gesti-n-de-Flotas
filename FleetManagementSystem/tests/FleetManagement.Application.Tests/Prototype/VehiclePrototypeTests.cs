using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Prototype;

/// <summary>
/// PRUEBAS DEL PATRÓN PROTOTYPE sobre Vehicle.Clone().
///
/// Verifican dos cosas a la vez, como debe hacerlo todo Prototype bien
/// implementado: (1) qué se COPIA del original (marca, modelo, año,
/// capacidad, ubicación) y (2) qué se REINICIA deliberadamente a un estado
/// "nuevo" en el clon (Id, placa, kilometraje, conductor asignado), para que
/// dos vehículos clonados nunca se confundan entre sí ni compartan recursos.
/// </summary>
public class VehiclePrototypeTests
{
    private static Vehicle CreateSourceVehicle() => new()
    {
        LicensePlate = "TRK-900",
        Brand = "Volvo",
        Model = "FH 460",
        Year = 2023,
        Type = VehicleType.Truck,
        Status = VehicleStatus.EnRoute,
        CapacityKg = 8000,
        MileageKm = 45000,
        AssignedDriverId = Guid.NewGuid(),
        LastMaintenanceDate = DateTime.UtcNow.AddDays(-10)
    };

    [Fact]
    public void Clone_CopiesDescriptiveAndCapacityFields_FromOriginal()
    {
        var original = CreateSourceVehicle();
        original.UpdateLocation(7.1193, -73.1227);

        var clone = original.Clone();

        Assert.Equal(original.Brand, clone.Brand);
        Assert.Equal(original.Model, clone.Model);
        Assert.Equal(original.Year, clone.Year);
        Assert.Equal(original.Type, clone.Type);
        Assert.Equal(original.CapacityKg, clone.CapacityKg);
        Assert.Equal(original.CurrentLocation.Y, clone.CurrentLocation.Y, 4);
        Assert.Equal(original.CurrentLocation.X, clone.CurrentLocation.X, 4);
    }

    [Fact]
    public void Clone_ResetsIdentityAndOperationalFields_ToFreshVehicleState()
    {
        var original = CreateSourceVehicle();

        var clone = original.Clone();

        Assert.NotEqual(original.Id, clone.Id);
        Assert.Equal(string.Empty, clone.LicensePlate); // debe asignarse individualmente tras clonar
        Assert.Equal(0, clone.MileageKm);
        Assert.Equal(VehicleStatus.Available, clone.Status);
        Assert.Null(clone.AssignedDriverId);
        Assert.Null(clone.LastMaintenanceDate);
    }

    [Fact]
    public void Clone_ProducesIndependentLocationInstance_NotSharedWithOriginal()
    {
        // Copia PROFUNDA: mover el vehículo original después de clonar no
        // debe alterar la ubicación ya copiada en el clon.
        var original = CreateSourceVehicle();
        original.UpdateLocation(7.0, -73.0);

        var clone = original.Clone();
        original.UpdateLocation(9.0, -75.0);

        Assert.Equal(7.0, clone.CurrentLocation.Y, 4);
        Assert.Equal(-73.0, clone.CurrentLocation.X, 4);
    }
}
