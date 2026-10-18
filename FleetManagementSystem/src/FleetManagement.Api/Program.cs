using FleetManagement.Api.Middleware;
using FleetManagement.Application.Builders;
using FleetManagement.Application.Facades;
using FleetManagement.Application.Factories;
using FleetManagement.Application.Flyweights;
using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Reports;
using FleetManagement.Application.Services;
using FleetManagement.Application.Simulation;
using FleetManagement.Infrastructure.BackgroundServices;
using FleetManagement.Infrastructure.Logging;
using FleetManagement.Infrastructure.Navigation;
using FleetManagement.Infrastructure.Notifications;
using FleetManagement.Infrastructure.Persistence;
using FleetManagement.Infrastructure.Security;
using FleetManagement.Infrastructure.Simulation;
using Microsoft.Extensions.Options;
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

// --- ADAPTER (patrón estructural): INavigationProvider (Target) desacopla
//     NavigationService de la API externa concreta. GoogleMapsAdapter
//     traduce nuestra interfaz a la de GoogleMapsApi (Adaptee). Para
//     integrar otro proveedor (Waze, HERE Maps) bastaría con registrar otro
//     adapter aquí, sin tocar NavigationService. ---
builder.Services.AddSingleton<GoogleMapsApi>();
builder.Services.AddSingleton<GoogleMapsAdapter>();

// --- PROXY DE CACHÉ (patrón estructural): INavigationProvider se resuelve ahora a un proxy que envuelve al Adapter
//     (GoogleMapsAdapter se registra como clase concreta para poder entregárselo). Controla el ACCESO al proveedor
//     real —si la misma ruta se pidió hace poco, devuelve la URL guardada— sin que NavigationService, el Adapter ni
//     GoogleMapsApi cambien. Es Singleton: el caché debe vivir más que una petición. Tiempo de vida y tamaño máximo
//     salen de la sección "NavigationCache" de la configuración y se validan al arrancar. ---
builder.Services.AddOptions<NavigationCacheOptions>()
    .Bind(builder.Configuration.GetSection(NavigationCacheOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton(provider => provider.GetRequiredService<IOptions<NavigationCacheOptions>>().Value);
builder.Services.AddSingleton(TimeProvider.System); // reloj inyectable: las pruebas lo reemplazan por uno manual
builder.Services.AddSingleton<INavigationProvider>(provider => new CachingNavigationProviderProxy(
    provider.GetRequiredService<GoogleMapsAdapter>(),
    provider.GetRequiredService<NavigationCacheOptions>(),
    provider.GetRequiredService<TimeProvider>()));

// --- BRIDGE (patrón estructural): INotificationChannel (Implementor) se
//     registra por separado de las notificaciones concretas (la Abstracción,
//     en Application/Notifications, no se registra en DI porque se
//     instancia directamente con "new" dentro de cada servicio que la usa,
//     igual que cualquier otro objeto de dominio/aplicación). Se registran
//     los tres canales concretos; el canal usado por defecto en toda la
//     aplicación es el de correo. Cambiarlo es esta única línea. ---
builder.Services.AddSingleton<EmailNotificationChannel>();
builder.Services.AddSingleton<SmsNotificationChannel>();
builder.Services.AddSingleton<PushNotificationChannel>();
builder.Services.AddSingleton<INotificationChannel, EmailNotificationChannel>();

// --- Servicios de aplicación (casos de uso). Scoped: el valor por defecto
//     recomendado por ASP.NET Core para servicios de negocio por petición. ---
builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<IDriverService, DriverService>();
builder.Services.AddScoped<IDeliveryRouteService, DeliveryRouteService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddScoped<ITripAlertService, TripAlertService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<INavigationService, NavigationService>();

// --- COMPOSITE (patrón estructural): el manifiesto de carga de una ruta se expone como
//     un árbol de CargoGroup/CargoItemComponent (ver FleetManagement.Domain.Cargo). ---
builder.Services.AddScoped<ICargoManifestService, CargoManifestService>();

// --- DECORATOR de reportes (patrón estructural): IFleetReportGenerator es el Componente y se registra SOLO el
//     generador base. Los decoradores (estadísticas, alertas de mantenimiento, resumen de carga) no se registran:
//     FleetReportService los apila en cada petición según las secciones que pida el cliente. Es independiente del
//     Decorator de notificaciones (Application/Notifications/Decorators). ---
builder.Services.AddScoped<IFleetReportGenerator, FleetReportGenerator>();
builder.Services.AddScoped<IFleetReportService, FleetReportService>();

// --- FACADE (patrón estructural): punto de entrada único al resumen del dashboard. Coordina los servicios de
//     vehículos, rutas, mantenimiento y alertas sin reemplazarlos. ---
builder.Services.AddScoped<IFleetDashboardFacade, FleetDashboardFacade>();

// --- FLYWEIGHT (patrón estructural): un único perfil inmutable por tipo de vehículo (velocidad máxima, restricciones de
//     navegación y plantillas de mantenimiento), compartido por todos los que lo consultan: el simulador, el servicio de
//     progreso y la consulta de tipos. La fábrica es Singleton porque ES la tabla de instancias compartidas; con otro
//     ciclo de vida se reconstruirían los perfiles en cada petición y el patrón perdería su razón de ser. ---
builder.Services.AddSingleton<IVehicleTypeProfileFactory, VehicleTypeProfileFactory>();
builder.Services.AddScoped<IVehicleTypeService, VehicleTypeService>();

// --- Simulación de movimiento en tiempo real (monitoreo en tiempo real, requerimiento #1) ---
// Los parámetros salen de la sección "Simulation" de la configuración (appsettings.json o variables de entorno como
// Simulation__TimeAcceleration=10) y se validan al arrancar: un valor imposible, p. ej. aceleración 0, detiene el
// arranque con un mensaje claro en vez de dejar una simulación congelada. Application recibe el objeto ya resuelto
// (sin depender de IOptions); este es el único lugar donde se enlaza la configuración.
builder.Services.AddOptions<SimulationOptions>()
    .Bind(builder.Configuration.GetSection(SimulationOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton(provider => provider.GetRequiredService<IOptions<SimulationOptions>>().Value);

// El avance de los viajes debe sobrevivir entre un paso de la simulación y el siguiente: Singleton. El simulador y el
// servicio de progreso dependen de repositorios y de servicios de aplicación (Scoped), así que son Scoped; el servicio de
// fondo crea un scope por paso.
builder.Services.AddSingleton<ITripProgressTracker, InMemoryTripProgressTracker>();
builder.Services.AddScoped<IVehicleMovementSimulator, VehicleMovementSimulator>();
builder.Services.AddScoped<ITripProgressService, TripProgressService>();
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
