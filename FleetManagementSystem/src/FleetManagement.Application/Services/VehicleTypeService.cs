using FleetManagement.Application.DTOs;
using FleetManagement.Application.Flyweights;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Services;

/// <summary>
/// Consulta de los perfiles por tipo de vehículo. Es un consumidor del patrón Flyweight: no construye nada, pide cada
/// perfil a <see cref="IVehicleTypeProfileFactory"/> (siempre la misma instancia compartida) y solo lo convierte a DTO.
/// </summary>
public class VehicleTypeService : IVehicleTypeService
{
    private readonly IVehicleTypeProfileFactory _profiles;

    public VehicleTypeService(IVehicleTypeProfileFactory profiles)
    {
        _profiles = profiles;
    }

    public IReadOnlyList<VehicleTypeProfileDto> GetAllProfiles()
        => Enum.GetValues<VehicleType>().Select(type => ToDto(_profiles.GetProfile(type))).ToList();

    private static VehicleTypeProfileDto ToDto(VehicleTypeProfile profile) => new(
        profile.Type.ToString(),
        profile.DisplayName,
        profile.MaxSpeedKmh,
        profile.AvoidHighways,
        profile.AvoidTolls,
        profile.AvoidLowBridges,
        profile.MaintenanceTasks
            .Select(task => new MaintenanceTaskTemplateDto(task.TaskName, task.Type.ToString(), task.IntervalDays, task.IntervalKm))
            .ToList());
}
