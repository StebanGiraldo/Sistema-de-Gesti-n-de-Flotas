using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Commands;

/// <summary>
/// Base de todas las órdenes que CREAN una ruta (por el formulario, express o duplicando otra) — patrón Template Method.
/// Lo que es igual en todas vive aquí, una sola vez: recordar qué ruta se creó, saber si la orden tuvo efecto, deshacer
/// (ELIMINAR esa ruta mientras siga planificada) y rehacer (crear otra ruta NUEVA con los mismos datos). Como deshacer
/// elimina y no cancela, deshacer y rehacer una y otra vez nunca dejan más de una ruta: no se acumulan rutas canceladas.
/// Lo único que cambia de una orden a otra es CÓMO se crea la ruta (<see cref="CreateAsync"/>) y cómo se describe
/// (<see cref="Description"/>).
/// Una forma nueva de crear rutas se agrega con una subclase pequeña, sin tocar esta clase, el invocador ni el historial.
/// </summary>
public abstract class RouteCreationCommand : IUndoableCommand<DeliveryRouteDto>
{
    private readonly IPlannedRouteDeleter _deleter;
    private Guid? _createdRouteId;

    protected RouteCreationCommand(IPlannedRouteDeleter deleter)
    {
        _deleter = deleter;
    }

    public abstract string Description { get; }

    public bool HasEffect => _createdRouteId is not null;

    public async Task<DeliveryRouteDto> ExecuteAsync()
    {
        var created = await CreateAsync();
        _createdRouteId = created.Id;
        return created;
    }

    public Task UndoAsync() => _deleter.DeleteAsync(new[] { _createdRouteId ?? throw new InvalidOperationException("La orden todavía no se ejecutó.") });

    public virtual Task RedoAsync() => ExecuteAsync();

    /// <summary>Crea la ruta por el servicio de siempre (con todas sus reglas) y la devuelve.</summary>
    protected abstract Task<DeliveryRouteDto> CreateAsync();
}
