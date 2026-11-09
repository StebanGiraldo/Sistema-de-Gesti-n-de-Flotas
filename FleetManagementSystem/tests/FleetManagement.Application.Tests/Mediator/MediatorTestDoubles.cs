using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Mediators;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Persistence;

namespace FleetManagement.Application.Tests.Mediator;

/// <summary>
/// Participante de vehículos de prueba (doble escrito a mano): no toca ningún repositorio, solo anota lo que el mediador
/// le pide. Sirve para probar al mediador solo, sin que el resultado dependa de cómo guarda el vehículo el participante real.
/// </summary>
public sealed class RecordingVehicleColleague : IVehicleColleague
{
    public record Call(string Kind, Guid VehicleId, bool? HasOtherActiveTrip = null);

    private readonly List<Call> _calls = new();

    public IReadOnlyList<Call> Calls => _calls;

    public Task MarkEnRouteAsync(Guid vehicleId)
    {
        _calls.Add(new Call("MarkEnRoute", vehicleId));
        return Task.CompletedTask;
    }

    public Task ReleaseAfterTripAsync(Guid vehicleId, bool hasOtherActiveTrip)
    {
        _calls.Add(new Call("Release", vehicleId, hasOtherActiveTrip));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Participante de rutas de prueba que hace de "remitente" ante el mediador: contesta lo que éste le pregunta con la
/// respuesta que fije la prueba y anota cada pregunta. No guarda nada (cambiar el estado no está soportado a propósito:
/// si el mediador intentara hacerlo, la prueba fallaría).
/// </summary>
public sealed class StubRouteColleague : IRouteColleague
{
    private readonly List<(Guid VehicleId, Guid ExceptRouteId)> _questions = new();

    public bool AnswerToHasOtherActiveTrip { get; set; }

    public IReadOnlyList<(Guid VehicleId, Guid ExceptRouteId)> Questions => _questions;

    public Task ChangeStatusAsync(DeliveryRoute route, DeliveryRouteStatus newStatus) => throw new NotSupportedException();

    public Task ReportDelayAsync(DeliveryRoute route, int delayMinutes) => throw new NotSupportedException();

    public Task<bool> HasOtherActiveTripAsync(Guid vehicleId, Guid exceptRouteId)
    {
        _questions.Add((vehicleId, exceptRouteId));
        return Task.FromResult(AnswerToHasOtherActiveTrip);
    }
}

/// <summary>
/// Mediador de prueba: anota los avisos que le llegan de un participante de rutas (quién avisó, de qué ruta y cómo estaba
/// la ruta en ese momento). Con una bitácora compartida permite comprobar el ORDEN en que ocurren las cosas.
/// </summary>
public sealed class RecordingMediator : IFleetLifecycleMediator
{
    public record Notice(string Kind, IRouteColleague Sender, DeliveryRoute Route, DeliveryRouteStatus StatusAtTheTime, int DelayAtTheTime);

    private readonly List<Notice> _notices = new();
    private readonly List<string>? _sharedLog;

    public RecordingMediator(List<string>? sharedLog = null)
    {
        _sharedLog = sharedLog;
    }

    public IReadOnlyList<Notice> Notices => _notices;

    public Task RouteStatusChangedAsync(IRouteColleague sender, DeliveryRoute route) => Record("StatusChanged", sender, route);

    public Task RouteDelayReportedAsync(IRouteColleague sender, DeliveryRoute route) => Record("DelayReported", sender, route);

    private Task Record(string kind, IRouteColleague sender, DeliveryRoute route)
    {
        _notices.Add(new Notice(kind, sender, route, route.Status, route.DelayMinutes));
        _sharedLog?.Add($"notice:{kind}");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Repositorio de rutas (en memoria) que anota cada escritura en una bitácora compartida con <see cref="RecordingMediator"/>:
/// así una prueba puede afirmar "primero se guardó la ruta y después se avisó al mediador", cosa que mirar el repositorio
/// no permite porque el repositorio en memoria guarda la misma instancia.
/// </summary>
public sealed class LoggingRouteRepository : IDeliveryRouteRepository
{
    private readonly InMemoryDeliveryRouteRepository _inner = new();
    private readonly List<string> _log;

    public LoggingRouteRepository(List<string> log)
    {
        _log = log;
    }

    public Task<IReadOnlyList<DeliveryRoute>> GetAllAsync() => _inner.GetAllAsync();

    public Task<DeliveryRoute?> GetByIdAsync(Guid id) => _inner.GetByIdAsync(id);

    public Task<IReadOnlyList<DeliveryRoute>> GetByDriverIdAsync(Guid driverId) => _inner.GetByDriverIdAsync(driverId);

    public Task<DeliveryRoute> AddAsync(DeliveryRoute route) => _inner.AddAsync(route);

    public Task DeleteAsync(Guid id) => _inner.DeleteAsync(id);

    public Task UpdateAsync(DeliveryRoute route)
    {
        _log.Add($"saved:{route.Status}");
        return _inner.UpdateAsync(route);
    }
}
