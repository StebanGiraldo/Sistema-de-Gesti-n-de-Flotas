using FleetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Persistence;

/// <summary>
/// DbContext de Entity Framework Core, listo para producción con
/// PostgreSQL + PostGIS. En este prototipo NO se registra en el contenedor
/// de Inyección de Dependencias (Program.cs usa los repositorios en memoria
/// de esta misma carpeta); se incluye para mostrar cómo migrar el sistema a
/// una base de datos real sin tocar las capas Domain o Application, gracias
/// a que ambas sólo conocen abstracciones (Principio de Inversión de
/// Dependencias).
///
/// Configuración simplificada con fines ilustrativos: en una implementación
/// real conviene revisar las relaciones (owned types o claves foráneas
/// explícitas para Waypoints/CargoItems) y añadir migraciones.
///
/// PARA ACTIVARLO EN PRODUCCIÓN:
///   1. Confirmar que el proyecto tenga acceso a NuGet y ejecutar "dotnet restore".
///   2. En Program.cs, registrar:
///        builder.Services.AddDbContext&lt;FleetDbContext&gt;(options =&gt;
///            options.UseNpgsql(connectionString, npgsql =&gt; npgsql.UseNetTopologySuite()));
///   3. Crear implementaciones EfVehicleRepository, EfDeliveryRouteRepository, etc.
///      que reciban FleetDbContext por constructor y reemplacen los registros
///      "AddSingleton&lt;IVehicleRepository, InMemoryVehicleRepository&gt;" por
///      "AddScoped&lt;IVehicleRepository, EfVehicleRepository&gt;" (DbContext es Scoped por convención).
///   4. Ejecutar "dotnet ef migrations add InitialCreate" y "dotnet ef database update".
/// </summary>
public class FleetDbContext : DbContext
{
    public FleetDbContext(DbContextOptions<FleetDbContext> options) : base(options)
    {
    }

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<DeliveryRoute> DeliveryRoutes => Set<DeliveryRoute>();
    public DbSet<Waypoint> Waypoints => Set<Waypoint>();
    public DbSet<CargoItem> CargoItems => Set<CargoItem>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<TripAlert> TripAlerts => Set<TripAlert>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.LicensePlate).IsRequired().HasMaxLength(20);
            entity.HasIndex(v => v.LicensePlate).IsUnique();
            entity.Property(v => v.CurrentLocation).HasColumnType("geometry (point, 4326)");
        });

        modelBuilder.Entity<DeliveryRoute>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Origin).HasColumnType("geometry (point, 4326)");
            entity.Property(r => r.Destination).HasColumnType("geometry (point, 4326)");
            entity.HasMany(r => r.Waypoints).WithOne().OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(r => r.CargoItems).WithOne().OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Waypoint>(entity =>
        {
            entity.HasKey(w => w.Id);
            entity.Property(w => w.Location).HasColumnType("geometry (point, 4326)");
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Username).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}
