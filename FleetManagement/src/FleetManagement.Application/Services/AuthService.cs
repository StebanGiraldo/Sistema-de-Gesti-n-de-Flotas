using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;

namespace FleetManagement.Application.Services;

/// <summary>
/// Caso de uso de Autenticación. Depende únicamente de abstracciones
/// (IUserRepository, IDriverRepository, IPasswordHasher, ISessionTokenStore,
/// IFleetAuditLogger); las implementaciones concretas de seguridad viven en
/// FleetManagement.Infrastructure.Security y se inyectan por DI (DIP).
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IDriverRepository _driverRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionTokenStore _sessionStore;
    private readonly IFleetAuditLogger _auditLogger;

    public AuthService(
        IUserRepository userRepository,
        IDriverRepository driverRepository,
        IPasswordHasher passwordHasher,
        ISessionTokenStore sessionStore,
        IFleetAuditLogger auditLogger)
    {
        _userRepository = userRepository;
        _driverRepository = driverRepository;
        _passwordHasher = passwordHasher;
        _sessionStore = sessionStore;
        _auditLogger = auditLogger;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByUsernameAsync(request.Username);
        if (user is null || !user.IsActive)
        {
            _auditLogger.LogEvent("Seguridad", $"Intento de inicio de sesión con usuario inexistente o inactivo: '{request.Username}'.");
            return null;
        }

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            _auditLogger.LogEvent("Seguridad", $"Contraseña incorrecta para el usuario '{request.Username}'.", request.Username);
            return null;
        }

        var token = _sessionStore.CreateSession(user.Id, user.Username, user.Role);
        var driver = await _driverRepository.GetByUserIdAsync(user.Id);

        _auditLogger.LogEvent("Seguridad", $"Inicio de sesión exitoso ({user.Role}).", user.Username);

        return new LoginResponse(token, user.Username, user.FullName, user.Role.ToString(), user.Id, driver?.Id);
    }

    public Task LogoutAsync(string token)
    {
        _sessionStore.RemoveSession(token);
        return Task.CompletedTask;
    }
}
