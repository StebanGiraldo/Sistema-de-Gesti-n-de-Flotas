using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces;

namespace FleetManagement.Application.Commands;

public sealed class CommandInvoker : ICommandInvoker
{
    private const string AuditCategory = "Comando";

    private readonly ICommandHistoryStore _store;
    private readonly IFleetAuditLogger _audit;
    private readonly TimeProvider _clock;

    public CommandInvoker(ICommandHistoryStore store, IFleetAuditLogger audit, TimeProvider clock)
    {
        _store = store;
        _audit = audit;
        _clock = clock;
    }

    public Task<TResult> ExecuteAsync<TResult>(string user, IUndoableCommand<TResult> command)
    {
        var history = _store.For(user);
        return history.RunExclusiveAsync(async () =>
        {
            var result = await command.ExecuteAsync();
            if (command.HasEffect)
            {
                history.Record(command, _clock.GetUtcNow().UtcDateTime);
                _audit.LogEvent(AuditCategory, $"Ejecutó: {command.Description}.", user);
            }

            return result;
        });
    }

    public Task<CommandStepDto> UndoAsync(string user)
    {
        var history = _store.For(user);
        return history.RunExclusiveAsync(async () =>
        {
            var entry = history.NextToUndo ?? throw new InvalidOperationException("No hay nada que deshacer.");
            try
            {
                await entry.Action.UndoAsync();
            }
            catch (CommandNotUndoableException ex)
            {
                history.DiscardNextToUndo();
                _audit.LogEvent(AuditCategory, $"No se pudo deshacer: {entry.Action.Description}. {ex.Message} Se quitó del historial.", user);
                throw;
            }

            history.MoveNextUndoToRedo();
            _audit.LogEvent(AuditCategory, $"Deshizo: {entry.Action.Description}.", user);
            return new CommandStepDto(entry.Action.Description, ToDto(history));
        });
    }

    public Task<CommandStepDto> RedoAsync(string user)
    {
        var history = _store.For(user);
        return history.RunExclusiveAsync(async () =>
        {
            var entry = history.NextToRedo ?? throw new InvalidOperationException("No hay nada que rehacer.");
            await entry.Action.RedoAsync();

            // Si al repetirla ya no cambió nada (otro la hizo antes), no hay nada que deshacer después.
            if (entry.Action.HasEffect)
                history.MoveNextRedoToUndo();
            else
                history.DiscardNextToRedo();

            _audit.LogEvent(AuditCategory, $"Rehízo: {entry.Action.Description}.", user);
            return new CommandStepDto(entry.Action.Description, ToDto(history));
        });
    }

    public Task<CommandHistoryDto> GetHistoryAsync(string user)
    {
        var history = _store.For(user);
        return history.RunExclusiveAsync(() => Task.FromResult(ToDto(history)));
    }

    private static CommandHistoryDto ToDto(CommandHistory history) => new(
        history.Undoable.Select(ToDto).ToList(),
        history.Redoable.Select(ToDto).ToList());

    private static CommandEntryDto ToDto(HistoryEntry entry) => new(entry.Action.Description, entry.At);
}
