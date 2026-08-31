using System.Collections.Generic;
using System.Linq;
using FleetManagementBackend.Models;
using FleetManagementBackend.Logging;
using NetTopologySuite.Geometries;

namespace FleetManagementBackend.Repositories
{
    public class VehicleRepositoryMock : IVehicleRepository
    {
        private static readonly List<Vehicle> _vehicles = new List<Vehicle>
        {
            new Vehicle { Id = "V-001", LicensePlate = "BGA-123", Status = "Disponible", CapacityTons = 5.0, Location = new Point(-73.1198, 7.1139) }, // Bucaramanga
            new Vehicle { Id = "V-002", LicensePlate = "BOG-789", Status = "En Ruta", CapacityTons = 12.0, Location = new Point(-74.0721, 4.7110) }, // Bogotá
            new Vehicle { Id = "V-003", LicensePlate = "MED-456", Status = "Mantenimiento", CapacityTons = 3.5, Location = new Point(-75.5658, 6.2518) }, // Medellín
            new Vehicle { Id = "V-004", LicensePlate = "CLO-321", Status = "Disponible", CapacityTons = 8.0, Location = new Point(-76.5320, 3.4516) }, // Cali
            new Vehicle { Id = "V-005", LicensePlate = "CTG-987", Status = "En Ruta", CapacityTons = 15.0, Location = new Point(-75.5144, 10.3910) }, // Cartagena
            new Vehicle { Id = "V-006", LicensePlate = "BAQ-654", Status = "Mantenimiento", CapacityTons = 6.0, Location = new Point(-74.7813, 10.9685) }, // Barranquilla
            new Vehicle { Id = "V-007", LicensePlate = "CUC-111", Status = "Disponible", CapacityTons = 4.0, Location = new Point(-72.5078, 7.8939) } // Cúcuta
        };

        public IEnumerable<Vehicle> GetAllVehicles()
        {
            FleetAuditLogger.Instance.LogAction("SISTEMA", "Consulta masiva de ubicación de vehículos.");
            return _vehicles;
        }

        public Vehicle? GetVehicleById(string id)
        {
            FleetAuditLogger.Instance.LogAction(id, "Consulta específica de estado.");
            return _vehicles.FirstOrDefault(v => v.Id == id);
        }

        public void UpdateVehicleLocation(string id, double newLat, double newLng, string newStatus)
        {
            var vehicle = _vehicles.FirstOrDefault(v => v.Id == id);
            if (vehicle != null)
            {
                // Si el vehículo está en mantenimiento, no lo movemos para realismo
                if (vehicle.Status != "Mantenimiento")
                {
                    vehicle.Location = new Point(newLng, newLat); // NTS usa (Longitud, Latitud)
                    vehicle.Status = newStatus;
                    FleetAuditLogger.Instance.LogAction(id, $"Vehículo en movimiento/actualizado a Estado: {newStatus} en [{newLat}, {newLng}]");
                }
                else
                {
                    FleetAuditLogger.Instance.LogAction(id, "Intento de movimiento bloqueado: El vehículo está en Mantenimiento.");
                }
            }
        }
    }
}