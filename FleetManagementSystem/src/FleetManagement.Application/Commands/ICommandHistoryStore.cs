namespace FleetManagement.Application.Commands;

/// <summary>Dónde vive el historial de cada usuario. Hoy en memoria (<c>Infrastructure</c>); cambiarlo no toca al invocador.</summary>
public interface ICommandHistoryStore
{
    CommandHistory For(string userKey);
}
