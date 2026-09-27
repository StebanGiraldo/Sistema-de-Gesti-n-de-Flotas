using FleetManagement.Domain.Entities;

namespace FleetManagement.Application.Factories;

/// <summary>
/// PATRÓN FACTORY METHOD (creacional).
///
/// Cada tipo de vehículo tiene su propia fábrica concreta que sabe cómo
/// construirlo con los valores por defecto correctos (capacidad de carga,
/// etc.) sin que el código cliente (VehicleService) necesite conocer esos
/// detalles ni recurrir a una cadena de sentencias switch/if.
///
/// Beneficio (SOLID - Open/Closed): agregar un nuevo tipo de vehículo en el
/// futuro sólo requiere crear una nueva clase que implemente esta interfaz y
/// registrarla en VehicleFactoryProvider; ninguna clase existente se
/// modifica.
/// </summary>
public interface IVehicleFactory
{
    Vehicle CreateVehicle(string licensePlate, string brand, string model, int year, double latitude, double longitude);
}
