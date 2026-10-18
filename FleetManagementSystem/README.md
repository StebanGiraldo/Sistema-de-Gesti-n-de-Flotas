# 🚚 FleetControl — Sistema de Gestión de Flotas

Prototipo full-stack de un Sistema de Gestión de Flotas: monitoreo en tiempo real,
optimización de rutas y asignación de cargas, mantenimiento predictivo e integración
con sistemas de navegación. Incluye autenticación con dos roles (Administrador /
Operador), un dashboard administrativo y un portal para conductores.

> Construido como ejercicio de **Clean Architecture**, **SOLID** y **patrones de
> diseño creacionales** (Singleton, Factory Method, Abstract Factory, Builder y
> Prototype) sobre **C# / .NET 8**, con un frontend en HTML + CSS + JavaScript
> puro (sin frameworks) y mapas con **Leaflet.js**.

---

## Tabla de contenidos

1. [Descripción general y módulos funcionales](#1-descripción-general-y-módulos-funcionales)
2. [Stack tecnológico](#2-stack-tecnológico)
3. [Arquitectura (Clean Architecture)](#3-arquitectura-clean-architecture)
4. [Estructura de carpetas](#4-estructura-de-carpetas)
5. [Cómo ejecutar el proyecto](#5-cómo-ejecutar-el-proyecto)
6. [Credenciales de demostración](#6-credenciales-de-demostración)
7. [Principios SOLID aplicados](#7-principios-solid-aplicados)
8. [Patrones de diseño](#8-patrones-de-diseño)
   - [8.1 Singleton — `FleetAuditLogger`](#81-singleton--fleetauditlogger)
   - [8.2 Factory Method — Fábricas de vehículos](#82-factory-method--fábricas-de-vehículos)
   - [8.3 Abstract Factory — Fábrica de incorporación de flota](#83-abstract-factory--fábrica-de-incorporación-de-flota)
   - [8.4 Builder — Constructor de rutas](#84-builder--constructor-de-rutas)
   - [8.5 Prototype — Clonación de vehículos y rutas](#85-prototype--clonación-de-vehículos-y-rutas)
   - [8.6 Resumen de ubicaciones](#86-resumen-de-ubicaciones-de-los-patrones)
   - [8.7 Patrones estructurales: Adapter, Bridge, Composite y Decorator](#87-patrones-estructurales-adapter-bridge-composite-y-decorator)
   - [8.8 Decorator de reportes y Facade del resumen](#88-decorator-de-reportes-y-facade-del-resumen)
   - [8.9 Flyweight — perfiles compartidos por tipo de vehículo](#89-flyweight--perfiles-compartidos-por-tipo-de-vehículo)
   - [8.10 Proxy — caché del proveedor de navegación](#810-proxy--caché-del-proveedor-de-navegación)
9. [Documentación de la API REST](#9-documentación-de-la-api-rest)
10. [Notas sobre paso a producción](#10-notas-sobre-paso-a-producción)
11. [Limitaciones conocidas del prototipo](#11-limitaciones-conocidas-del-prototipo)
12. [Pruebas automatizadas y cómo demostrar el Factory Method](#12-pruebas-automatizadas-y-cómo-demostrar-el-factory-method)

---

## 1. Descripción general y módulos funcionales

| # | Módulo | Requerimiento | Dónde vive |
|---|--------|----------------|------------|
| 1 | **Monitoreo en tiempo real** | Ver ubicación y estado de cada vehículo (disponible / en ruta / mantenimiento / fuera de servicio) y su avance sobre la ruta asignada | `FleetController` + `TripProgressController` + `VehicleSimulationBackgroundService` + pestaña "Mapa y vehículos" del dashboard |
| 2 | **Optimización de rutas y asignación de cargas** | Crear rutas con paradas y artículos de carga, asignar vehículo/conductor, estimar distancia/tiempo | `DeliveryRoutesController` + `DeliveryRouteService` (patrón **Builder**) + pestaña "Rutas y cargas" |
| 3 | **Mantenimiento predictivo** | Registrar mantenimientos y detectar cuáles vehículos tienen tareas vencidas por fecha o kilometraje | `MaintenanceController` + `MaintenanceService.GetVehiclesDueForMaintenanceAsync()` + pestaña "Mantenimiento" |
| 4 | **Integración con sistemas de navegación** | El conductor puede abrir la ruta asignada en una app de navegación real | `NavigationController` + `NavigationService` (genera enlace de Google Maps) + botón "Abrir navegación" en el portal del operador |

Además, el sistema incluye:

- **Autenticación con roles** (`AuthController`, `AuthService`) — Administrador y Operador.
- **Dashboard administrativo** de una sola página (SPA) con mapa, gestión de vehículos, rutas, mantenimiento, alertas y auditoría.
- **Portal del operador**: el conductor ve su ruta asignada, la carga a transportar, puede reportar una complicación durante el viaje (avería, retraso, tráfico, accidente, clima) y abrir la navegación externa.

---

## 2. Stack tecnológico

| Capa | Tecnología |
|------|------------|
| Backend | C# / **.NET 8**, ASP.NET Core Web API |
| "Base de datos" del prototipo | Colecciones en memoria (`ConcurrentDictionary`), thread-safe |
| Preparación para producción | `NetTopologySuite.Geometries.Point` para coordenadas + `FleetDbContext` (EF Core) listo para **PostgreSQL + PostGIS** |
| Frontend | HTML5, CSS3 (sistema de diseño propio, sin framework), JavaScript puro (Vanilla JS, `fetch`) |
| Mapas | **Leaflet.js** + teselas de OpenStreetMap |
| Documentación de API | Swagger / OpenAPI (`Swashbuckle.AspNetCore`) |

---

## 3. Arquitectura (Clean Architecture)

```
                ┌─────────────────────────────┐
                │   FleetManagement.Api       │  Controladores REST, Program.cs (DI),
                │   (ASP.NET Core Web API)    │  Middleware, Filtros
                └───────────────┬─────────────┘
                                │ depende de
                ┌───────────────▼─────────────┐
                │ FleetManagement.Infrastructure│ Repositorios en memoria, Singleton
                │                              │ (FleetAuditLogger), seguridad,
                │                              │ BackgroundService, FleetDbContext
                └───────────────┬─────────────┘
                                │ depende de
                ┌───────────────▼─────────────┐
                │ FleetManagement.Application  │ Casos de uso (Services), DTOs,
                │                              │ interfaces (repos/servicios),
                │                              │ Factories, Builders
                └───────────────┬─────────────┘
                                │ depende de
                ┌───────────────▼─────────────┐
                │   FleetManagement.Domain     │ Entidades, enums, IPrototype<T>
                │   (sin dependencias externas)│ (núcleo, no depende de nada más)
                └─────────────────────────────┘
```

La regla de dependencia se respeta en todo el proyecto: las flechas de dependencia
siempre apuntan hacia adentro (hacia `Domain`). `Domain` no conoce a `Application`;
`Application` no conoce a `Infrastructure` ni a `Api`. Esto es lo que permite, por
ejemplo, sustituir los repositorios en memoria por Entity Framework Core sin tocar
una sola línea de los casos de uso (ver [sección 10](#10-notas-sobre-paso-a-producción)).

---

## 4. Estructura de carpetas

```
FleetManagementSystem/
├── FleetManagementSystem.sln
├── README.md
├── src/
│   ├── FleetManagement.Domain/
│   │   ├── Common/IPrototype.cs                     ← contrato del patrón Prototype
│   │   ├── Entities/ (Vehicle, DeliveryRoute, Driver, AppUser, Waypoint,
│   │   │              CargoItem, MaintenanceRecord, MaintenancePlan,
│   │   │              NavigationProfile, TripAlert)
│   │   └── Enums/Enums.cs
│   │
│   ├── FleetManagement.Application/
│   │   ├── Interfaces/Repositories/ (6 interfaces — DIP)
│   │   ├── Interfaces/Services/ (7 interfaces — DIP)
│   │   ├── Interfaces/ (IFleetAuditLogger, IPasswordHasher, ISessionTokenStore)
│   │   ├── DTOs/ (records de entrada/salida de la API)
│   │   ├── Factories/  ← FACTORY METHOD + ABSTRACT FACTORY
│   │   ├── Builders/   ← BUILDER
│   │   ├── Reports/    ← DECORATOR de reportes (generador base + decoradores, sección 8.8)
│   │   ├── Facades/    ← FACADE del resumen del dashboard (sección 8.8)
│   │   ├── Metrics/    ← FleetMetrics: cálculos que comparten el reporte y el resumen
│   │   ├── Geo/        ← GeoMath: distancias (Haversine) e interpolación, compartidas por rutas y simulación
│   │   ├── Simulation/ ← simulación del recorrido sobre la ruta asignada (secciones 5.3 y 10)
│   │   ├── Flyweights/ ← FLYWEIGHT: perfil compartido por tipo de vehículo (sección 8.9)
│   │   └── Services/   (servicios: casos de uso)
│   │
│   ├── FleetManagement.Infrastructure/
│   │   ├── Logging/FleetAuditLogger.cs  ← SINGLETON
│   │   ├── Persistence/ (6 repositorios en memoria + InMemoryDataSeeder + FleetDbContext)
│   │   ├── Security/ (PasswordHasher, InMemorySessionTokenStore)
│   │   ├── Navigation/ ← GoogleMapsAdapter (Adapter) + CachingNavigationProviderProxy y su caché (PROXY, sección 8.10)
│   │   ├── Simulation/ (InMemoryTripProgressTracker: avance de los viajes, en memoria)
│   │   └── BackgroundServices/VehicleSimulationBackgroundService.cs  (temporizador de la simulación)
│   │
│   └── FleetManagement.Api/
│       ├── Program.cs                 ← Composition Root (Inyección de Dependencias)
│       ├── Controllers/ (13 controladores REST)
│       ├── Middleware/TokenAuthenticationMiddleware.cs
│       └── Filters/RequireRoleAttribute.cs
│
├── tests/
│   └── FleetManagement.Application.Tests/     ← pruebas xUnit del Factory Method (sección 12)
│       └── Factories/ (VehicleFactoryTests, VehicleFactoryProviderTests,
│                       VehicleServiceFactoryMethodTests)
│
└── frontend/
    ├── login.html
    ├── dashboard.html                 (panel administrativo — SPA)
    ├── operator.html                  (portal del conductor)
    ├── css/styles.css
    └── js/ (api.js, auth.js, dashboard.js, operator.js)
```

---

## 5. Cómo ejecutar el proyecto

### 5.1 Backend

Requiere el **.NET 8 SDK** instalado y acceso a internet la primera vez (para que
NuGet descargue `NetTopologySuite`, `Microsoft.EntityFrameworkCore`,
`Npgsql.EntityFrameworkCore.PostgreSQL` y `Swashbuckle.AspNetCore`).

```bash
cd FleetManagementSystem

# Restaura los paquetes NuGet de los 4 proyectos
dotnet restore

# Ejecuta la API (por defecto en http://localhost:5080)
dotnet run --project src/FleetManagement.Api
```

Al iniciar, la aplicación:
1. Registra todas las dependencias (ver `Program.cs`).
2. Carga datos de demostración (`InMemoryDataSeeder`) con usuarios, vehículos, rutas
   y mantenimientos de ejemplo, usando coordenadas de Santander y otras ciudades de Colombia.
3. Expone Swagger en **http://localhost:5080/swagger** para explorar y probar cada endpoint.
4. Inicia el `VehicleSimulationBackgroundService`, que cada segundo avanza los vehículos
   "En ruta" a lo largo de su ruta asignada (origen → paradas → destino) para simular el
   tiempo real; un vehículo "En ruta" sin ruta activa no se mueve. Los parámetros se
   configuran como indica la [sección 5.3](#53-configuración-de-la-simulación-y-del-caché-de-navegación).

> Si NuGet no está accesible en su red corporativa/sandbox, configure un proxy o
> ejecute `dotnet restore` desde una red con salida a `api.nuget.org`.

### 5.2 Frontend

El frontend es HTML/CSS/JS estático — no requiere Node.js ni build. Basta con
abrir los archivos con un servidor estático simple (abrir el `.html` directamente
con doble clic también funciona en la mayoría de navegadores, gracias a que CORS
está configurado en el backend con `AllowAnyOrigin`).

Opción recomendada (evita restricciones de `file://` en algunos navegadores):

```bash
cd FleetManagementSystem/frontend
python3 -m http.server 8080
# Abrir http://localhost:8080/login.html
```

Si el backend corre en un host/puerto distinto de `http://localhost:5080`, ajuste
la constante `API_BASE_URL` en `frontend/js/api.js`.

### 5.3 Configuración de la simulación y del caché de navegación

Los parámetros de la simulación del recorrido y del caché del proxy de navegación
([sección 8.10](#810-proxy--caché-del-proveedor-de-navegación)) no están escritos dentro del
código: se leen de las secciones `Simulation` y `NavigationCache` de la configuración con el
patrón Options (`IOptions<T>`) y se **validan al arrancar**. Un valor imposible (por ejemplo,
`Simulation:TickSeconds` igual a 0) detiene la aplicación con un mensaje claro en lugar de
dejar una simulación congelada. `appsettings.json` no incluye estas secciones, así que, si no
se configuran, valen los valores por defecto:

| Clave | Por defecto | Qué controla |
|-------|-------------|--------------|
| `Simulation:TickSeconds` | `1` | Cada cuántos segundos reales avanza la simulación un paso (mayor que 0). |
| `Simulation:TimeAcceleration` | `1` | Cuántas veces más rápido que en la realidad se mueven los vehículos (mayor que 0). Con `1`, un vehículo que va a 100 km/h recorre 100 km en una hora real; con `10`, en 6 minutos. |
| `Simulation:DelayedSpeedFactor` | `0.5` | Fracción de la velocidad normal con la que avanza una ruta en estado `Delayed` (mayor que 0 y hasta 1). |
| `NavigationCache:TimeToLiveMinutes` | `10` | Minutos que se considera vigente una URL de navegación guardada (mayor que 0 y hasta 10 080, una semana). |
| `NavigationCache:MaxEntries` | `500` | Máximo de URLs guardadas a la vez (al menos 1). |

Para una demostración, lo más cómodo son las variables de entorno (el doble guion bajo
equivale a `:`). El viaje de demostración de `VAN-002` (6,955 km a 100 km/h) dura unos
4 min 10 s con la configuración por defecto y unos 25 s con aceleración 10:

```bash
# Git Bash / Linux / macOS
export Simulation__TimeAcceleration=10
dotnet run --project src/FleetManagement.Api
```

```powershell
# PowerShell
$env:Simulation__TimeAcceleration = "10"
dotnet run --project src/FleetManagement.Api
```

> `appsettings.json` ya traía la clave `FleetOptions:SimulationIntervalSeconds`, pero ningún
> código la leía (el intervalo de 5 segundos estaba escrito dentro del servicio de simulación).
> Sigue ahí, sin efecto y sin modificar; la clave que gobierna el intervalo ahora es
> `Simulation:TickSeconds`.

---

## 6. Credenciales de demostración

| Rol | Usuario | Contraseña | Vista |
|-----|---------|------------|-------|
| Administrador | `admin` | `Admin123!` | `dashboard.html` |
| Operador (conductor) | `operador1` | `Operador123!` | `operator.html` (tiene una ruta activa asignada) |
| Operador (conductor) | `operador2` | `Operador123!` | `operator.html` (tiene una ruta planificada) |

Las contraseñas se almacenan con hashing **PBKDF2** (`Rfc2898DeriveBytes`, 100.000
iteraciones, sal aleatoria por usuario) — ver `Infrastructure/Security/PasswordHasher.cs`.

---

## 7. Principios SOLID aplicados

| Principio | Cómo se aplica | Ejemplo / ruta |
|-----------|-----------------|-----------------|
| **S — Responsabilidad única** | Cada clase tiene un único motivo de cambio: los controladores sólo traducen HTTP↔DTOs, los servicios sólo orquestan reglas de negocio, los repositorios sólo persisten. | `Api/Controllers/FleetController.cs` (HTTP) vs. `Application/Services/VehicleService.cs` (negocio) vs. `Infrastructure/Persistence/InMemoryVehicleRepository.cs` (datos) |
| **O — Abierto/Cerrado** | Se puede agregar un nuevo tipo de vehículo creando una clase nueva (`IVehicleFactory`), sin modificar `VehicleService` ni `VehicleFactoryProvider` más que para registrarla. | `Application/Factories/ConcreteVehicleFactories.cs` |
| **L — Sustitución de Liskov** | Cualquier implementación de `IVehicleRepository`, `IDeliveryRouteBuilder` o `IVehicleFactory` puede sustituir a otra sin romper el comportamiento esperado por quien la consume (p. ej. `InMemoryVehicleRepository` podrá cambiarse por una implementación EF Core sin que `VehicleService` note la diferencia). | Todas las interfaces en `Application/Interfaces/**` |
| **I — Segregación de interfaces** | En vez de una única interfaz gigante de "repositorio genérico", cada agregado tiene su propia interfaz mínima (`IVehicleRepository`, `IMaintenanceRepository`, `ITripAlertRepository`, …), y cada caso de uso expone sólo los métodos que sus consumidores necesitan. | `Application/Interfaces/Repositories/*.cs` |
| **D — Inversión de dependencias** | Las capas externas dependen de abstracciones definidas por las capas internas, nunca al revés. `Program.cs` es el único lugar que conoce las implementaciones concretas. | `Program.cs` (Composition Root) + `Application/Interfaces/**` |

---

## 8. Patrones de diseño

Los cinco patrones solicitados están implementados, **conectados a un caso de uso
real** (no como código de ejemplo aislado) y expuestos a través de la API y la
interfaz gráfica, para que se puedan ejercitar de punta a punta.

### 8.1 Singleton — `FleetAuditLogger`

**Ruta:** `src/FleetManagement.Infrastructure/Logging/FleetAuditLogger.cs`
**Interfaz (DIP):** `src/FleetManagement.Application/Interfaces/IFleetAuditLogger.cs`
**Registro en DI:** `src/FleetManagement.Api/Program.cs`
**Consumido por:** `VehicleService`, `DeliveryRouteService`, `MaintenanceService`, `TripAlertService`, `AuthService`, y expuesto en `GET /api/audit/logs` (pestaña "Auditoría" del dashboard).

```csharp
public sealed class FleetAuditLogger : IFleetAuditLogger
{
    private static readonly Lazy<FleetAuditLogger> LazyInstance =
        new(() => new FleetAuditLogger(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static FleetAuditLogger Instance => LazyInstance.Value;

    private readonly ConcurrentQueue<AuditLogEntryDto> _logs = new();
    private readonly object _writeLock = new();

    private FleetAuditLogger() { }   // constructor privado: única forma de obtenerlo es "Instance"

    public void LogEvent(string category, string message, string? username = null)
    {
        lock (_writeLock) { /* encola y recorta el historial */ }
    }
}
```

```csharp
// Program.cs — reconciliación del Singleton clásico con el contenedor de DI
builder.Services.AddSingleton<IFleetAuditLogger>(_ => FleetAuditLogger.Instance);
```

**Por qué este patrón:** el enunciado pide explícitamente un Singleton thread-safe
para auditar eventos críticos. Un logger de auditoría debe tener **una única
fuente de verdad** en todo el proceso: si cada clase creara su propia instancia,
el historial quedaría fragmentado y sería imposible tener una vista centralizada
de "qué pasó y cuándo" en el sistema.

**Por qué es thread-safe:** `Lazy<T>` con `LazyThreadSafetyMode.ExecutionAndPublication`
(el modo por defecto) garantiza que, aunque varios hilos pidan `Instance` al mismo
tiempo por primera vez, el constructor sólo se ejecuta una vez. El `lock` adicional
protege la operación compuesta "encolar + recortar historial", que si no fuera
atómica podría perder o duplicar el recorte bajo concurrencia real (varios
controladores auditando al mismo tiempo).

**Beneficio concreto:** cualquier parte del sistema —desde un login fallido hasta
un cambio de estado de un vehículo— escribe en el **mismo** registro, y el panel
de auditoría del administrador puede mostrar una línea de tiempo confiable de toda
la actividad del sistema con una sola consulta.

---

### 8.2 Factory Method — Fábricas de vehículos

**Ruta interfaz:** `src/FleetManagement.Application/Factories/IVehicleFactory.cs`
**Ruta implementaciones concretas:** `src/FleetManagement.Application/Factories/ConcreteVehicleFactories.cs` (`TruckFactory`, `VanFactory`, `CarFactory`, `MotorcycleFactory`)
**Ruta proveedor:** `src/FleetManagement.Application/Factories/VehicleFactoryProvider.cs`
**Consumido por:** `VehicleService.CreateVehicleAsync()` → expuesto en `POST /api/fleet/vehicles` (botón "+ Vehículo" del dashboard).

```csharp
public interface IVehicleFactory
{
    Vehicle CreateVehicle(string licensePlate, string brand, string model, int year, double latitude, double longitude);
}

public class TruckFactory : IVehicleFactory
{
    public Vehicle CreateVehicle(string licensePlate, string brand, string model, int year, double latitude, double longitude)
    {
        var vehicle = new Vehicle { /* ... */ Type = VehicleType.Truck, CapacityKg = 8000, Status = VehicleStatus.Available };
        vehicle.UpdateLocation(latitude, longitude);
        return vehicle;
    }
}
```

```csharp
// VehicleService.CreateVehicleAsync
var vehicleFactory = _vehicleFactoryProvider.GetFactory(vehicleType);
var vehicle = vehicleFactory.CreateVehicle(request.LicensePlate, request.Brand, request.Model,
                                            request.Year, request.Latitude, request.Longitude);
```

**Por qué este patrón:** cada tipo de vehículo (camión, furgoneta, automóvil,
motocicleta) necesita valores por defecto distintos (capacidad de carga, etc.).
Sin Factory Method, `VehicleService` tendría un `switch` con la lógica de creación
de los cuatro tipos, y cada vez que se agregara un tipo nuevo habría que **modificar**
esa clase (violando Open/Closed).

**Beneficio concreto:** para agregar, por ejemplo, un tipo `Trailer` en el futuro,
sólo hay que crear `TrailerFactory : IVehicleFactory` y añadir una línea al
diccionario de `VehicleFactoryProvider`. `VehicleService`, `FleetController` y
todo el resto del sistema permanecen intactos.

---

### 8.3 Abstract Factory — Fábrica de incorporación de flota

**Ruta interfaz:** `src/FleetManagement.Application/Factories/IFleetOnboardingAbstractFactory.cs`
**Ruta implementaciones concretas:** `src/FleetManagement.Application/Factories/ConcreteOnboardingFactories.cs` (`TruckOnboardingFactory`, `VanOnboardingFactory`, `CarOnboardingFactory`, `MotorcycleOnboardingFactory`)
**Ruta proveedor:** `src/FleetManagement.Application/Factories/FleetOnboardingFactoryProvider.cs`
**Consumido por:** `VehicleService.CreateVehicleAsync()` (mismo flujo que el Factory Method, un paso después).

```csharp
public interface IFleetOnboardingAbstractFactory
{
    MaintenancePlan CreateMaintenancePlan();      // Producto A de la familia
    NavigationProfile CreateNavigationProfile();  // Producto B de la familia
}

public class TruckOnboardingFactory : IFleetOnboardingAbstractFactory
{
    public MaintenancePlan CreateMaintenancePlan() => new() { /* tareas típicas de un camión */ };
    public NavigationProfile CreateNavigationProfile() => new() { AvoidLowBridges = true, MaxSpeedKmh = 80, /* ... */ };
}
```

```csharp
// VehicleService.CreateVehicleAsync — Factory Method y Abstract Factory trabajando juntos
var onboardingFactory = _onboardingFactoryProvider.GetFactory(vehicleType);
var maintenancePlan   = onboardingFactory.CreateMaintenancePlan();
var navigationProfile = onboardingFactory.CreateNavigationProfile();
```

**Diferencia clave con el Factory Method (8.2):** `IVehicleFactory` crea **un
solo producto** (el `Vehicle`). `IFleetOnboardingAbstractFactory` crea **una
familia de dos productos relacionados** (`MaintenancePlan` + `NavigationProfile`)
que **deben ser coherentes entre sí** para el mismo tipo de vehículo.

**Por qué este patrón:** sin Abstract Factory, nada impediría —por un error de
copiar/pegar— asignarle a un camión el plan de mantenimiento pensado para una
motocicleta (cada 2.000 km) junto con un perfil de navegación que sí evita puentes
bajos pero de una furgoneta. Al agrupar ambas fábricas por tipo de vehículo, la
familia completa se construye o no se construye: es imposible mezclar piezas de
familias distintas.

**Beneficio concreto:** al registrar un vehículo, el sistema dota automáticamente
al vehículo de un plan de mantenimiento y un perfil de navegación **consistentes
con su tipo**, sin que el desarrollador tenga que recordar combinarlos a mano en
cada punto del código donde se crea un vehículo.

---

### 8.4 Builder — Constructor de rutas

**Ruta interfaz:** `src/FleetManagement.Application/Builders/IDeliveryRouteBuilder.cs`
**Ruta implementación:** `src/FleetManagement.Application/Builders/DeliveryRouteBuilder.cs`
**Ruta Director (opcional):** `src/FleetManagement.Application/Builders/DeliveryRouteDirector.cs`
**Consumido por:** `DeliveryRouteService.CreateRouteAsync()` → `POST /api/routes` (botón "+ Nueva ruta"); `DeliveryRouteService.CreateExpressRouteAsync()` → `POST /api/routes/express` (botón "⚡ Ruta express", usa el Director).

```csharp
public interface IDeliveryRouteBuilder
{
    IDeliveryRouteBuilder WithName(string name);
    IDeliveryRouteBuilder WithOrigin(double latitude, double longitude);
    IDeliveryRouteBuilder WithDestination(double latitude, double longitude);
    IDeliveryRouteBuilder AddWaypoint(double latitude, double longitude, string label);
    IDeliveryRouteBuilder AddCargoItem(string description, double weightKg, double volumeM3, CargoPriority priority);
    IDeliveryRouteBuilder AssignVehicle(Guid vehicleId);
    IDeliveryRouteBuilder AssignDriver(Guid driverId);
    IDeliveryRouteBuilder ScheduleFor(DateTime date);
    IDeliveryRouteBuilder WithEstimatedTrip(double distanceKm, double durationMinutes);
    DeliveryRoute Build();  // valida (nombre, origen y destino obligatorios) antes de entregar el objeto
}
```

```csharp
// DeliveryRouteService.CreateRouteAsync — construcción paso a paso con partes opcionales
var builder = _routeBuilder.WithName(request.Name)
                            .WithOrigin(request.OriginLat, request.OriginLng)
                            .WithDestination(request.DestinationLat, request.DestinationLng);

foreach (var wp in request.Waypoints ?? new())
    builder = builder.AddWaypoint(wp.Latitude, wp.Longitude, wp.Label);

foreach (var cargo in request.CargoItems ?? new())
    builder = builder.AddCargoItem(cargo.Description, cargo.WeightKg, cargo.VolumeM3, priority);

var route = builder.Build();
```

**Por qué este patrón:** una ruta puede tener cero o varias paradas intermedias,
cero o varios artículos de carga, y asignaciones de vehículo/conductor opcionales.
Representar todas esas combinaciones con un único constructor produciría una firma
de 10+ parámetros (muchos opcionales/`null`) difícil de leer y de invocar
correctamente. El Builder permite **agregar sólo lo que aplica**, en el orden que
convenga, y **validar el resultado en `Build()`** antes de entregarlo.

**El Director (`DeliveryRouteDirector`):** encapsula una "receta" reutilizable
—una ruta express con carga urgente y salida inmediata— para no repetir la misma
secuencia de llamadas al builder en cada lugar donde se necesite ese caso común.

**Beneficio concreto:** `DeliveryRouteService` arma la ruta con un bucle simple
sobre las paradas y la carga que vinieron en la petición HTTP, sin `if`s anidados
ni un DTO gigante con decenas de campos opcionales, y `Build()` impide crear una
ruta sin nombre, origen o destino.

---

### 8.5 Prototype — Clonación de vehículos y rutas

**Ruta interfaz:** `src/FleetManagement.Domain/Common/IPrototype.cs`
**Implementaciones:** `Vehicle.Clone()` en `src/FleetManagement.Domain/Entities/Vehicle.cs`; `DeliveryRoute.Clone()` (+ `Waypoint.Clone()`, `CargoItem.Clone()`) en `src/FleetManagement.Domain/Entities/DeliveryRoute.cs`, `Waypoint.cs`, `CargoItem.cs`.
**Consumido por:** `VehicleService.CloneVehicleAsync()` → `POST /api/fleet/vehicles/{id}/clone` (botón ⧉ junto a cada vehículo); `DeliveryRouteService.DuplicateRouteAsync()` → `POST /api/routes/{id}/duplicate` (botón "⧉ Duplicar" en cada ruta, para rutas recurrentes).

```csharp
public interface IPrototype<T>
{
    T Clone();
}

public class Vehicle : IPrototype<Vehicle>
{
    public Vehicle Clone() => new()
    {
        Id = Guid.NewGuid(),                 // nueva identidad: es una unidad física distinta
        LicensePlate = string.Empty,         // debe asignarse individualmente
        Brand = Brand, Model = Model, Type = Type, CapacityKg = CapacityKg,
        Status = VehicleStatus.Available,
        MileageKm = 0,
        CurrentLocation = new Point(CurrentLocation.X, CurrentLocation.Y) { SRID = 4326 }, // copia profunda
        AssignedDriverId = null,
        RegisteredAt = DateTime.UtcNow
    };
}
```

```csharp
public class DeliveryRoute : IPrototype<DeliveryRoute>
{
    public DeliveryRoute Clone() => new()
    {
        Id = Guid.NewGuid(),
        Waypoints = Waypoints.Select(w => w.Clone()).ToList(),   // copia profunda de la lista
        CargoItems = CargoItems.Select(c => c.Clone()).ToList(), // ídem
        // ... vehículo/conductor NO se copian, para no duplicar una asignación por accidente
    };
}
```

**Por qué este patrón:** dar de alta 10 camionetas idénticas de flota, o programar
"la misma ruta de reparto" todos los días, son operaciones frecuentes en un
sistema de logística real. Reconstruir cada vez la entidad completa desde cero
—sobre todo una ruta, que tiene listas anidadas de paradas y artículos de
carga— es repetitivo y propenso a errores de captura.

**Por qué la copia debe ser profunda (deep copy):** si `Clone()` simplemente
copiara la referencia a la lista `Waypoints`, modificar las paradas del clon
también modificaría las de la ruta original (serían la misma lista en memoria).
Por eso cada elemento de las colecciones también implementa `IPrototype<T>` y se
clona individualmente (`Waypoints.Select(w => w.Clone())`).

**Qué se copia y qué no (decisión de diseño explícita):** los datos "de plantilla"
(marca, modelo, tipo, paradas, artículos de carga) sí se copian; la identidad, la
placa, el kilometraje, el historial de mantenimiento y las asignaciones de
vehículo/conductor **no** se copian, porque cada clon es una entidad físicamente
distinta que aún no debería competir por los mismos recursos que el original.

**Beneficio concreto:** onboarding de flota y programación de rutas recurrentes
pasan de "volver a diligenciar todos los campos" a "clonar y ajustar dos datos".

---

### 8.6 Resumen de ubicaciones de los patrones

| Patrón | Endpoint que lo ejercita | Botón en la UI |
|--------|---------------------------|----------------|
| Singleton (`FleetAuditLogger`) | `GET /api/audit/logs` | Pestaña "Auditoría" |
| Factory Method (`IVehicleFactory`) | `POST /api/fleet/vehicles` | "+ Vehículo" |
| Abstract Factory (`IFleetOnboardingAbstractFactory`) | `POST /api/fleet/vehicles` (mismo endpoint, un paso después del Factory Method) | "+ Vehículo" |
| Builder (`IDeliveryRouteBuilder` + `DeliveryRouteDirector`) | `POST /api/routes` y `POST /api/routes/express` | "+ Nueva ruta" / "⚡ Ruta express" |
| Prototype (`IPrototype<T>`) | `POST /api/fleet/vehicles/{id}/clone` y `POST /api/routes/{id}/duplicate` | ⧉ junto a cada vehículo / ruta |
| Adapter (`INavigationProvider` + `GoogleMapsAdapter`) | `GET /api/navigation/route/{routeId}` | Enlace de navegación de la ruta |
| Bridge (`FleetNotification` + `INotificationChannel`) | `POST /api/maintenance`, `POST /api/alerts`, `PATCH /api/fleet/vehicles/{id}/status` (el envío simulado aparece en la consola del backend) | — |
| Composite (`ICargoComponent` / `CargoGroup`) | `GET /api/routes/{id}/cargo-manifest` | — |
| Decorator (`NotificationDecorator`: Audit, Retry, Priority) | Los mismos endpoints que el Bridge; el resultado queda en `GET /api/audit/logs` | Pestaña "Auditoría" |
| Decorator de reportes (`FleetReportDecorator`: Statistics, MaintenanceAlerts, CargoSummary) | `GET /api/reports/fleet?sections=...` | — |
| Facade (`FleetDashboardFacade`) | `GET /api/dashboard/summary` | — |
| Flyweight (`IVehicleTypeProfileFactory` → `VehicleTypeProfile`) | `GET /api/fleet/vehicle-types`; además lo usa por dentro el simulador, cuyo resultado se ve en `GET /api/progress` | Marcadores del mapa (un ícono compartido por tipo y estado) |
| Proxy (`CachingNavigationProviderProxy`) | `GET /api/navigation/route/{routeId}` (el mismo endpoint del Adapter: la segunda consulta de la misma ruta sale del caché) | Enlace de navegación de la ruta |

### 8.7 Patrones estructurales: Adapter, Bridge, Composite y Decorator

| Patrón | Dónde está | Qué resuelve |
|--------|------------|--------------|
| **Adapter** | `Application/Interfaces/INavigationProvider` (Target), `Infrastructure/Navigation/GoogleMapsAdapter` (Adapter), `GoogleMapsApi` (Adaptee); cliente: `NavigationService` | Desacopla la navegación de Google Maps: integrar otro proveedor sólo requiere otro adapter. |
| **Bridge** | `Application/Notifications/FleetNotification` y sus especializaciones (`Maintenance`, `TripAlert`, `Vehicle`); `Interfaces/INotificationChannel` y los canales Email/SMS/Push en `Infrastructure/Notifications` | Separa *qué* se notifica de *por dónde* se envía, sin una clase por cada combinación. |
| **Composite** | `Domain/Cargo/` (`ICargoComponent`, `CargoItemComponent`, `CargoGroup`, `CargoManifest`), `Application/Services/CargoManifestService`, `Api/Controllers/CargoManifestController` | Trata de forma uniforme un artículo de carga y grupos de cargas anidados: manifiesto de la ruta agrupado por prioridad, con pesos y volúmenes totales. |
| **Decorator** | `Application/Notifications/INotification` y `Application/Notifications/Decorators/` (`Audit`, `Retry`, `Priority`) | Añade auditoría, reintentos y prioridad a una notificación sin modificar sus clases ni el canal. |

Los cuatro conviven con los patrones creacionales sin reemplazarlos: el Composite sólo
**lee** las rutas que construyen Builder y Prototype (`CargoItem` no se modificó; la hoja
`CargoItemComponent` lo envuelve), y el Decorator **envuelve** la notificación del Bridge
(el único cambio en esas clases es que `FleetNotification` implementa `INotification`).

Ejemplo de Bridge + Decorator, tal como lo usan `MaintenanceService`, `TripAlertService` y `VehicleService`:

```csharp
await new MaintenanceNotification(channel, recipient, plate, type, dueDate, dueKm) // Bridge: qué + por qué canal
    .WithPriority(NotificationPriority.Normal)   // Decorator: prioridad (también decide cuántos reintentos)
    .WithRetry()                                 // Decorator: reintenta si el canal falla
    .WithAudit(auditLogger)                      // Decorator: registra resultado y errores en la bitácora
    .SendAsync();
```

Orden recomendado: `Priority` dentro de `Retry`, y `Retry` dentro de `Audit` (el primero que se
escribe queda más adentro). Detalle y diagramas en la documentación técnica de la integración.

### 8.8 Decorator de reportes y Facade del resumen

Se agregaron **de forma aditiva**: ningún patrón anterior se reemplazó ni se reescribió. El único archivo
existente que cambió en `src/` es `Program.cs` (dos `using` y tres registros de DI); todo lo demás es código nuevo.

| Patrón | Dónde está | Qué resuelve |
|--------|------------|--------------|
| **Decorator (reportes)** | `Application/Reports/` (`IFleetReportGenerator` = Componente, `FleetReportGenerator` = Componente concreto), `Application/Reports/Decorators/` (`FleetReportDecorator` = Decorador base; `StatisticsReportDecorator`, `MaintenanceAlertsReportDecorator`, `CargoSummaryReportDecorator`), `Application/Services/FleetReportService`, `Api/Controllers/ReportsController` | Añade secciones opcionales a un reporte base (vehículos y rutas) sin modificar el generador base ni las demás secciones, y en el orden que pida el cliente. |
| **Facade** | `Application/Facades/` (`IFleetDashboardFacade`, `FleetDashboardFacade`), `Api/Controllers/DashboardController` | Una sola llamada coordina los servicios de vehículos, rutas, mantenimiento y alertas para armar el resumen del dashboard; los servicios siguen siendo la única fuente de cada dato. |

Es independiente del Decorator de notificaciones de la sección 8.7 (otro componente, otras clases). Las cifras que
comparten el reporte y el resumen —estadísticas, totales de carga, mantenimientos vencidos y conteo por estado— se
calculan en un único lugar, `Application/Metrics/FleetMetrics`, para que ambos den exactamente los mismos números
sin repetir lógica. Los datos salen siempre de los servicios y repositorios existentes: no se inventó ningún campo.

```csharp
// El primero que se escribe queda más adentro (igual que en el Decorator de notificaciones).
IFleetReportGenerator report =
    new CargoSummaryReportDecorator(
        new MaintenanceAlertsReportDecorator(
            new StatisticsReportDecorator(baseGenerator), maintenanceService));
FleetReportDto dto = await report.GenerateAsync();
```

En la API, `FleetReportService` arma esa cadena en cada petición según `sections`; en DI sólo está registrado
el generador base.

| Endpoint | Resultado |
|----------|-----------|
| `GET /api/reports/fleet` | Reporte base: `metadata`, `vehicles` y `routes`. |
| `GET /api/reports/fleet?sections=Statistics,MaintenanceAlerts,CargoSummary` | Lo anterior más `statistics`, `maintenanceAlerts` y `cargoSummary`. Las secciones pueden ir separadas por comas o repetirse (`sections=A&sections=B`), sin distinguir mayúsculas. Una sección desconocida (o un número) devuelve `400` con `{ "message": ... }` que lista los valores válidos. |
| `GET /api/dashboard/summary` | `generatedAtUtc`, `statistics`, `cargo`, `overdueMaintenanceCount` y `alertsByStatus`. |

Límites a tener en cuenta:

- El módulo de mantenimiento sólo expone los elementos **vencidos**; el modelo no tiene un concepto de «pendientes» o
  «próximos», así que no se muestran (no se inventaron).
- Añadir un decorador que aporte un tipo de sección **nuevo** exige una propiedad anulable en `FleetReportDto`, un valor
  en `ReportSection` y una línea en el `switch` de `FleetReportService`; el generador base y los demás decoradores no cambian.
- Los dos endpoints son de sólo lectura y, como el resto de las consultas `GET`, no exigen rol (ver sección 9).
- El frontend todavía no consume estos endpoints: se pueden probar con Swagger o `curl`.

### 8.9 Flyweight — perfiles compartidos por tipo de vehículo

Se agregó **de forma aditiva**, junto con la simulación del recorrido (secciones 5.3 y 10): ninguna clase de los
patrones anteriores se modificó. El Abstract Factory (`FleetOnboardingFactoryProvider` y sus fábricas),
`VehicleService` y `FleetController` siguen exactamente como estaban; `VehicleService.CreateVehicleAsync` sigue
creando, con el Abstract Factory, el `NavigationProfile` y el `MaintenancePlan` propios de cada vehículo nuevo.

| Pieza | Dónde está | Papel |
|-------|------------|-------|
| `VehicleTypeProfile` | `Application/Flyweights/VehicleTypeProfile.cs` | **Flyweight**: perfil inmutable que comparten todos los vehículos de un mismo tipo (nombre del perfil, velocidad máxima, restricciones de navegación y plantillas de mantenimiento). |
| `IVehicleTypeProfileFactory` / `VehicleTypeProfileFactory` | `Application/Flyweights/` | **Fábrica de flyweights**: `GetProfile(VehicleType)` devuelve siempre la **misma instancia** para un tipo (`ConcurrentDictionary<VehicleType, Lazy<…>>`, segura para varios hilos). |
| `Vehicle` y `TripProgress` | `Domain/Entities/Vehicle.cs`, `Application/Simulation/TripProgress.cs` | **Estado extrínseco**: placa, posición, estado, kilometraje y avance del viaje. Cambia por vehículo y no se comparte. |
| Consumidores | `VehicleMovementSimulator`, `TripProgressService` y `VehicleTypeService` (con `GET /api/fleet/vehicle-types`) | Solo **leen** el perfil: nadie lo reconstruye ni lo modifica. |
| Frontend | `frontend/js/dashboard.js` (`vehicleIconCache`, `vehicleDivIcon`) | El mismo patrón en el cliente: un solo ícono de Leaflet (`L.divIcon`) por combinación tipo + estado, compartido por todos los marcadores. |

**Estado intrínseco y extrínseco.** Lo que es igual para cualquier vehículo de un tipo (la velocidad máxima de un
camión, evitar puentes bajos, sus tareas de mantenimiento) es *intrínseco* y vive en el perfil compartido. Lo que
cambia de un vehículo a otro (placa, dónde está, cuántos kilómetros lleva, cuánto de su ruta ha recorrido) es
*extrínseco* y se queda en el vehículo.

| Tipo | Perfil | Velocidad máxima |
|------|--------|------------------|
| `Truck` | Perfil Camión de Carga | 80 km/h |
| `Van` | Perfil Furgoneta de Reparto | 100 km/h |
| `Car` | Perfil Vehículo Liviano | 120 km/h |
| `Motorcycle` | Perfil Motocicleta de Mensajería | 80 km/h |

**Qué ahorra y qué no.** Con cuatro tipos de vehículo el ahorro de memoria es **pequeño**: son cuatro objetos chicos,
y construirlos de nuevo en cada uso costaría microsegundos. Presentarlo como una optimización de memoria sería
exagerar. El patrón se justifica aquí por otras tres razones, que sí aplican:

1. **Una sola fuente de verdad.** El simulador (cuánto avanza cada vehículo), el servicio de progreso (cuántos
   minutos faltan) y el endpoint de tipos leen la misma velocidad del mismo objeto. La ETA que ve el usuario sale de
   la misma función de velocidad que mueve al vehículo (`TripSpeed`), así que no pueden contradecirse.
2. **No reconstruir en cada ciclo.** La simulación corre cada segundo para todos los viajes activos. Pedir el perfil
   es una consulta a un diccionario; el Abstract Factory, en cambio, crea objetos nuevos (`NavigationProfile`,
   `MaintenancePlan`) cada vez que se le pide.
3. **Escalabilidad.** Si el catálogo creciera (por ejemplo, un perfil por modelo con tablas de mantenimiento largas),
   compartir una instancia por clave sí ahorraría memoria de forma medible; el diseño ya está listo para eso.

**Relación con el Abstract Factory.** `VehicleTypeProfileFactory` no repite los valores: los lee de
`FleetOnboardingFactoryProvider` (sin modificarlo) y los **copia** a records inmutables. Los productos de ese
Abstract Factory (`NavigationProfile`, `MaintenancePlan`) son clases mutables, así que se descartan después de
copiarlos: compartirlos rompería el patrón, porque cualquier consumidor podría alterarlos para todos los demás. Una
prueba comprueba que cada perfil coincide con lo que entrega el Abstract Factory.

```csharp
// En el simulador: la velocidad sale del perfil compartido del tipo, no de un objeto creado para la ocasión.
var profile = _profiles.GetProfile(trip.Vehicle.Type);                      // siempre la misma instancia por tipo
var speedKmh = TripSpeed.KmPerHour(profile, trip.Route.Status, _options);  // perfil × retraso × aceleración
```

```javascript
// dashboard.js — un ícono por combinación tipo|estado, reutilizado por todos los marcadores
const vehicleIconCache = new Map();
function vehicleDivIcon(vehicle) {
  const key = `${vehicle.type}|${vehicle.status}`;
  if (!vehicleIconCache.has(key)) vehicleIconCache.set(key, createVehicleDivIcon(vehicle));
  return vehicleIconCache.get(key);
}
```

En el cliente el caché tiene además un efecto práctico: Leaflet rehace el elemento del marcador cada vez que recibe un
ícono distinto, por eso el dashboard llama a `setIcon` solo cuando el ícono cambió de verdad (así no se corta el
deslizamiento suave del marcador entre dos lecturas).

**Límites.** Un Flyweight solo es seguro mientras sea inmutable: por eso `VehicleTypeProfile` no tiene setters y copia
su lista de tareas a una colección de solo lectura. Los perfiles no cambian mientras la aplicación corre; si algún día
hicieran falta cambios en caliente, habría que **reemplazar** la instancia, nunca modificarla.

**Cómo comprobarlo.** `GET /api/fleet/vehicle-types` devuelve los cuatro perfiles con su velocidad máxima, y
`GET /api/progress` muestra al simulador usándolos: la ETA de un viaje es `kilómetros restantes / velocidad del tipo`
(ajustada por retraso y aceleración). Las pruebas de `Flyweight/` verifican con `Assert.Same` que el mismo tipo
devuelve siempre la misma instancia, también desde muchos hilos a la vez (`Parallel.For`).

### 8.10 Proxy — caché del proveedor de navegación

Se agregó **de forma aditiva**: `NavigationService`, `GoogleMapsAdapter` y `GoogleMapsApi` **no se modificaron**. Fuera
del código nuevo, lo único que cambió es el registro de `INavigationProvider` en `Program.cs`.

| Pieza | Dónde está | Papel |
|-------|------------|-------|
| `INavigationProvider` | `Application/Interfaces/` (ya existía) | **Interfaz del sujeto**: la misma que usa `NavigationService`. El proxy la implementa, así que el cliente no nota la diferencia. |
| `GoogleMapsAdapter` | `Infrastructure/Navigation/` (sin cambios) | **Sujeto real**: genera la URL de Google Maps. |
| `CachingNavigationProviderProxy` | `Infrastructure/Navigation/` | **Proxy de caché**: si la misma ruta ya se pidió hace poco, devuelve la URL guardada y no llega al sujeto real. |
| `NavigationCacheKey` | `Infrastructure/Navigation/` | Clave de una ruta: origen + paradas en orden de visita + destino. |
| `ExpiringUrlCache` | `Infrastructure/Navigation/` | Almacén con vencimiento y tamaño máximo, seguro para varios hilos. |
| `NavigationCacheOptions` | `Infrastructure/Navigation/` | Tiempo de vida y tamaño máximo, configurables ([sección 5.3](#53-configuración-de-la-simulación-y-del-caché-de-navegación)). |

El Adapter pasa a registrarse como clase concreta y `INavigationProvider` se construye con una fábrica que lo envuelve
(`Program.cs`):

```csharp
builder.Services.AddSingleton<GoogleMapsAdapter>();
// ... opciones validadas de "NavigationCache" y TimeProvider.System ...
builder.Services.AddSingleton<INavigationProvider>(provider => new CachingNavigationProviderProxy(
    provider.GetRequiredService<GoogleMapsAdapter>(),
    provider.GetRequiredService<NavigationCacheOptions>(),
    provider.GetRequiredService<TimeProvider>()));
```

**Qué pasa en una consulta.**

1. `NavigationService` pide la URL a su `INavigationProvider`, que ahora es el proxy.
2. El proxy calcula la `NavigationCacheKey` de la ruta.
3. Si hay una entrada **vigente** con esa clave, devuelve esa URL y **no llama** al sujeto real.
4. Si no, llama al Adapter, guarda la URL con su tiempo de vida y la devuelve.

**La clave.** Dos pedidos comparten entrada solo si el proveedor real devolvería la misma URL:

- Origen, destino y paradas **en el orden en que se visitan** (se ordenan por `Order`, igual que hace el Adapter).
- Coordenadas redondeadas a **6 decimales** (~11 cm) y formateadas con la **cultura invariante** (con coma decimal las
  claves cambiarían de un equipo a otro). `-0` y `0` dan la misma clave.
- **No** entran la etiqueta de las paradas, el nombre de la ruta ni los valores absolutos de `Order`, porque no afectan a
  la URL de Google Maps: dos rutas con otro nombre y otras etiquetas, pero la misma geometría, comparten entrada.

**Vencimiento y tamaño.** Una entrada vence cuando `ahora ≥ creación + NavigationCache:TimeToLiveMinutes` (el reloj es un
`TimeProvider` inyectado: las pruebas lo adelantan a mano en vez de esperar). Al llegar a `NavigationCache:MaxEntries` se
retiran primero las entradas vencidas y, si sigue lleno, la más antigua. Una URL vencida nunca se devuelve.

**Proxy frente a Decorator.** Estructuralmente se parecen (los dos envuelven un objeto con su misma interfaz), pero la
intención es distinta:

| | Decorators del proyecto (notificaciones y reportes) | Proxy de caché |
|---|---|---|
| Intención | **Añadir responsabilidades** funcionales al objeto envuelto: reintentar, auditar, dar prioridad, agregar secciones a un reporte. | **Controlar el acceso** al sujeto real: decidir si y cuándo se le llama. |
| Efecto en el resultado | El resultado cambia o se enriquece. | Ninguno: devuelve exactamente lo que daría el sujeto real. |
| Composición | Se apilan y combinan en el orden que elige quien los arma (`.WithPriority().WithRetry().WithAudit()`). | Uno solo, fijo, decidido en el Composition Root. |
| Quién lo conoce | Quien los arma decide cuáles aplicar. | El cliente (`NavigationService`) no sabe que existe. |
| Ciclo de vida | Por operación o por petición. | **Singleton**: guarda estado (el caché); con otro ciclo de vida cada petición tendría un caché vacío. |

**Compromisos asumidos** (decisiones conscientes, no descuidos):

- **Sin coalescencia de pedidos simultáneos.** Si dos peticiones piden a la vez una ruta que todavía no está en el
  caché, las dos pueden llegar al proveedor (una vez cada una). Es aceptable porque generar la URL no tiene efectos
  secundarios; evitarlo exigiría coordinar los pedidos en vuelo, más complejidad de la que este caso justifica.
- **Caché de memoria, por proceso.** No se comparte entre instancias de la API ni sobrevive a un reinicio. Con varias
  instancias haría falta un caché distribuido detrás de la misma interfaz.
- **Los errores no se guardan.** Si el proveedor falla, la excepción llega al cliente tal cual y el siguiente pedido lo
  intenta de nuevo.
- **Redondeo a 6 decimales.** Dos puntos a menos de ~11 cm comparten URL; para una app de navegación es irrelevante,
  pero es una decisión consciente.
- **El beneficio práctico de hoy es pequeño.** `GoogleMapsApi.BuildDirectionsUrl` solo arma una cadena en memoria: no
  hay red, cuota ni latencia que ahorrar. El proxy se justifica cuando el proveedor sea remoto (por ejemplo, una API de
  direcciones con cuota y costo por consulta); como es transparente, entonces bastará con cambiar el sujeto que envuelve.

**Cómo comprobarlo.** Como el proxy es transparente, el endpoint `GET /api/navigation/route/{routeId}` responde igual con
o sin él, así que la evidencia no está en la respuesta sino en las pruebas, que cuentan cuántas veces se llama al
sujeto: `dotnet test --filter "FullyQualifiedName~Proxy"`. Cubren que la segunda consulta idéntica no llega al sujeto, que
una clave distinta sí, el vencimiento por tiempo de vida (con un reloj manual), el tamaño máximo, que los errores no se
guardan, la transparencia frente al `GoogleMapsAdapter` real y el conjunto con el `NavigationService` real.

---

## 9. Documentación de la API REST

Documentación interactiva completa disponible en **Swagger** (`/swagger`) al
correr el backend. Resumen de endpoints:

| Método | Ruta | Descripción | Rol requerido |
|--------|------|--------------|----------------|
| POST | `/api/auth/login` | Inicia sesión, devuelve token de sesión | — |
| POST | `/api/auth/logout` | Cierra la sesión actual | — |
| GET | `/api/fleet/vehicles` | Ubicación y estado de todos los vehículos | — |
| GET | `/api/fleet/vehicles/{id}` | Detalle de un vehículo | — |
| GET | `/api/fleet/vehicle-types` | Perfil de cada tipo de vehículo: velocidad máxima, restricciones de navegación y tareas de mantenimiento (Flyweight) | — |
| GET | `/api/progress` | Avance de **todos** los viajes activos en una sola llamada: porcentaje, km recorridos y restantes, próxima parada, ETA en minutos y puntos de la ruta | — |
| POST | `/api/fleet/vehicles` | Crea un vehículo (Factory Method + Abstract Factory) | Admin |
| POST | `/api/fleet/vehicles/{id}/clone` | Clona un vehículo plantilla (Prototype) | Admin |
| PATCH | `/api/fleet/vehicles/{id}/status` | Cambia el estado de un vehículo | Admin |
| GET | `/api/drivers` | Lista de conductores | — |
| GET | `/api/routes` | Lista de rutas | — |
| GET | `/api/routes/{id}` | Detalle de una ruta | — |
| GET | `/api/routes/driver/{driverId}` | Rutas asignadas a un conductor | — |
| GET | `/api/routes/{id}/cargo-manifest` | Manifiesto jerárquico de la carga de una ruta (Composite) | — |
| POST | `/api/routes` | Crea una ruta (Builder) | Admin |
| POST | `/api/routes/express` | Crea una ruta express (Builder + Director) | Admin |
| POST | `/api/routes/{id}/duplicate` | Duplica una ruta (Prototype) | Admin |
| PATCH | `/api/routes/{id}/status` | Cambia el estado de una ruta | — |
| GET | `/api/maintenance` | Historial de mantenimiento | — |
| GET | `/api/maintenance/vehicle/{vehicleId}` | Historial de un vehículo | — |
| GET | `/api/maintenance/due` | Vehículos con mantenimiento vencido (predictivo) | — |
| POST | `/api/maintenance` | Registra un mantenimiento | Admin |
| GET | `/api/alerts` | Lista de alertas de viaje | — |
| GET | `/api/alerts/route/{routeId}` | Alertas de una ruta | — |
| POST | `/api/alerts` | Reporta una complicación durante el viaje | — (operador) |
| PATCH | `/api/alerts/{id}/resolve` | Marca una alerta como resuelta | Admin |
| GET | `/api/navigation/route/{routeId}` | Enlace de navegación externa (Google Maps). Adapter detrás de un Proxy de caché | — |
| GET | `/api/audit/logs` | Registro de auditoría (Singleton) | Admin |
| GET | `/api/reports/fleet` | Reporte de la flota (vehículos y rutas). Con `?sections=Statistics,MaintenanceAlerts,CargoSummary` agrega esas secciones (Decorator) | — |
| GET | `/api/dashboard/summary` | Resumen general de la flota para el dashboard (Facade) | — |

La autorización por rol se implementa con el atributo `[RequireRole(UserRole.Admin)]`
(`Api/Filters/RequireRoleAttribute.cs`), evaluado sobre el `ClaimsPrincipal` que
construye `TokenAuthenticationMiddleware` a partir del header `Authorization: Bearer {token}`.

---

## 10. Notas sobre paso a producción

Este es un **prototipo**; las siguientes decisiones fueron tomadas deliberadamente
para mantenerlo simple, y quedan documentadas aquí para quien continúe el proyecto:

- **Persistencia:** los repositorios (`Infrastructure/Persistence/InMemory*.cs`)
  guardan todo en `ConcurrentDictionary` — los datos se pierden al reiniciar el
  proceso. El proyecto ya incluye `FleetDbContext.cs`, configurado para
  PostgreSQL + PostGIS (incluye `HasPostgresExtension("postgis")` y columnas
  `geometry (point, 4326)`), pero **no está registrado en el contenedor de DI**
  por defecto. Para activarlo: registrar `AddDbContext<FleetDbContext>(...)` en
  `Program.cs`, crear implementaciones `Ef*Repository` que reciban el `DbContext`,
  y reemplazar los `AddSingleton<IVehicleRepository, InMemoryVehicleRepository>`
  (y análogos) por `AddScoped<IVehicleRepository, EfVehicleRepository>`. Gracias a
  que `Application` y `Domain` sólo conocen interfaces, este cambio no afecta a
  ninguna otra capa.
- **Autenticación:** se usa un token de sesión simple (GUID) en memoria
  (`ISessionTokenStore`) en lugar de JWT, para no depender de paquetes NuGet
  adicionales en el prototipo. En producción, sustituir por JWT firmado
  (`Microsoft.AspNetCore.Authentication.JwtBearer`) o un proveedor de identidad
  (Azure AD, Auth0, IdentityServer, etc.), implementando `ISessionTokenStore` (o
  reemplazando `TokenAuthenticationMiddleware`) sin tocar `AuthService`.
  Las contraseñas ya se almacenan correctamente hasheadas (PBKDF2 + sal), lo cual
  sí es una práctica válida para producción.
- **Optimización de rutas:** `DeliveryRouteService` estima distancia y tiempo con
  la fórmula de **Haversine** entre origen, paradas y destino (hoy en `Application/Geo/GeoMath`,
  compartida con la simulación; da los mismos resultados que antes), más una velocidad
  promedio asumida. Es una heurística razonable para un prototipo, no un
  solver de ruteo (VRP); un siguiente paso natural sería integrar un motor de
  ruteo real (OSRM, Google Routes API, etc.) detrás de la misma interfaz
  `INavigationService`.
- **Simulación de tiempo real:** `VehicleSimulationBackgroundService` es ahora un
  simple temporizador (`PeriodicTimer`, cada `Simulation:TickSeconds`, por defecto 1 s)
  que en cada paso le pide a `IVehicleMovementSimulator` avanzar el tiempo transcurrido.
  Ya no hay saltos aleatorios: el simulador mueve cada vehículo "En ruta" que tiene una
  ruta activa (`InProgress` o `Delayed`) **a lo largo de la poligonal origen → paradas →
  destino**, a la velocidad de su tipo (perfil Flyweight, [sección 8.9](#89-flyweight--perfiles-compartidos-por-tipo-de-vehículo))
  multiplicada por `Simulation:TimeAcceleration`; una ruta `Delayed` avanza a
  `Simulation:DelayedSpeedFactor` de esa velocidad (la mitad por defecto). Reglas:
  - Un vehículo "En ruta" **sin** ruta activa no se mueve.
  - El kilometraje suma la distancia realmente recorrida sobre la ruta.
  - Al llegar, el vehículo queda en el destino exacto y la ruta se completa con
    `IDeliveryRouteService.UpdateRouteStatusAsync(id, "Completed")`, que ya libera al
    vehículo y deja la auditoría (esa lógica no se duplicó).
  - Si una ruta deja de estar activa por un cambio manual de estado (por ejemplo,
    `Cancelled`), su avance se descarta en el siguiente paso.
  - `GET /api/progress` informa el avance de todos los viajes activos en una sola
    llamada. El dashboard y el portal del operador lo consultan cada 5 s, **en paralelo**
    con los vehículos (`Promise.all`), dibujan el recorrido con sus paradas (`L.polyline`),
    muestran porcentaje, kilómetros restantes, próxima parada y ETA, y deslizan el
    marcador de forma suave entre una lectura y la siguiente.

  Los parámetros están en la [sección 5.3](#53-configuración-de-la-simulación-y-del-caché-de-navegación).
  En un sistema real, esta posición vendría de dispositivos GPS/telemetría reportando
  a la API (o a un broker de mensajes) en lugar de generarse en el servidor.
- **CORS:** configurado como `AllowAnyOrigin()` para simplificar la evaluación
  del prototipo desde cualquier origen local. En producción debe restringirse al
  dominio real del frontend.

---

## 11. Limitaciones conocidas del prototipo

- No se compiló en este entorno de generación por no contar con acceso a NuGet;
  el código fue escrito y revisado cuidadosamente (sintaxis, `using`s, tipos de
  los enums, firmas de interfaces vs. implementaciones), pero se recomienda
  ejecutar `dotnet build` como primer paso al recibirlo.
- No incluye pruebas automatizadas (unitarias/integración). La separación por
  capas e interfaces del proyecto está pensada para que agregar pruebas con
  `xUnit` + `Moq`/`NSubstitute` sobre `Application.Services` sea directo, sin
  necesidad de un servidor HTTP real ni de una base de datos.
- El cálculo de "próximo mantenimiento vencido" compara la fecha/kilometraje
  registrados manualmente en cada `MaintenanceRecord`; no hay integración con
  telemetría real del vehículo para el kilometraje actual (se actualiza junto
  con la simulación de movimiento).
- La simulación recorre la ruta en línea recta entre el origen, las paradas y el
  destino (lo mismo que dibuja el mapa): no sigue las calles ni simula tráfico. El
  avance de los viajes vive en memoria, como el resto de los datos, y se pierde al
  reiniciar la API.
- El caché del proxy de navegación ([sección 8.10](#810-proxy--caché-del-proveedor-de-navegación))
  es de memoria y por proceso, y hoy el proveedor real solo arma un texto, así que su
  beneficio práctico es pequeño. Lo mismo vale para el Flyweight con cuatro tipos de
  vehículo ([sección 8.9](#89-flyweight--perfiles-compartidos-por-tipo-de-vehículo)).

---

## 12. Pruebas automatizadas y cómo demostrar el Factory Method

Además de la explicación y el código de la [sección 8.2](#82-factory-method--fábricas-de-vehículos),
el proyecto incluye un quinto proyecto —**`tests/FleetManagement.Application.Tests`**—
con pruebas unitarias **xUnit** que demuestran objetivamente que el patrón
Factory Method funciona, en tres niveles:

| Archivo | Qué demuestra |
|---------|----------------|
| `Factories/VehicleFactoryTests.cs` | Cada fábrica concreta (`TruckFactory`, `VanFactory`, `CarFactory`, `MotorcycleFactory`) produce un `Vehicle` con el `Type` y la `CapacityKg` correctos para su categoría. |
| `Factories/VehicleFactoryProviderTests.cs` | `VehicleFactoryProvider.GetFactory(tipo)` devuelve **polimórficamente** la fábrica concreta correcta para cada `VehicleType` —sin ningún `switch`/`if` en el código cliente— y lanza `NotSupportedException` si un tipo no tiene fábrica registrada. |
| `Factories/VehicleServiceFactoryMethodTests.cs` | El caso de uso completo (`VehicleService.CreateVehicleAsync`, el mismo que invoca `POST /api/fleet/vehicles`) delega correctamente en el Factory Method y devuelve la capacidad correcta según el tipo solicitado; también cubre los caminos de error (tipo inválido, placa duplicada). |

### Ejecutar las pruebas

```bash
cd FleetManagementSystem
dotnet test
```

(el proyecto de pruebas ya está registrado en `FleetManagementSystem.sln`, así
que `dotnet test` en la raíz lo encuentra solo; también puede apuntarlo
explícitamente con `dotnet test tests/FleetManagement.Application.Tests`).

Salida esperada (resumen):

```
Aprobado! - Con error: 0, Superado: 20, Omitido: 0, Total: 20
```

> Si `dotnet restore` se queja de no encontrar la versión exacta de `xunit`,
> `xunit.runner.visualstudio` o `Microsoft.NET.Test.Sdk` indicada en el
> `.csproj`, ejecute `dotnet add package xunit` (sin especificar versión)
> dentro de `tests/FleetManagement.Application.Tests` para que tome la
> última disponible en su máquina; el código de las pruebas no depende de
> ninguna versión concreta.

### Pruebas del Decorator de reportes y del Facade (sección 8.8)

La tabla y la salida de arriba describen sólo las pruebas del Factory Method (20). El mismo proyecto de pruebas
incluye además las de Builder, Prototype, Adapter, Bridge, Composite y Decorator de notificaciones (143 en total
con las anteriores) y las de la integración Decorator de reportes + Facade (75 más), de modo que el total esperado
con todas es de **218** (cada fila de un `[Theory]` cuenta como una prueba). Siguen sin usar librerías de mocking:
los dobles de prueba están escritos a mano.

| Carpeta | Qué demuestra |
|---------|----------------|
| `Metrics/FleetMetricsTests.cs` | Los cálculos compartidos (estadísticas, totales de carga con 3 decimales, vencidos, conteo por estado), incluida la flota vacía sin dividir por cero. |
| `ReportDecorator/` | El generador base y cada decorador por separado; la composición (combinar, orden libre, intercambiables con el componente, una sola consulta por subsistema); la interpretación de `sections` (mayúsculas, comas, duplicados, errores); y la integración con los servicios reales, que coincide con el manifiesto Composite. |
| `Facade/` | El resumen con los cuatro servicios sustituidos y con los servicios reales: cifras, una consulta por subsistema, mismas cifras que el reporte, los errores no se ocultan y es de sólo lectura (no cambia datos, no escribe en la bitácora ni envía avisos). |
| `Support/` | Dobles de prueba de los servicios (`FleetServiceFakes.cs`) y una flota con los servicios reales sobre los repositorios en memoria (`RealFleet.cs`). |

Para ejecutar sólo una parte: `dotnet test --filter "FullyQualifiedName~ReportDecorator"` o `...~Facade`.

### Pruebas de la simulación, el Flyweight y el Proxy (secciones 5.3, 8.9 y 8.10)

Con las pruebas nuevas, el total esperado en el mismo proyecto es de **393**: las 218 anteriores, sin modificar, más 175
nuevas (cada fila de un `[Theory]` cuenta como una prueba). Siguen sin usar librerías de mocking: los dobles de prueba
están escritos a mano.

| Carpeta | Qué demuestra |
|---------|----------------|
| `Geo/` | `GeoMath`: distancias de referencia, simetría, interpolación exacta en los extremos y que `DeliveryRouteService` estima la distancia exactamente igual que antes. |
| `Simulation/` | `RoutePath` (tramos, paradas, punto exacto, tramos de longitud cero), `TripSpeed`, `ActiveTrips`, `SimulationOptions`, `InMemoryTripProgressTracker` (también con varios hilos), `TripProgressService` y `VehicleSimulationBackgroundService`. Del `VehicleMovementSimulator`, con dobles de los repositorios y del servicio de rutas: el vehículo solo se mueve sobre la poligonal; al llegar queda en el destino y la ruta se completa; sin ruta activa no se mueve; una ruta retrasada avanza más despacio; el kilometraje suma la distancia real recorrida. |
| `Flyweight/` | Misma instancia por tipo (`Assert.Same`), también desde muchos hilos a la vez (`Parallel.For`); instancias distintas entre tipos; valores copiados del Abstract Factory; inmutabilidad; y `VehicleTypeService`. |
| `Proxy/` | La segunda llamada idéntica no llega al sujeto, una clave distinta sí, vencimiento por tiempo de vida, tamaño máximo, errores sin guardar, transparencia frente al `GoogleMapsAdapter` real y detrás del `NavigationService` real. Además, el almacén (`ExpiringUrlCache`), la clave (`NavigationCacheKey`) y las opciones (`NavigationCacheOptions`) por separado. |
| `Architecture/` | `Application` no referencia a `Infrastructure` ni a `Api` (ni a ASP.NET Core ni EF Core), `Domain` no referencia a ninguna otra capa e `Infrastructure` no referencia a `Api`. |
| `Support/ManualTimeProvider.cs` | Reloj manual que reemplaza al reloj real en las pruebas del Proxy: el tiempo solo avanza cuando la prueba lo ordena. |

Para ejecutar sólo una parte: `dotnet test --filter "FullyQualifiedName~Simulation"`, `...~Flyweight`, `...~Proxy` o
`...~Architecture`.

### Demostración manual / en vivo (sin correr pruebas)

Para una presentación en vivo, la forma más rápida de evidenciar el patrón es
mostrar que **el mismo endpoint produce objetos distintos según el tipo**, sin
que el controlador ni el servicio tengan un `switch` visible:

1. Ejecute el backend (`dotnet run --project src/FleetManagement.Api`) y abra `http://localhost:5080/swagger`.
2. Expanda `POST /api/fleet/vehicles`, use **"Try it out"** y envíe:
   ```json
   { "licensePlate": "DEMO-1", "brand": "Marca", "model": "Modelo", "year": 2024, "type": "Truck", "latitude": 7.1, "longitude": -73.1 }
   ```
   Observe en la respuesta `"capacityKg": 8000`.
3. Repita el envío cambiando sólo `"type": "Motorcycle"` (y la placa a `DEMO-2`). Observe `"capacityKg": 30`.
4. Esa diferencia de capacidad, obtenida del mismo endpoint sin lógica condicional visible, es la evidencia de que `VehicleFactoryProvider.GetFactory(tipo)` seleccionó una fábrica concreta distinta en cada llamada.
5. Alternativamente, desde `dashboard.html`, use el botón **"+ Vehículo"**: al crear el vehículo aparece la confirmación *"Vehículo creado (patrones Factory Method + Abstract Factory)"* (ver `frontend/js/dashboard.js`), y puede repetir el paso 2-3 cambiando el campo "Tipo" del formulario para ver la misma diferencia de capacidad reflejada de inmediato en la lista de vehículos.
