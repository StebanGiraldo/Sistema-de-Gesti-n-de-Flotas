using Microsoft.AspNetCore.Mvc;
using FleetManagementBackend.Models;
using FleetManagementBackend.Services;
using FleetManagementBackend.Logging;

namespace FleetManagementBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        // Inyección de dependencias para cumplir con SOLID
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            bool isValid = _authService.ValidateUser(request.Username, request.Password);

            if (isValid)
            {
                // Registramos el acceso exitoso usando nuestro Patrón Singleton
                FleetAuditLogger.Instance.LogAction(request.Username, "Inicio de sesión exitoso al sistema.");
                return Ok(new { success = true, message = "Autenticación correcta" });
            }

            // Registramos el fallo de seguridad en el Singleton
            FleetAuditLogger.Instance.LogAction(request.Username, "Intento fallido de inicio de sesión (Credenciales inválidas).");
            return Unauthorized(new { success = false, message = "Usuario o contraseña incorrectos" });
        }
    }
}