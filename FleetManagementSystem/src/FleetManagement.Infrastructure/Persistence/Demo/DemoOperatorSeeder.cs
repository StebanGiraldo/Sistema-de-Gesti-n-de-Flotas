using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Infrastructure.Persistence.Demo;

/// <summary>Da de alta las cuentas de operador de demostración y, para cada una, su conductor (el portal busca al conductor por usuario).</summary>
public sealed class DemoOperatorSeeder
{
    private readonly IUserRepository _users;
    private readonly IDriverRepository _drivers;
    private readonly IPasswordHasher _hasher;

    public DemoOperatorSeeder(IUserRepository users, IDriverRepository drivers, IPasswordHasher hasher)
    {
        _users = users;
        _drivers = drivers;
        _hasher = hasher;
    }

    public async Task SeedAsync(IEnumerable<DemoOperator> operators)
    {
        foreach (var spec in operators)
            await SeedOneAsync(spec);
    }

    private async Task SeedOneAsync(DemoOperator spec)
    {
        var (hash, salt) = _hasher.HashPassword(DemoOperators.Password);
        var user = await _users.AddAsync(new AppUser
        {
            Username = spec.Username,
            FullName = spec.FullName,
            Role = UserRole.Operator,
            PasswordHash = hash,
            PasswordSalt = salt
        });
        await _drivers.AddAsync(new Driver { FullName = spec.FullName, LicenseNumber = spec.LicenseNumber, Phone = spec.Phone, UserId = user.Id });
    }
}
