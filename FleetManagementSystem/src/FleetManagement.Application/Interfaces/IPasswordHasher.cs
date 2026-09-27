namespace FleetManagement.Application.Interfaces;

/// <summary>Abstracción de hashing de contraseñas (implementación PBKDF2 en Infrastructure.Security).</summary>
public interface IPasswordHasher
{
    (string Hash, string Salt) HashPassword(string password);
    bool VerifyPassword(string password, string hash, string salt);
}
