using FleetManagement.Api.Middleware;
using FleetManagement.Application.Builders;
using FleetManagement.Application.Factories;
using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Services;
using FleetManagement.Infrastructure.BackgroundServices;
using FleetManagement.Infrastructure.Logging;
using FleetManagement.Infrastructure.Persistence;
using FleetManagement.Infrastructure.Security;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------
// MVC / Controllers / Swagger
// ---------------------------------------------------------------------
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Fleet Management System API",
        Version = "v1",
        Description =
            "API REST para el Sistema de Gestión de Flotas: monitoreo en tiempo real, " +
            "optimización de rutas y asignación de cargas, mantenimiento predictivo e " +
            "integración con sistemas de navegación."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Token de sesión obtenido en POST /api/auth/login. Formato: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ---------------------------------------------------------------------
// CORS: permite que el frontend estático (servido en otro origen/puerto,
// o abierto como archivo local) consuma la API con fetch().
// ---------------------------------------------------------------------
const string CorsPolicyName = "FleetFrontendPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// ---------------------------------------------------------------------
// INYECCIÓN DE DEPENDENCIAS (Principio de Inversión de Dependencias - DIP)
// Todo se registra contra ABSTRACCIONES (interfaces); las clases concretas
// de Infrastructure son intercambiables sin tocar Application ni Domain.
// ---------------------------------------------------------------------

// --- Repositorios (mock en memoria). Singleton: los datos deben persistir
//     durante toda la vida del proceso, no sólo durante una petición HTTP. ---
builder.Services.AddSingleton<IVehicleRepository, InMemoryVehicleRepository>();
builder.Services.AddSingleton<IDriverRepository, InMemoryDriverRepository>();
builder.Services.AddSingleton<IDeliveryRouteRepository, InMemoryDeliveryRouteRepository>();
builder.Services.AddSingleton<IMaintenanceRepository, InMemoryMaintenanceRepository>();
builder.Services.AddSingleton<ITripAlertRepository, InMemoryTripAlertRepository>();
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();

// --- Seguridad ---
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<ISessionTokenStore, InMemorySessionTokenStore>();

// --- SINGLETON (patrón GoF clásico): se expone la MISMA instancia estática
//     FleetAuditLogger.Instance a través de la interfaz IFleetAuditLogger,
//     reconciliando el patrón clásico con el ciclo de vida del contenedor. ---
builder.Services.AddSingleton<IFleetAuditLogger>(_ => FleetAuditLogger.Instance);

// --- FACTORY METHOD y ABSTRACT FACTORY: proveedores resueltos por tipo de vehículo. ---
builder.Services.AddSingleton<VehicleFactoryProvider>();
builder.Services.AddSingleton<FleetOnboardingFactoryProvider>();

// --- BUILDER: una instancia nueva por cada consumidor (Transient es semánticamente
//     correcto para un objeto de construcción de un solo uso). ---
builder.Services.AddTransient<IDeliveryRouteBuilder, DeliveryRouteBuilder>();
builder.Services.AddSingleton<DeliveryRouteDirector>();

// --- Servicios de aplicación (casos de uso). Scoped: el valor por defecto
//     recomendado por ASP.NET Core para servicios de negocio por petición. ---
builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<IDriverService, DriverService>();
builder.Services.AddScoped<IDeliveryRouteService, DeliveryRouteService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddScoped<ITripAlertService, TripAlertService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<INavigationService, NavigationService>();

// --- Simulación de movimiento en tiempo real (monitoreo en tiempo real, requerimiento #1) ---
builder.Services.AddHostedService<VehicleSimulationBackgroundService>();

var app = builder.Build();

// ---------------------------------------------------------------------
// Datos de demostración (seed) para que el prototipo funcione de inmediato
// ---------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    await InMemoryDataSeeder.SeedAsync(scope.ServiceProvider);
}

// ---------------------------------------------------------------------
// Pipeline HTTP
// ---------------------------------------------------------------------
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Fleet Management System API v1");
});

app.UseCors(CorsPolicyName);
app.UseMiddleware<TokenAuthenticationMiddleware>();
app.MapControllers();

app.Run();
