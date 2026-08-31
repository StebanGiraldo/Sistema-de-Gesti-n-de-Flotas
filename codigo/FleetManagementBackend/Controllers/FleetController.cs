using Microsoft.AspNetCore.Mvc;
using FleetManagementBackend.Repositories;
using System.Linq;

namespace FleetManagementBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FleetController : ControllerBase
    {
        private readonly IVehicleRepository _repository;

        public FleetController(IVehicleRepository repository)
        {
            _repository = repository;
        }

        [HttpGet("locations")]
        public IActionResult GetFleetLocations()
        {
            var vehicles = _repository.GetAllVehicles();
            var response = vehicles.Select(v => new
            {
                id = v.Id,
                licensePlate = v.LicensePlate,
                status = v.Status,
                capacityTons = v.CapacityTons,
                latitude = v.Location?.Y,
                longitude = v.Location?.X
            });

            return Ok(response);
        }

        [HttpPost("simulate-movement")]
        public IActionResult SimulateMovement()
        {
            // Movimiento más fluido con pasos más cortos
            var v1 = _repository.GetVehicleById("V-001");
            if (v1 != null && v1.Status != "Mantenimiento")
            {
                double newLat = (v1.Location?.Y ?? 7.1139) + 0.002;
                double newLng = (v1.Location?.X ?? -73.1198) + 0.002;
                _repository.UpdateVehicleLocation("V-001", newLat, newLng, "En Ruta");
            }

            var v2 = _repository.GetVehicleById("V-002");
            if (v2 != null && v2.Status != "Mantenimiento")
            {
                double newLat = (v2.Location?.Y ?? 4.7110) - 0.002;
                double newLng = (v2.Location?.X ?? -74.0721) - 0.002;
                _repository.UpdateVehicleLocation("V-002", newLat, newLng, "En Ruta");
            }

            var v5 = _repository.GetVehicleById("V-005");
            if (v5 != null && v5.Status != "Mantenimiento")
            {
                double newLat = (v5.Location?.Y ?? 10.3910) + 0.002;
                double newLng = (v5.Location?.X ?? -75.5144) - 0.002;
                _repository.UpdateVehicleLocation("V-005", newLat, newLng, "En Ruta");
            }

            return Ok(new { message = "Telemetría fluida aplicada." });
        }
    }
}