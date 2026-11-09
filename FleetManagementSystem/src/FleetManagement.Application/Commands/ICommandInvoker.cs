using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Commands;

/// <summary>
/// PATRÓN COMMAND — el invocador: ejecuta las órdenes, las guarda en el historial de quien las pidió y las deshace o rehace.
/// No sabe qué hace cada orden.
/// </summary>
public interface ICommandInvoker
{
    /// <summary>Ejecuta la orden. Si falla, no queda nada en el historial; si no cambió nada, tampoco.</summary>
    Task<TResult> ExecuteAsync<TResult>(string user, IUndoableCommand<TResult> command);

    /// <summary>Deshace la última operación del usuario. Si ya no se puede (<see cref="CommandNotUndoableException"/>), la quita del historial.</summary>
    Task<CommandStepDto> UndoAsync(string user);

    /// <summary>Rehace la última operación deshecha. Si falla de forma pasajera, queda disponible para reintentar; si ya no es posible (<see cref="CommandNotRedoableException"/>), la quita del historial.</summary>
    Task<CommandStepDto> RedoAsync(string user);

    Task<CommandHistoryDto> GetHistoryAsync(string user);
}
