using FleetManagement.Application.Builders;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Assignment;

/// <summary>
/// PATRÓN DECORATOR sobre <see cref="IDeliveryRouteService"/>: antes de crear una ruta o de sacarla a ruta, comprueba
/// las reglas de asignación (<see cref="IRouteAssignmentValidator"/>) y solo entonces delega en el servicio real, que no
/// se entera. Así las reglas viven en un solo lugar, el servicio de rutas no crece y se pueden quitar o cambiar sin tocarlo
/// (Open/Closed). Todo lo demás se delega tal cual.
///
/// Se registra en <c>Program.cs</c> como la implementación de <see cref="IDeliveryRouteService"/>: por eso también protege
/// al simulador y a cualquier otro que use esa interfaz. Terminar o cancelar un viaje nunca se bloquea.
/// </summary>
public sealed class AssignmentValidatingRouteService : IDeliveryRouteService
{
    private static readonly CargoItemDto ExpressCargo = new(
        DeliveryRouteDirector.ExpressCargoDescription,
        DeliveryRouteDirector.ExpressCargoWeightKg,
        DeliveryRouteDirector.ExpressCargoVolumeM3,
        nameof(CargoPriority.Urgent));

    private readonly IDeliveryRouteService _inner;
    private readonly IRouteAssignmentValidator _validator;

    public AssignmentValidatingRouteService(IDeliveryRouteService inner, IRouteAssignmentValidator validator)
    {
        _inner = inner;
        _validator = validator;
    }

    public async Task<DeliveryRouteDto> CreateRouteAsync(CreateDeliveryRouteRequest request)
    {
        await _validator.ValidateNewRouteAsync(request.AssignedVehicleId, request.AssignedDriverId, request.CargoItems);
        return await _inner.CreateRouteAsync(request);
    }

    public async Task<DeliveryRouteDto> CreateExpressRouteAsync(CreateExpressRouteRequest request)
    {
        await _validator.ValidateNewRouteAsync(request.AssignedVehicleId, request.AssignedDriverId, new[] { ExpressCargo });
        return await _inner.CreateExpressRouteAsync(request);
    }

    public async Task UpdateRouteStatusAsync(Guid id, string status)
    {
        if (StartsATrip(status))
            await _validator.ValidateStartAsync(id);

        await _inner.UpdateRouteStatusAsync(id, status);
    }

    public Task<IReadOnlyList<DeliveryRouteDto>> GetAllRoutesAsync() => _inner.GetAllRoutesAsync();

    public Task<DeliveryRouteDto?> GetRouteByIdAsync(Guid id) => _inner.GetRouteByIdAsync(id);

    public Task<IReadOnlyList<DeliveryRouteDto>> GetRoutesByDriverAsync(Guid driverId) => _inner.GetRoutesByDriverAsync(driverId);

    public Task<DeliveryRouteDto> DuplicateRouteAsync(Guid routeId, DuplicateRouteRequest request) => _inner.DuplicateRouteAsync(routeId, request);

    // Un texto que no es un estado válido no se valida aquí: el servicio real lo rechaza con su propio mensaje.
    private static bool StartsATrip(string status)
        => Enum.TryParse<DeliveryRouteStatus>(status, true, out var parsed)
           && parsed is DeliveryRouteStatus.InProgress or DeliveryRouteStatus.Delayed;
}
