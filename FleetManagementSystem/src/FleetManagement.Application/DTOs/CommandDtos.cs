namespace FleetManagement.Application.DTOs;

/// <summary>Una operación del historial de deshacer/rehacer.</summary>
public record CommandEntryDto(string Description, DateTime ExecutedAt);

/// <summary>Historial de un usuario. Las dos listas van de la más reciente a la más antigua: la primera es la que se deshace (o rehace) a continuación.</summary>
public record CommandHistoryDto(IReadOnlyList<CommandEntryDto> Undoable, IReadOnlyList<CommandEntryDto> Redoable);

/// <summary>Resultado de deshacer o rehacer: qué operación fue y cómo quedó el historial.</summary>
public record CommandStepDto(string Description, CommandHistoryDto History);
