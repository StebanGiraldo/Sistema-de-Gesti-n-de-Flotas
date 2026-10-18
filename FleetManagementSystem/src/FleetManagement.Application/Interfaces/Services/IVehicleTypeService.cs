using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Interfaces.Services;

public interface IVehicleTypeService
{
    /// <summary>Un perfil por cada tipo de vehículo, en el orden en que se declaran los tipos.</summary>
    IReadOnlyList<VehicleTypeProfileDto> GetAllProfiles();
}
