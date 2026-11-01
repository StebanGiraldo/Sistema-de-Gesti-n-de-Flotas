namespace FleetManagement.Application.Commands;

/// <summary>Una orden que además se puede deshacer y rehacer: es lo único que acepta el <see cref="ICommandInvoker"/>.</summary>
public interface IUndoableCommand<TResult> : ICommand<TResult>, IUndoableAction
{
}
