using System;
using System.Collections.Generic;

namespace FleetManagementBackend.Logging
{
    // La palabra clave 'sealed' evita que otras clases hereden de esta,
    // garantizando estrictamente que sea única.
    public sealed class FleetAuditLogger
    {
        // 1. Instancia estática y Lazy (Perezosa) para que sea 'Thread-Safe' (Segura para hilos múltiples).
        // Esto significa que se crea la instancia solo cuando se solicita por primera vez.
        private static readonly Lazy<FleetAuditLogger> _instance = 
            new Lazy<FleetAuditLogger>(() => new FleetAuditLogger());

        // Una lista en memoria para simular el archivo o base de datos de auditoría
        private readonly List<string> _auditLogs;

        // 2. Constructor PRIVADO. 
        // Esta es la regla de oro del Singleton: nadie fuera de esta clase puede usar "new FleetAuditLogger()".
        private FleetAuditLogger()
        {
            _auditLogs = new List<string>();
            // Registramos el momento exacto en que el Singleton nace.
            LogAction("SISTEMA", "FleetAuditLogger inicializado. Instancia única creada.");
        }

        // 3. Propiedad pública para acceder a la única instancia.
        public static FleetAuditLogger Instance => _instance.Value;

        // Método para registrar eventos en la bitácora
        public void LogAction(string vehicleId, string action)
        {
            string logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] VEHÍCULO: {vehicleId} | ACCIÓN: {action}";
            
            // Bloqueamos la lista temporalmente (lock) para evitar problemas si dos vehículos
            // reportan su ubicación en el mismo microsegundo exacto.
            lock (_auditLogs)
            {
                _auditLogs.Add(logEntry);
                // Imprimimos en la consola del servidor para que lo veas en vivo durante tu demostración
                Console.WriteLine(logEntry);
            }
        }

        // Método para obtener todos los registros (útil para revisar la auditoría)
        public IReadOnlyList<string> GetLogs()
        {
            return _auditLogs.AsReadOnly();
        }
    }
}