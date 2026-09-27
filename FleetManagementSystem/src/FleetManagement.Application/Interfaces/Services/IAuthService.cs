using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Interfaces.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task LogoutAsync(string token);
}
