using FleetManagementBackend.Repositories;
using System.Text.Json.Serialization;
using FleetManagementBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Agregamos soporte para controladores API
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
         // Esto ayuda a manejar de forma limpia la conversión de datos (especialmente mapas) a formato JSON
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        // Solución para permitir valores flotantes y especiales de NetTopologySuite (mapas)
        options.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals;
    });

// 2. PRINCIPIO SOLID (Inversión de Dependencias):
// Registramos nuestro Repositorio Simulado en el contenedor de dependencias de .NET.
// Usamos "AddSingleton" o "AddScoped" para que la aplicación sepa qué clase usar cuando se la pidan.
builder.Services.AddScoped<IVehicleRepository, VehicleRepositoryMock>();

// 3. Configuramos CORS para permitir que cualquier página web (o archivo HTML local) hable con esta API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
// 4. Registramos el servicio de autenticación

builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

// 4. Activamos la política de CORS que acabamos de crear
app.UseCors("AllowAll");

app.UseAuthorization();

// Mapeamos los controladores para que las rutas funcionen (ej: /api/fleet/locations)
app.MapControllers();

// 5. Mensaje de bienvenida en consola para saber que el servidor arrancó
System.Console.WriteLine("==================================================");
System.Console.WriteLine("Servidor backend de Gestión de Flotas iniciado.");
System.Console.WriteLine("==================================================");

app.Run();