using System.Collections.Generic;
using FleetManagementBackend.Models;

namespace FleetManagementBackend.Repositories
{
    public interface IVehicleRepository
    {
        IEnumerable<Vehicle> GetAllVehicles();
        Vehicle? GetVehicleById(string id);
        void UpdateVehicleLocation(string id, double newLat, double newLng, string newStatus);
    }
}