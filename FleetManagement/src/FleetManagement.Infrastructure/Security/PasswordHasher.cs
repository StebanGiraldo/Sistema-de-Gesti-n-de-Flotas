using System.Security.Cryptography;
using FleetManagement.Application.Interfaces;

namespace FleetManagement.Infrastructure.Security;

/// <summary>
/// Implementación de hashing de contraseñas con PBKDF2 (Rfc2898DeriveBytes),
/// usando sólo tipos incluidos en el runtime de .NET (System.Security.Cryptography),
/// sin dependencias externas adicionales. Cada contraseña se combina con una
/// sal aleatoria distinta por usuario y se compara en tiempo constante para
/// mitigar ataques de temporización.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int KeySizeBytes = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public (string Hash, string Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySizeBytes);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool VerifyPassword(string password, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var expectedHash = Convert.FromBase64String(hash);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, Algorithm, KeySizeBytes);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
