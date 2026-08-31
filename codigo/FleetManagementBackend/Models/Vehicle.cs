using NetTopologySuite.Geometries; // Librería de mapas

namespace FleetManagementBackend.Models
{
    public class Vehicle
    {
        public string Id { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
        
        // Estado: Disponible, EnRuta, Mantenimiento
        public string Status { get; set; } = string.Empty; 
        
        // Usamos la clase Point para guardar Latitud y Longitud exacta.
        // Esto es fundamental para la integración futura con PostgreSQL + PostGIS
        public Point? Location { get; set; }
        
        public double CapacityTons { get; set; }
    }
}