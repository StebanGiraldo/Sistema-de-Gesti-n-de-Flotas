using FleetManagement.Domain.Enums;

namespace FleetManagement.Domain.Entities;

/// <summary>
/// Cuenta de acceso al sistema. Se llama "AppUser" (y no "User") para evitar
/// ambigüedades con convenciones de ASP.NET Core Identity y con
/// ClaimsPrincipal.
/// </summary>
public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
}
