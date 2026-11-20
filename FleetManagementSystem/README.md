# 🚚 FleetControl — Sistema de Gestión de Flotas

Prototipo full-stack de un Sistema de Gestión de Flotas: monitoreo en tiempo real,
optimización de rutas y asignación de cargas, mantenimiento predictivo e integración
con sistemas de navegación. Incluye autenticación con dos roles (Administrador /
Operador), un dashboard administrativo y un portal para conductores.

> Construido como ejercicio de **Clean Architecture**, **SOLID** y **patrones de
> diseño** sobre **C# / .NET 8**, con un frontend en HTML + CSS + JavaScript puro
> (sin frameworks) y mapas con **Leaflet.js**. Los 21 patrones están en la
> [sección 8](#8-patrones-de-diseño): creacionales (Singleton, Factory Method, Abstract
> Factory, Builder, Prototype), estructurales (Adapter, Bridge, Composite, Decorator,
> Facade, Flyweight, Proxy) y de comportamiento (Strategy, Chain of Responsibility,
> Command, Iterator, Mediator, Memento, **Observer, State y Visitor**).

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
   - [8.11 Strategy — optimizar paradas y repartir cargas](#811-strategy--optimizar-paradas-y-repartir-cargas)
   - [8.12 Decorator — reglas de asignación sobre el servicio de rutas](#812-decorator--reglas-de-asignación-sobre-el-servicio-de-rutas)
   - [8.13 Command — deshacer y rehacer operaciones de rutas](#813-command--deshacer-y-rehacer-operaciones-de-rutas)
   - [8.14 Chain of Responsibility — de la alerta reportada a quien debe atenderla](#814-chain-of-responsibility--de-la-alerta-reportada-a-quien-debe-atenderla)
   - [8.15 Iterator — recorrer la carga de una ruta sin conocer su estructura](#815-iterator--recorrer-la-carga-de-una-ruta-sin-conocer-su-estructura)
   - [8.16 Mediator — coordinar el estado de la ruta con el del vehículo](#816-mediator--coordinar-el-estado-de-la-ruta-con-el-del-vehículo)
   - [8.17 Memento — volver al orden anterior de las paradas](#817-memento--volver-al-orden-anterior-de-las-paradas)
   - [8.18 Observer — publicar la telemetría del recorrido sin que el simulador sepa quién escucha](#818-observer--publicar-la-telemetría-del-recorrido-sin-que-el-simulador-sepa-quién-escucha)
   - [8.19 State — qué puede hacer un vehículo según su estado](#819-state--qué-puede-hacer-un-vehículo-según-su-estado)
   - [8.20 Visitor — analizar la flota sin cargar las entidades con análisis](#820-visitor--analizar-la-flota-sin-cargar-las-entidades-con-análisis)
   - [8.21 Template Method — la hoja de ruta: un esqueleto, contenido distinto por audiencia](#821-template-method--la-hoja-de-ruta-un-esqueleto-contenido-distinto-por-audiencia)
   - [8.22 Interpreter — reglas de mantenimiento y filtros de monitoreo escritos como texto](#822-interpreter--reglas-de-mantenimiento-y-filtros-de-monitoreo-escritos-como-texto)
9. [Documentación de la API REST](#9-documentación-de-la-api-rest)
10. [Notas sobre paso a producción](#10-notas-sobre-paso-a-producción)
11. [Limitaciones conocidas del prototipo](#11-limitaciones-conocidas-del-prototipo)
12. [Pruebas automatizadas y cómo demostrar el Factory Method](#12-pruebas-automatizadas-y-cómo-demostrar-el-factory-method)

---

## 1. Descripción general y módulos funcionales

| # | Módulo | Requerimiento | Dónde vive |
|---|--------|----------------|------------|
| 1 | **Monitoreo en tiempo real** | Ver ubicación y estado de cada vehículo (disponible / en ruta / mantenimiento / fuera de servicio) y su avance sobre la ruta asignada | `FleetController` + `TripProgressController` + `VehicleSimulationBackgroundService` + pestaña "Mapa y vehículos" del dashboard; la telemetría que publica la simulación la reciben observadores y la consulta `MonitoringController` (**Observer**, [8.18](#818-observer--publicar-la-telemetría-del-recorrido-sin-que-el-simulador-sepa-quién-escucha)) y el estado operativo de cada vehículo lo gobiernan sus estados (**State**, [8.19](#819-state--qué-puede-hacer-un-vehículo-según-su-estado)) |
| 2 | **Optimización de rutas y asignación de cargas** | Crear rutas con paradas y artículos de carga y asignar vehículo/conductor **respetando las reglas** (capacidad en kg y m³, disponibilidad, mantenimiento vencido, sin doble asignación); **reordenar las paradas** de una ruta para recorrer menos kilómetros; **repartir un lote de entregas** entre los vehículos disponibles con un plan que se compara con el reparto simple; estimar distancia y tiempo con la velocidad del tipo de vehículo | `DeliveryRoutesController` + `DeliveryRouteService` (**Builder**) + `AssignmentValidatingRouteService` (**Decorator**, [8.12](#812-decorator--reglas-de-asignación-sobre-el-servicio-de-rutas)) + `DispatchController` y `RouteOptimizationController` (**Strategy**, [8.11](#811-strategy--optimizar-paradas-y-repartir-cargas)) + pestaña "Rutas y cargas" (botones "🧭 Planificar reparto" y "🧭 Optimizar") |
| 3 | **Mantenimiento predictivo** | Registrar mantenimientos y detectar cuáles vehículos tienen tareas vencidas por fecha o kilometraje | `MaintenanceController` + `MaintenanceService.GetVehiclesDueForMaintenanceAsync()` + pestaña "Mantenimiento" |
| 4 | **Integración con sistemas de navegación** | El conductor puede abrir la ruta asignada en una app de navegación real | `NavigationController` + `NavigationService` (genera enlace de Google Maps) + botón "Abrir navegación" en el portal del operador |

Además, el sistema incluye:

- **Autenticación con roles** (`AuthController`, `AuthService`) — Administrador y Operador.
- **Dashboard administrativo** de una sola página (SPA) con mapa, gestión de vehículos, rutas, mantenimiento, alertas y auditoría.
- **Portal del operador**: el conductor ve su ruta asignada, la carga a transportar, puede reportar una complicación durante el viaje (avería, retraso, tráfico, accidente, clima) y abrir la navegación externa.
- **Análisis de la flota** (`AnalysisController`, `FleetAnalysisService`): utilización de los vehículos, recorridos y distancias, historial de mantenimiento y hallazgos operativos, calculados por visitantes sobre los datos reales (**Visitor**, [8.20](#820-visitor--analizar-la-flota-sin-cargar-las-entidades-con-análisis)). Solo lectura y sin pantalla propia en el frontend: se consulta por la API.

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
│   │   ├── Cargo/ ← COMPOSITE de la carga (sección 8.7) e ITERATOR que la recorre (sección 8.15)
│   │   ├── Entities/ (Vehicle, DeliveryRoute, Driver, AppUser, Waypoint,
│   │   │              CargoItem, MaintenanceRecord, MaintenancePlan,
│   │   │              NavigationProfile, TripAlert)
│   │   ├── States/     ← STATE: los cuatro estados operativos del vehículo, su contexto y las reglas de transición (sección 8.19)
│   │   └── Enums/Enums.cs
│   │
│   ├── FleetManagement.Application/
│   │   ├── Interfaces/Repositories/ (6 interfaces — DIP)
│   │   ├── Interfaces/Services/ (14 interfaces — DIP)
│   │   ├── Interfaces/ (IFleetAuditLogger, IPasswordHasher, ISessionTokenStore)
│   │   ├── DTOs/ (records de entrada/salida de la API)
│   │   ├── Factories/  ← FACTORY METHOD + ABSTRACT FACTORY
│   │   ├── Builders/   ← BUILDER
│   │   ├── Reports/    ← DECORATOR de reportes (generador base + decoradores, sección 8.8)
│   │   ├── Facades/    ← FACADE del resumen del dashboard (sección 8.8)
│   │   ├── Metrics/    ← FleetMetrics: cálculos que comparten el reporte y el resumen
│   │   ├── Geo/        ← GeoMath (Haversine, interpolación), TripEstimate (minutos = km / velocidad) y VehicleSpeed: compartidos por rutas, simulación y plan de reparto
│   │   ├── Assignment/ ← reglas de asignación (capacidad, disponibilidad, doble asignación) y el DECORATOR que las aplica (sección 8.12)
│   │   ├── Optimization/ ← STRATEGY: orden de las paradas, reparto de entregas y secuencia de cada viaje (sección 8.11)
│   │   ├── Commands/   ← COMMAND: órdenes que se pueden deshacer y rehacer, su invocador y el historial por usuario (sección 8.13); y el MEMENTO del orden de las paradas (sección 8.17)
│   │   ├── Mediators/  ← MEDIATOR: coordina el estado de la ruta con el de su vehículo (sección 8.16)
│   │   ├── Monitoring/ ← OBSERVER: eventos de telemetría, publicador, observadores y tablero en vivo (sección 8.18)
│   │   ├── Analysis/   ← VISITOR: elementos adaptadores, estructura de objetos y los cuatro visitantes de análisis (sección 8.20)
│   │   ├── AlertHandling/ ← CHAIN OF RESPONSIBILITY: validar, registrar, clasificar y escalar una alerta (sección 8.14)
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
│   │   ├── Commands/ (InMemoryCommandHistoryStore: un historial de órdenes por usuario, en memoria)
│   │   └── BackgroundServices/VehicleSimulationBackgroundService.cs  (temporizador de la simulación)
│   │
│   └── FleetManagement.Api/
│       ├── Program.cs                 ← Composition Root (Inyección de Dependencias)
│       ├── Controllers/ (18 controladores REST: los 16 de siempre + MonitoringController y AnalysisController)
│       ├── Middleware/TokenAuthenticationMiddleware.cs
│       └── Filters/RequireRoleAttribute.cs
│
├── tests/
│   └── FleetManagement.Application.Tests/     ← pruebas xUnit de todo el proyecto (sección 12)
│       ├── Factories/, Builders/, Prototype/, Adapter/, Bridge/, Composite/, Decorator/, ReportDecorator/, Facade/
│       ├── Metrics/, Flyweight/, Proxy/, Simulation/, Geo/, Architecture/, Support/
│       ├── Assignment/    ← reglas de asignación y su Decorator (sección 8.12)
│       ├── Optimization/  ← Strategy: orden de paradas, reparto, plan y optimización de una ruta (sección 8.11)
│       ├── Command/       ← Command: historial, invocador, órdenes de rutas, foto de paradas y eliminador de rutas (sección 8.13)
│       ├── AlertHandling/ ← Chain of Responsibility: cadena, manejadores, política de gravedad, opciones y decorador (sección 8.14)
│       ├── Iterator/      ← Iterator: recorrido de la carga y lista plana de artículos (sección 8.15)
│       ├── Mediator/      ← Mediator: participantes, reglas ruta-vehículo, diseño e integración con servicios reales (sección 8.16)
│       ├── Memento/       ← Memento: recuerdo, restauración, rechazos y su uso desde Command (sección 8.17)
│       ├── Observer/      ← Observer: publicador, observadores, tablero, eventos del simulador y diseño (sección 8.18)
│       ├── State/         ← State: estados, transiciones, compatibilidad con el Mediator y con los servicios reales (sección 8.19)
│       └── Visitor/       ← Visitor: elementos, estructura, los cuatro visitantes, servicio de análisis y diseño (sección 8.20)
│
└── frontend/
    ├── login.html
    ├── dashboard.html                 (panel administrativo — SPA)
    ├── operator.html                  (portal del conductor)
    ├── css/styles.css
    └── js/ (api.js, auth.js, dashboard.js, dispatch.js [plan de reparto y optimizar paradas], commands.js [deshacer y rehacer], operator.js)
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

Las reglas de las alertas (`AlertHandling:*`: límites de la descripción y del retraso, umbral de retraso grave y destinatarios
del escalamiento) se leen y se validan de la misma manera; sus claves y valores por defecto están en la
[sección 8.14](#814-chain-of-responsibility--de-la-alerta-reportada-a-quien-debe-atenderla).

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

Los patrones están implementados, **conectados a un caso de uso real** (no como código de
ejemplo aislado) y expuestos a través de la API —y, cuando corresponde, de la interfaz
gráfica— para que se puedan ejercitar de punta a punta. Las secciones 8.1 a 8.5 son los cinco
creacionales; de la 8.7 a la 8.17 los estructurales y los patrones de comportamiento anteriores; y
las **8.18, 8.19 y 8.20** incorporan **Observer, State y Visitor** y las **8.21 y 8.22**
**Template Method (hoja de ruta) e Interpreter (reglas como texto)**, sin reemplazar ninguno de los
anteriores. El Template Method de la 8.21 es **distinto** del que ya existía dentro de
`Application/Commands/RouteCreationCommand.cs`, que no se tocó.

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
| Builder (`IDeliveryRouteBuilder` + `DeliveryRouteDirector`) | `POST /api/routes` y `POST /api/routes/express` (el panel usa sus versiones con historial, `/api/commands/routes` y `/api/commands/routes/express`, que llaman al mismo servicio) | "+ Nueva ruta" / "⚡ Ruta express" |
| Prototype (`IPrototype<T>`) | `POST /api/fleet/vehicles/{id}/clone` y `POST /api/routes/{id}/duplicate` (el panel usa la versión con historial, `/api/commands/routes/{id}/duplicate`, que llama al mismo servicio) | ⧉ junto a cada vehículo / ruta |
| Adapter (`INavigationProvider` + `GoogleMapsAdapter`) | `GET /api/navigation/route/{routeId}` | Enlace de navegación de la ruta |
| Bridge (`FleetNotification` + `INotificationChannel`) | `POST /api/maintenance`, `POST /api/alerts`, `PATCH /api/fleet/vehicles/{id}/status` (el envío simulado aparece en la consola del backend) | — |
| Composite (`ICargoComponent` / `CargoGroup`) | `GET /api/routes/{id}/cargo-manifest` | — |
| Decorator (`NotificationDecorator`: Audit, Retry, Priority) | Los mismos endpoints que el Bridge; el resultado queda en `GET /api/audit/logs` | Pestaña "Auditoría" |
| Decorator de reportes (`FleetReportDecorator`: Statistics, MaintenanceAlerts, CargoSummary) | `GET /api/reports/fleet?sections=...` | — |
| Facade (`FleetDashboardFacade`) | `GET /api/dashboard/summary` | — |
| Flyweight (`IVehicleTypeProfileFactory` → `VehicleTypeProfile`) | `GET /api/fleet/vehicle-types`; además lo usa por dentro el simulador, cuyo resultado se ve en `GET /api/progress` | Marcadores del mapa (un ícono compartido por tipo y estado) |
| Proxy (`CachingNavigationProviderProxy`) | `GET /api/navigation/route/{routeId}` (el mismo endpoint del Adapter: la segunda consulta de la misma ruta sale del caché) | Enlace de navegación de la ruta |
| Strategy (`IRouteOptimizer`, `ICargoAssignmentStrategy`, `IStopSequencer`) | `POST /api/dispatch/plan`, `POST /api/dispatch/apply`, `GET` y `POST /api/routes/{id}/optimization` | "🧭 Planificar reparto" / "🧭 Optimizar" |
| Decorator de reglas de asignación (`AssignmentValidatingRouteService`) | `POST /api/routes`, `POST /api/routes/express`, `PATCH /api/routes/{id}/status` (responden 400 con los mensajes de las reglas incumplidas) | "+ Nueva ruta" (aviso rojo de capacidad), cambio de estado de la ruta |
| Command (`ICommand<T>` / `IUndoableCommand<T>` + `CommandInvoker`) | `POST /api/commands/routes`, `POST /api/commands/routes/express`, `POST /api/commands/routes/{id}/duplicate`, `POST /api/commands/routes/{id}/optimization`, `POST /api/commands/dispatch/apply`, `GET /api/commands/history`, `POST /api/commands/undo`, `POST /api/commands/redo` | «+ Nueva ruta», «⚡ Ruta express», «⧉ Duplicar», «Crear rutas del plan», «Aplicar este orden» y los botones «↶ Deshacer» / «↷ Rehacer» |
| Chain of Responsibility (`IAlertHandler` + `AlertHandlingTripAlertService`) | `POST /api/alerts` (la alerta pasa por la cadena; las graves y críticas generan un aviso de escalamiento visible en `GET /api/audit/logs`) | «🚨 Reportar complicación» del portal del conductor |
| Iterator (`ICargoIterator` + `CargoDepthFirstIterator`) | `GET /api/routes/{id}/cargo-items` (los mismos artículos del `cargo-manifest`, como lista plana en el orden del recorrido) | — |
| Mediator (`IFleetLifecycleMediator` + `IRouteColleague` / `IVehicleColleague`) | `PATCH /api/routes/{id}/status` (el vehículo asignado queda En ruta o Disponible según el estado) y `POST /api/alerts` con retraso (la ruta queda Retrasada) | Cambio de estado de la ruta y «🚨 Reportar complicación» |
| Memento (`IRouteMemento` / `RouteSnapshot` + `IRouteSnapshotService`) | `POST /api/commands/routes/{id}/optimization` guarda el recuerdo; `POST /api/commands/undo` y `POST /api/commands/redo` lo usan | «Aplicar este orden», «↶ Deshacer» y «↷ Rehacer» |
| Observer (`ITelemetryPublisher` / `ITelemetryObserver` + `VehicleTelemetryPublisher`) | `GET /api/monitoring/telemetry` y `GET /api/monitoring/telemetry/{vehicleId}` (lo último que publicó el simulador de cada vehículo); las paradas alcanzadas quedan en `GET /api/audit/logs` (categoría «Telemetría») | — |
| State (`IVehicleState` + `VehicleStateContext` + los cuatro estados) | `PATCH /api/fleet/vehicles/{id}/status` (responde 400 si el estado actual no permite el cambio), `POST /api/maintenance` (un vehículo en Mantenimiento sale del taller: es la única forma de dejarlo Disponible), `PATCH /api/routes/{id}/status` y `POST /api/routes` (el estado decide qué hace el vehículo al empezar o terminar el viaje y si puede usarse) | Cambio de estado del vehículo y de la ruta |
| Visitor (`IFleetVisitor` / `IFleetElement` + `FleetElementStructure`) | `GET /api/analysis/fleet` (utilización, recorridos y distancias, historial de mantenimiento y hallazgos operativos) | — |
| Template Method (`RouteSheetTemplate` + `DriverRouteSheet` / `DispatcherRouteSheet`, elegidas por `IRouteSheetProvider`) | `GET /api/route-sheets/{routeId}?audience=Driver` o `Dispatcher` (mismo esqueleto, contenido distinto; audiencia desconocida → 400 con los valores válidos). Es independiente del Template Method de `RouteCreationCommand` | «📄 Hoja de ruta» en cada ruta de «Rutas y cargas» (panel, con selector Conductor / Despachador) y en la barra de acciones del portal del operador |
| Interpreter (`IExpression<TResult>` + `IRuleContext`, con `RuleParser`) | `POST /api/rules/maintenance/evaluate` y `POST /api/rules/vehicles/filter` con `{ "rule": "..." }` (solo lectura, rol Admin; regla inválida → 400 con la posición del error) | — |

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

### 8.11 Strategy — optimizar paradas y repartir cargas

Requerimiento #2 (optimización de rutas y asignación de cargas). Hay tres decisiones que se pueden resolver de más de una
manera, y cada una es una **estrategia intercambiable** detrás de una interfaz: el Composition Root (`Program.cs`) elige
cuál se usa y los servicios no lo saben. Viven en `Application/Optimization/` y son **puras** (reciben datos y devuelven un
resultado, sin repositorios ni reloj), por eso se prueban sin infraestructura.

| Estrategia (interfaz) | Implementaciones | Qué decide |
|-----------------------|------------------|------------|
| `IRouteOptimizer` | `NearestNeighborRouteOptimizer` (vecino más cercano) · `TwoOptRouteOptimizer` (recibe otro optimizador como punto de partida y lo mejora con 2-opt) | En qué **orden** visitar unas paradas desde un origen, con o sin destino fijo. |
| `ICargoAssignmentStrategy` | `PriorityBestFitStrategy` (la del plan) · `FirstFitInOrderStrategy` (la línea base "sin optimizar") | A qué **vehículo** va cada entrega. |
| `IStopSequencer` | `PriorityStopSequencer` (urgentes primero, luego el camino más corto) · `GivenOrderStopSequencer` (el orden recibido) | En qué orden recorre cada vehículo sus entregas. |

`DispatchPlanner` junta las dos últimas (primero asigna, luego ordena). Registro en `Program.cs`:

```csharp
builder.Services.AddSingleton<IRouteOptimizer>(new TwoOptRouteOptimizer(new NearestNeighborRouteOptimizer()));
builder.Services.AddSingleton<ICargoAssignmentStrategy, PriorityBestFitStrategy>();
builder.Services.AddSingleton<IStopSequencer>(provider => new PriorityStopSequencer(provider.GetRequiredService<IRouteOptimizer>()));
builder.Services.AddSingleton(provider => new DispatchPlanner(
    provider.GetRequiredService<ICargoAssignmentStrategy>(),
    provider.GetRequiredService<IStopSequencer>()));
```

Cambiar de algoritmo es cambiar esas líneas (por ejemplo, un optimizador con distancias por carretera implementaría
`IRouteOptimizer` sin tocar servicios ni controladores: Open/Closed).

**Qué hace cada algoritmo.**

- *Orden de las paradas.* El vecino más cercano va siempre a la parada pendiente más próxima (Haversine, línea recta).
  El 2-opt toma ese recorrido y, mientras exista un tramo que puesto al revés lo acorte, lo invierte; el origen (y el
  destino, si lo hay) quedan fijos. Solo acepta cambios que ahorran, así que **nunca devuelve algo más largo que su punto
  de partida**.
- *Reparto de entregas (best fit con prioridades).* Las entregas salen de una cola de prioridad (`PriorityQueue`): primero
  las urgentes y, entre iguales, las más pesadas. Cada una va al vehículo **ya cargado** al que mejor le calza (el que queda
  con menos espacio libre), para consolidar y usar menos vehículos; si en ninguno cabe, abre el vehículo disponible que antes
  llega al punto de salida (cercanía y velocidad de su tipo, perfil Flyweight). Si en ninguno cabe, la entrega queda **sin
  asignar, con su motivo**. Se respetan el peso (kg) y el volumen (m³), este último solo si el vehículo declara su capacidad.
- *Secuencia de cada viaje.* Las urgentes se visitan primero (entre sí, por el camino más corto) y después las demás.
  Priorizar lo urgente puede alargar el recorrido: es una decisión de negocio y el plan lo explica cuando ocurre.

**Qué se puede hacer con esto.**

| Caso de uso | Servicio | Endpoint | UI |
|-------------|----------|----------|----|
| Reordenar las paradas de una ruta ya creada | `RouteOptimizationService` | `GET /api/routes/{id}/optimization` (propone, no cambia nada) · `POST` (aplica; solo rutas planificadas) | Botón "🧭 Optimizar" en la tabla de rutas: lista actual frente a propuesta, kilómetros, minutos y mapa |
| Repartir un lote de entregas entre los vehículos disponibles | `DispatchPlanningService` | `POST /api/dispatch/plan` (calcula) · `POST /api/dispatch/apply` (crea las rutas) | Botón "🧭 Planificar reparto" |

Reglas del plan de reparto:

- **Solo salen vehículos que pueden salir:** que no figuren "en ruta", sin viaje activo, que no estén en mantenimiento ni
  fuera de servicio y sin mantenimiento vencido (la misma regla que se aplicará al crear la ruta, [8.12](#812-decorator--reglas-de-asignación-sobre-el-servicio-de-rutas)).
  Los demás se devuelven en `excludedVehicles`, cada uno con su motivo.
- **Se compara con el reparto simple** (`FirstFitInOrderStrategy` + `GivenOrderStopSequencer`: cada entrega al primer
  vehículo donde cabe, en el orden recibido): vehículos usados, kilómetros y minutos antes y después. La comparación solo es
  válida (`comparable`) si los dos planes asignan la misma cantidad de entregas; si no, se dice por qué no lo es. Si el plan
  optimizado recorre *más* que el simple (por atender primero lo urgente, por ejemplo), `comparison.note` lo explica en vez
  de esconderlo.
- **Calcular no guarda nada.** `apply` vuelve a calcular el plan, **comprueba todos los viajes antes de crear el primero**
  (todo o nada: no queda un reparto a medias) y crea una ruta **planificada** por viaje (origen = punto de salida, paradas =
  entregas menos la última, destino = la última entrega, carga = las entregas). No inicia los viajes, no cambia el estado de
  los vehículos y no asigna conductor.
- El tiempo estimado usa la velocidad del tipo de vehículo ([8.9](#89-flyweight--perfiles-compartidos-por-tipo-de-vehículo)), la misma que usa la simulación.
- Un plan admite como máximo 200 entregas.

**Ejemplo con la flota de demostración** (4 entregas en Santander desde Bucaramanga, 85 kg en total): el plan usa **1 vehículo
(TRK-001) y 35,95 km (27 min)**; el reparto simple usaría 2 vehículos y 48,53 km (36,4 min), o sea **12,58 km (25,9 %) y 9,4
min menos**. Quedan excluidos CAR-003 (mantenimiento vencido), VAN-002 (viaje activo) y VAN-005 (fuera de servicio). En el
mismo ejemplo, reordenar una ruta que visitaba las paradas en zig-zag pasa de 69,64 a 50,73 km (18,91 km, 27,2 % menos).

**Qué tan buena es la heurística (medido con un script aparte, no es una prueba del proyecto).** Contra el mejor orden
posible (probando todas las permutaciones), en 200 instancias aleatorias de 7 paradas:

| | Vecino más cercano | Vecino más cercano + 2-opt |
|---|---|---|
| Con destino fijo: recorrido medio ÷ óptimo (peor caso) | 1,129 (1,493) | **1,008** (1,132) |
| Con destino fijo: instancias en que halla el óptimo | 26 de 200 | **148 de 200** |
| Sin destino: recorrido medio ÷ óptimo (peor caso) | 1,067 (1,315) | **1,012** (1,195) |
| Sin destino: instancias en que halla el óptimo | 66 de 200 | **151 de 200** |

El 2-opt acorta mucho lo que deja el vecino más cercano y no lo empeora nunca, pero **no garantiza el óptimo**. Por eso
`RouteOptimizationService` solo propone un cambio si ahorra al menos 0,01 km: una ruta que ya estaba en su mejor orden no se
empeora (hay una prueba con rutas puestas a propósito en su mejor orden, donde la heurística sí habría empeorado
el recorrido).

**Un comportamiento a conocer (decisión de negocio, no un error).** Con capacidad escasa, el reparto lleva primero lo
urgente y lo pesado, así que puede dejar sin asignar más entregas *pequeñas* que el orden simple. Con una sola furgoneta de
55 kg y entregas de 11, 54, 20, 26, 42, 4, 47, 35 y 4 kg, el plan lleva la de 54 kg (8 quedan sin asignar) y el orden
simple lleva cuatro pequeñas (39 kg, 5 sin asignar). En el lote sembrado de 100 casos de las pruebas, el plan transportó más
kilos en total (11.424 frente a 10.953 kg: más en 29 casos, menos en 3, igual en 68) y asignó todo en los 64 casos en que el
orden simple también lo asignó todo, pero dejó **más entregas sin asignar en 15 casos** (y menos en 2). Lo que no se lleva
siempre se informa con su motivo.

### 8.12 Decorator — reglas de asignación sobre el servicio de rutas

Las reglas de negocio de la asignación (qué vehículo puede llevar qué carga) viven en **un solo lugar** y se comprueban
**antes** de crear o iniciar una ruta, sin modificar la lógica de `DeliveryRouteService` (solo se le añadió la velocidad
por tipo de vehículo y la liberación del vehículo descrita abajo).

| Pieza | Dónde está | Papel |
|-------|------------|-------|
| `IDeliveryRouteService` | `Application/Interfaces/Services/` (ya existía) | Interfaz del **componente**: la que usan los controladores y el simulador. |
| `DeliveryRouteService` | `Application/Services/` | **Componente real** (Builder, Prototype, estimación de distancia y tiempo). |
| `AssignmentValidatingRouteService` | `Application/Assignment/` | **Decorator**: valida y luego delega en el servicio real, que no se entera. Lo demás lo delega tal cual. |
| `IRouteAssignmentValidator` / `RouteAssignmentValidator` | `Application/Assignment/` | Las reglas. Cada una es un método pequeño; el validador reúne **todas** las infracciones y lanza una sola `AssignmentRuleException` con la lista. |

En `Program.cs` se registra el servicio real y `IDeliveryRouteService` se construye como el decorator que lo envuelve, así
que **todo** el que lo pida (controladores y simulador) recibe la versión protegida:

```csharp
builder.Services.AddScoped<IRouteAssignmentValidator, RouteAssignmentValidator>();
builder.Services.AddScoped<DeliveryRouteService>();
builder.Services.AddScoped<IDeliveryRouteService>(provider => new AssignmentValidatingRouteService(
    provider.GetRequiredService<DeliveryRouteService>(),
    provider.GetRequiredService<IRouteAssignmentValidator>()));
```

**Reglas.**

| Cuándo | Qué se comprueba |
|--------|------------------|
| Al crear una ruta (`POST /api/routes`, `POST /api/routes/express`) con vehículo | Que el vehículo exista · que no esté en mantenimiento ni fuera de servicio · que no tenga mantenimiento vencido (lo decide `IMaintenanceService`, el módulo predictivo) · que el **peso** total de la carga quepa en `CapacityKg` · que el **volumen** quepa en `CapacityM3` (si el vehículo lo declara) |
| Al crear una ruta con conductor | Que el conductor exista |
| Al iniciar un viaje (`PATCH /api/routes/{id}/status` a `InProgress` o `Delayed`) | Que el vehículo siga operativo y sin mantenimiento vencido · que no esté en **otro viaje activo** · que el conductor no esté en otro viaje activo |

- Si se incumple alguna, la API responde **400** con todos los mensajes juntos (por ejemplo, *"La carga (2000 kg) supera la
  capacidad del vehículo VAN-002 (1500 kg)."*) y no se crea ni cambia nada. Una carga exactamente igual a la capacidad es válida.
- **Terminar o cancelar un viaje nunca se bloquea.** Al hacerlo solo se libera al vehículo que figuraba "en ruta": uno que
  entretanto pasó a mantenimiento o fuera de servicio conserva ese estado. Tampoco se libera si el vehículo sigue en **otro**
  viaje activo (en curso o retrasado): se corrigió al agregar Command ([8.13](#813-command--deshacer-y-rehacer-operaciones-de-rutas)),
  porque cancelar una ruta planificada podía dejar «disponible» a un vehículo que seguía ocupado en otro viaje (un defecto que ya
  existía; deshacer una creación ya no cancela la ruta, la elimina).
- `CapacityM3` es un dato nuevo de `Vehicle` (camión 40 m³, furgoneta 10, auto 3,5, moto 0,3; las fábricas y los datos de
  demostración lo traen, y el Prototype lo copia). El formulario de nueva ruta pide el volumen de cada artículo y avisa en
  rojo, antes de enviar, si la carga supera la capacidad del vehículo elegido; el servidor lo valida de todos modos.
- Duplicar una ruta (Prototype) **no** vuelve a validar al crear la copia (queda planificada, igual que el original); las
  reglas de salida sí se aplican cuando la copia se inicia.

### 8.13 Command — deshacer y rehacer operaciones de rutas

Las operaciones del módulo de rutas que se pueden deshacer se piden como **órdenes** que se guardan en un historial y se
pueden **deshacer y rehacer**: *crear una ruta* (por cualquiera de sus tres caminos: el formulario «+ Nueva ruta», «⚡ Ruta
express» o «⧉ Duplicar»), *aplicar el orden optimizado de las paradas* y *aplicar el plan de reparto*. Cada orden es un objeto
que sabe ejecutarse, deshacerse y rehacerse; un **invocador** las ejecuta y las anota en el historial de quien las pidió.
**Los endpoints de siempre no cambian** (`POST /api/routes`, `POST /api/routes/express`, `POST /api/routes/{id}/duplicate`,
`POST /api/routes/{id}/optimization` y `POST /api/dispatch/apply` hacen lo mismo que antes y no dejan historial): las órdenes
viven en un controlador aparte, `api/commands`, y el panel administrativo usa ese para todas las operaciones de arriba
(«+ Nueva ruta», «⚡ Ruta express», «⧉ Duplicar», «Crear rutas del plan» y «Aplicar este orden»). Si algún día se agrega otro
camino para crear rutas, también debe pasar por `api/commands`: si no, esa ruta no entra al historial y «Deshacer» no la ve.

| Pieza | Dónde está | Papel en el patrón |
|-------|------------|--------------------|
| `ICommand<TResult>` | `Application/Commands/` | **Command**: `ExecuteAsync()` y nada más. |
| `IUndoableAction` | `Application/Commands/` | Lo que el historial necesita de cualquier orden, sin saber qué devuelve: descripción, si cambió algo (`HasEffect`), `UndoAsync()` y `RedoAsync()`. |
| `IUndoableCommand<TResult>` | `Application/Commands/` | Una orden que se ejecuta **y** se puede deshacer (junta las dos anteriores). |
| `RouteCreationCommand` (clase base abstracta) | `Application/Commands/` | **Template Method**: lo que es igual en toda orden que *crea* una ruta —recordar cuál creó, `HasEffect`, deshacer (= eliminarla) y rehacer (= crear otra nueva)— se escribe **una sola vez**; cada subclase solo dice *cómo* se crea (`CreateAsync`) y cómo se describe. |
| `CreateRouteCommand`, `CreateExpressRouteCommand`, `DuplicateRouteCommand`, `ApplyRouteOptimizationCommand`, `ApplyDispatchPlanCommand` | `Application/Commands/` | **Concrete Commands**. Las tres primeras heredan de `RouteCreationCommand` (formulario, «⚡ express» y «⧉ duplicar»). Cada una usa los servicios que ya existían (los **Receivers**): no repite su lógica. |
| `ICommandInvoker` / `CommandInvoker` | `Application/Commands/` | **Invoker**: ejecuta, deshace y rehace; no sabe qué hace cada orden. |
| `IRouteCommandFactory` / `RouteCommandFactory` | `Application/Commands/` | Arma cada orden con sus dependencias, para que el controlador no las conozca. |
| `CommandHistory` | `Application/Commands/` | El historial de **un** usuario: lo que puede deshacer y rehacer (20 operaciones como máximo), con exclusión para que dos peticiones suyas no se pisen. |
| `ICommandHistoryStore` / `InMemoryCommandHistoryStore` | `Application/Commands/` / `Infrastructure/Commands/` | Un historial por usuario, en memoria. |
| `RouteSnapshot` / `IRouteMemento` / `IRouteSnapshotService` | `Application/Commands/` | **Memento** ([8.17](#817-memento--volver-al-orden-anterior-de-las-paradas)): la "foto" del orden de las paradas y de la distancia y el tiempo estimados, tomada antes de optimizar. |
| `IPlannedRouteDeleter` / `PlannedRouteDeleter` | `Application/Commands/` | Elimina las rutas que creó una orden: todas o ninguna, y solo si siguen planificadas; deja constancia en la auditoría. |
| `CommandNotUndoableException` / `CommandNotRedoableException` | `Application/Commands/` | El estado cambió y ya no se puede deshacer / rehacer (la ruta ya salió, o ya no existe): la API responde 409 y el invocador quita la operación del historial. |
| `CommandsController` | `Api/Controllers/` | HTTP: traduce los resultados y los errores. |
| `commands.js` y los botones «↶ Deshacer» / «↷ Rehacer» | `frontend/` | Muestran el historial del usuario y piden deshacer o rehacer. |

```mermaid
classDiagram
    direction LR
    class CommandsController {
        +POST routes
        +POST routes/express
        +POST routes/id/duplicate
        +POST routes/id/optimization
        +POST dispatch/apply
        +GET history
        +POST undo
        +POST redo
    }
    class IRouteCommandFactory {
        <<interface>>
        +CreateRoute(request)
        +CreateExpressRoute(request)
        +DuplicateRoute(routeId, request)
        +ApplyOptimization(routeId)
        +ApplyDispatchPlan(request)
    }
    class ICommandInvoker {
        <<interface>>
        +ExecuteAsync(user, command)
        +UndoAsync(user)
        +RedoAsync(user)
        +GetHistoryAsync(user)
    }
    class CommandInvoker
    class ICommandHistoryStore {
        <<interface>>
        +For(user) CommandHistory
    }
    class InMemoryCommandHistoryStore
    class CommandHistory {
        +Undoable
        +Redoable
        +Record(action)
    }
    class ICommand~TResult~ {
        <<interface>>
        +ExecuteAsync() TResult
    }
    class IUndoableAction {
        <<interface>>
        +Description
        +HasEffect
        +UndoAsync()
        +RedoAsync()
    }
    class IUndoableCommand~TResult~ {
        <<interface>>
    }
    class RouteCreationCommand {
        <<abstract>>
        #CreateAsync()
    }
    class CreateRouteCommand
    class CreateExpressRouteCommand
    class DuplicateRouteCommand
    class ApplyRouteOptimizationCommand
    class ApplyDispatchPlanCommand
    class IDeliveryRouteService {
        <<interface>>
        Receiver existente
    }
    class IRouteOptimizationService {
        <<interface>>
        Receiver existente
    }
    class IDispatchPlanningService {
        <<interface>>
        Receiver existente
    }
    class IRouteSnapshotService {
        <<interface>>
        Memento del orden de paradas
    }
    class IPlannedRouteDeleter {
        <<interface>>
        Elimina todas o ninguna
    }

    CommandsController --> ICommandInvoker
    CommandsController --> IRouteCommandFactory
    IRouteCommandFactory ..> IUndoableCommand : crea
    ICommandInvoker <|.. CommandInvoker
    CommandInvoker --> ICommandHistoryStore
    ICommandHistoryStore <|.. InMemoryCommandHistoryStore
    InMemoryCommandHistoryStore o-- CommandHistory : uno por usuario
    CommandHistory o-- IUndoableAction
    ICommand <|-- IUndoableCommand
    IUndoableAction <|-- IUndoableCommand
    IUndoableCommand <|.. RouteCreationCommand
    RouteCreationCommand <|-- CreateRouteCommand
    RouteCreationCommand <|-- CreateExpressRouteCommand
    RouteCreationCommand <|-- DuplicateRouteCommand
    IUndoableCommand <|.. ApplyRouteOptimizationCommand
    IUndoableCommand <|.. ApplyDispatchPlanCommand
    RouteCreationCommand --> IPlannedRouteDeleter
    CreateRouteCommand --> IDeliveryRouteService
    CreateExpressRouteCommand --> IDeliveryRouteService
    DuplicateRouteCommand --> IDeliveryRouteService
    ApplyRouteOptimizationCommand --> IRouteOptimizationService
    ApplyRouteOptimizationCommand --> IRouteSnapshotService
    ApplyDispatchPlanCommand --> IDispatchPlanningService
    ApplyDispatchPlanCommand --> IPlannedRouteDeleter
```

**Qué significa deshacer y rehacer en cada orden.**

| Orden | Deshacer | Rehacer | Se niega a deshacer (409) si… |
|-------|----------|---------|-------------------------------|
| Crear una ruta (formulario, «⚡ express» o «⧉ duplicar») | **Elimina** la ruta creada (al duplicar, la **copia**; la original no se toca). No la deja «Cancelada»: desaparece de la lista, así que deshacer y rehacer una y otra vez no acumulan rutas. La auditoría conserva lo que pasó. | Crea una ruta **nueva** (con otro identificador) con los mismos datos y con las reglas de hoy; al duplicar, vuelve a duplicar la ruta original, que debe seguir existiendo. | la ruta creada ya no existe o ya no está planificada (ya salió, se completó o se canceló a mano). |
| Aplicar el orden optimizado | Restaura el orden de las paradas y la distancia y el tiempo estimados que tenía antes (la "foto"). | Vuelve a optimizar con las reglas de hoy; si la ruta ya no existe, no se puede rehacer (409). | la ruta ya no existe, ya no está planificada o sus paradas cambiaron desde entonces. |
| Aplicar el plan de reparto | **Elimina todas** las rutas que creó, o ninguna. | Vuelve a aplicar el plan con la flota de ese momento: puede dar otro reparto o fallar con el motivo (400). | alguna de esas rutas ya no está planificada o ya no existe. |

**Reglas del historial.**

- Es **por usuario** (por nombre de usuario, no por sesión: otro inicio de sesión del mismo usuario ve el mismo historial), vive en
  memoria y guarda las **20** operaciones más recientes; se pierde al reiniciar la API.
- Una orden que falla (regla de asignación incumplida, ruta inexistente…) **no queda** en el historial, y una que no cambió
  nada (por ejemplo, optimizar una ruta que ya está en su mejor orden) tampoco.
- Una operación nueva descarta lo que se había deshecho: ya no se puede rehacer (como en un editor de texto).
- Si deshacer **o rehacer** ya no es posible porque lo que la operación necesita cambió o ya no existe (la ruta salió, o se eliminó
  al deshacer su creación), la API responde **409** con el motivo y **quita esa operación del historial**, para que no bloquee a las
  anteriores ni deje un botón atascado. Una falla pasajera (que no es un cambio de estado, por ejemplo una regla de asignación que hoy
  no se cumple) la deja para reintentar.
- Una ruta eliminada **no se recupera**: rehacer su creación no la devuelve, crea otra ruta con los mismos datos y otro identificador.
  Por eso, si una operación posterior actuaba sobre la ruta eliminada (por ejemplo, optimizarla), esa operación ya no se puede rehacer.
- Dos peticiones del mismo usuario a la vez (dos «deshacer» seguidos) se atienden una tras otra: no pueden deshacer lo mismo dos veces.
- Todo queda en la bitácora con el usuario, en la categoría «Comando» (*Ejecutó / Deshizo / Rehízo / No se pudo deshacer / No se pudo rehacer*); la ruta eliminada deja además «Ruta 'X' eliminada al deshacer su creación» en la categoría «Ruta».

**Endpoints** (todos solo para administradores: 401 sin sesión y 403 con un operador):

| Método | Ruta | Qué hace | Respuestas |
|--------|------|----------|------------|
| POST | `/api/commands/routes` | Igual que `POST /api/routes` y deja la operación en el historial. | 201 con la ruta (`Location` hacia `GET /api/routes/{id}`) · 400 reglas de asignación o datos inválidos |
| POST | `/api/commands/routes/express` | Igual que `POST /api/routes/express` y deja la operación en el historial. | 201 con la ruta (`Location` hacia `GET /api/routes/{id}`) · 400 reglas de asignación o datos inválidos |
| POST | `/api/commands/routes/{id}/duplicate` | Igual que `POST /api/routes/{id}/duplicate` y deja la operación en el historial. | 201 con la copia (`Location` hacia `GET /api/routes/{id}`) · 404 ruta inexistente |
| POST | `/api/commands/routes/{id}/optimization` | Igual que `POST /api/routes/{id}/optimization`. | 200 · 404 ruta inexistente · 400 ya no está planificada |
| POST | `/api/commands/dispatch/apply` | Igual que `POST /api/dispatch/apply`. | 200 · 400 datos inválidos o nada que asignar |
| GET | `/api/commands/history` | `{ "undoable": [...], "redoable": [...] }`, de lo más reciente a lo más antiguo, cada una con `description` y `executedAt`. | 200 |
| POST | `/api/commands/undo` | Deshace la última operación del usuario (crear una ruta la elimina). | 200 `{ "description", "history" }` · 400 nada que deshacer · 409 ya no se puede |
| POST | `/api/commands/redo` | Rehace la última operación deshecha. | 200 · 400 nada que rehacer · 409 ya no se puede (y se quita del historial) |

En el panel, los botones **«↶ Deshacer»** y **«↷ Rehacer»** de la pestaña *Rutas y cargas* están apagados cuando no hay nada que
deshacer o rehacer; al pasar el cursor muestran qué operación se va a deshacer o rehacer. Si el historial no se puede leer (por
ejemplo, porque la API que está corriendo es una versión anterior y no tiene `/api/commands/history`), los botones también
quedan apagados, pero el texto de ayuda lo dice —«No se pudo leer el historial (Error 404)…»— y se deja un aviso en la consola
del navegador: no se confunde con «todavía no hay nada que deshacer».

**Cómo cumple SOLID y qué se evitó.**

| Principio | En este patrón |
|-----------|----------------|
| **S** | Cada pieza tiene un solo motivo de cambio: la orden sabe su operación, el invocador ejecuta y anota, el historial guarda, la fábrica arma, el eliminador elimina, el controlador traduce HTTP. |
| **O** | Una operación nueva que se pueda deshacer es **una clase nueva** (`IUndoableCommand<T>`), un método en la fábrica y un endpoint; el invocador y el historial no cambian (una prueba comprueba que no conocen ninguna orden concreta). Un camino nuevo para crear rutas es una subclase pequeña de `RouteCreationCommand` (cómo crear y cómo describir), sin tocar la base. |
| **L** | El invocador funciona con cualquier `IUndoableCommand<T>`: sus pruebas usan órdenes de mentira que no tocan rutas ni vehículos. |
| **I** | `ICommand<T>` (ejecutar) y `IUndoableAction` (lo que pide el historial) son interfaces pequeñas y separadas; el historial no necesita saber el tipo del resultado. |
| **D** | Las órdenes dependen de interfaces de servicios, nunca de repositorios ni de clases concretas (una prueba de arquitectura lo vigila); el almacén de historiales es una interfaz y `Program.cs` es el único que elige la implementación. |

Smells evitados a propósito: ningún `switch` ni `if (orden is X)` en el invocador; ninguna clase que lo haga todo; ninguna lógica
repetida (las órdenes reutilizan los servicios existentes, así que las reglas de asignación del Decorator de la
[sección 8.12](#812-decorator--reglas-de-asignación-sobre-el-servicio-de-rutas) y la estrategia de la
[sección 8.11](#811-strategy--optimizar-paradas-y-repartir-cargas) se aplican igual al crear, al rehacer y al planificar); ningún
estado compartido entre usuarios; lo que comparten las tres órdenes que crean rutas (recordar cuál crearon, deshacer y rehacer) se escribió una sola vez en `RouteCreationCommand` en lugar de copiarlo tres veces; y deshacer **elimina** de verdad la ruta creada en vez de dejarla «Cancelada» (para eso el repositorio de rutas ganó un `DeleteAsync`, como el de vehículos): una ruta cancelada se acumulaba cada vez que se deshacía y se rehacía.

**Qué no se tocó.** Singleton, Factory Method, Abstract Factory, Builder + Director, Prototype, Adapter, Bridge, Composite,
los Decorators, el Facade, el Flyweight, el Proxy y Strategy siguen exactamente como estaban. Las órdenes llaman al
`IDeliveryRouteService` protegido por el Decorator y reutilizan el Builder y las estrategias a través de los servicios
existentes. Los tres únicos cambios en código existente, por si se quieren revisar: (1) `DeliveryRouteService` ya no libera a un vehículo
al cancelar o completar una ruta si ese vehículo sigue ocupado en **otra** ruta activa (un defecto que ya existía), (2) el texto del
estado de la ruta, que antes era un método privado de `RouteOptimizationService`, pasó a `RouteStatusText` para que lo compartan sin
cambiar ningún mensaje, y (3) `IDeliveryRouteRepository` y su implementación en memoria ganaron `DeleteAsync` (solo lo usa «Deshacer»;
no hay ningún endpoint para borrar rutas; por eso el repositorio de rutas de prueba de `Simulation/SimulationFakes.cs` ganó la línea que lo implementa).

### 8.14 Chain of Responsibility — de la alerta reportada a quien debe atenderla

Cuando un operador reporta una alerta (`POST /api/alerts`), el reporte pasa por una **cadena de manejadores**: se valida, se
registra, se clasifica por gravedad y se **escala** a quien corresponde (o se deja abierta). Cada manejador hace lo suyo y decide
si el reporte sigue o lo da por atendido; quien reporta solo conoce al primero.

```mermaid
flowchart LR
    R([POST /api/alerts]) --> V["1. ValidateAlertReportHandler<br/>descripción y retraso válidos"]
    V -- no cumple --> X(["400 · se corta la cadena<br/>no se registra nada"])
    V --> G["2. RegisterAlertHandler<br/>TripAlertService de siempre<br/>(guarda · retraso en la ruta · aviso a operaciones)"]
    G --> C["3. ClassifySeverityHandler<br/>Minor · Serious · Critical"]
    C --> E1{"4. Escalate<br/>¿Critical?"}
    E1 -- sí --> A["aviso a la administración<br/>prioridad Critical"]
    E1 -- no --> E2{"5. Escalate<br/>¿Serious?"}
    E2 -- sí --> S["aviso al supervisor<br/>prioridad High"]
    E2 -- no --> U["6. UnescalatedAlertHandler<br/>queda abierta, sin escalar"]
```

| # | Manejador | Qué hace | ¿Corta la cadena? |
|---|-----------|----------|-------------------|
| 1 | `ValidateAlertReportHandler` | Rechaza una descripción vacía o de más de 500 caracteres y un retraso de más de 1440 minutos (`AlertRejectedException` → 400). | Sí, si rechaza: no se registra nada. |
| 2 | `RegisterAlertHandler` | Registra la alerta con el `TripAlertService` **de siempre**: valida tipo y ruta, la guarda, refleja el retraso en la ruta y avisa a operaciones (Bridge + Decorators). | No |
| 3 | `ClassifySeverityHandler` | Le pone gravedad según `AlertSeverityPolicy`. | No |
| 4 | `EscalateAlertHandler` (críticas) | Avisa a la administración con prioridad `Critical` y lo deja en la bitácora («Escalamiento»). | Sí: la alerta ya fue atendida. |
| 5 | `EscalateAlertHandler` (graves) | Avisa al supervisor con prioridad `High`. | Sí |
| 6 | `UnescalatedAlertHandler` | Deja constancia de que no requiere escalamiento: queda abierta. | Sí (es el último) |

Los dos manejadores de escalamiento son **la misma clase** con distinto nivel, prioridad y destinatario: agregar un nivel no
obliga a escribir otra clase.

**Gravedad y qué pasa con cada una.**

| Gravedad | Cuándo | Qué pasa |
|----------|--------|----------|
| `Critical` | Accidente (`Accident`), con cualquier retraso. | Aviso a `administrador@fleetcontrol.demo`, prioridad `Critical`. |
| `Serious` | Avería (`Breakdown`), o **cualquier otro tipo** con un retraso de 60 minutos o más. | Aviso a `supervisor@fleetcontrol.demo`, prioridad `High`. |
| `Minor` | Todo lo demás. | Queda registrada y **abierta**, sin escalar. |

Solo se **clasifica y escala**: ninguna alerta se resuelve sola (`PATCH /api/alerts/{id}/resolve` sigue siendo manual) y
escalar no cambia su estado. El aviso a operaciones que ya enviaba el servicio de alertas sigue enviándose en todos los casos;
el escalamiento es un aviso adicional. Los avisos usan el mismo Bridge y los mismos Decorators (prioridad, reintentos y
auditoría) que ya existían.

**Configuración.** Las reglas no están escritas dentro de los manejadores: se leen de la sección `AlertHandling` y se
**validan al arrancar**, igual que `Simulation` y `NavigationCache` ([sección 5.3](#53-configuración-de-la-simulación-y-del-caché-de-navegación)).

| Clave | Por defecto | Qué controla |
|-------|-------------|--------------|
| `AlertHandling:MaxDescriptionLength` | `500` | Máximo de caracteres de la descripción (1 a 10 000). |
| `AlertHandling:MaxDelayMinutes` | `1440` | Máximo de minutos de retraso en una alerta (1 a 100 000). |
| `AlertHandling:SeriousDelayMinutes` | `60` | Desde cuántos minutos de retraso una alerta, sea del tipo que sea, es grave (1 a 100 000). |
| `AlertHandling:SupervisorRecipient` | `supervisor@fleetcontrol.demo` | A quién se escalan las graves (no puede estar vacío). |
| `AlertHandling:AdministratorRecipient` | `administrador@fleetcontrol.demo` | A quién se escalan las críticas (no puede estar vacío). |

Por ejemplo, `AlertHandling__SeriousDelayMinutes=30` (el doble guion bajo equivale a `:`) hace graves los retrasos desde 30 minutos.

**Cómo está armado.**

```mermaid
classDiagram
    direction TB
    class ITripAlertService {
        <<interface>>
        +CreateAlertAsync(request)
        +GetAllAsync()
        +GetByRouteAsync(routeId)
        +ResolveAlertAsync(id)
    }
    class TripAlertService {
        Servicio real, sin cambios
    }
    class AlertHandlingTripAlertService {
        Decorator: el reporte pasa por la cadena
    }
    class IAlertHandler {
        <<interface>>
        +SetNext(next) IAlertHandler
        +HandleAsync(context)
    }
    class AlertHandler {
        <<abstract>>
        +HandleAsync(context)
        #CanHandle(context)
        #ConsumesTheReport
        #ProcessAsync(context)*
    }
    class ValidateAlertReportHandler
    class RegisterAlertHandler
    class ClassifySeverityHandler
    class EscalateAlertHandler {
        nivel, prioridad y destinatario como parámetros
    }
    class UnescalatedAlertHandler
    class AlertReportContext {
        Request
        Alert
        Severity
        Steps
    }
    class AlertSeverityPolicy
    class AlertHandlingOptions {
        Límites, umbral y destinatarios
    }

    ITripAlertService <|.. TripAlertService
    ITripAlertService <|.. AlertHandlingTripAlertService
    AlertHandlingTripAlertService --> ITripAlertService : delega lo demás
    AlertHandlingTripAlertService --> IAlertHandler : primer eslabón
    IAlertHandler <|.. AlertHandler
    AlertHandler --> IAlertHandler : siguiente
    AlertHandler <|-- ValidateAlertReportHandler
    AlertHandler <|-- RegisterAlertHandler
    AlertHandler <|-- ClassifySeverityHandler
    AlertHandler <|-- EscalateAlertHandler
    AlertHandler <|-- UnescalatedAlertHandler
    RegisterAlertHandler --> ITripAlertService : el real, no el decorador
    ClassifySeverityHandler --> AlertSeverityPolicy
    ValidateAlertReportHandler --> AlertHandlingOptions
    AlertSeverityPolicy --> AlertHandlingOptions
    AlertHandler ..> AlertReportContext
```

- `IAlertHandler` (`SetNext` y `HandleAsync`) es el contrato; `AlertHandler` es la base común: guarda al sucesor y decide en un
  solo lugar qué pasa con el reporte. Cada manejador solo dice si le corresponde (`CanHandle`), qué hace (`ProcessAsync`) y si con
  eso lo da por atendido (`ConsumesTheReport`).
- `AlertReportContext` es lo que va de mano en mano: el reporte, la alerta ya registrada, su gravedad y una línea por cada
  paso (útil para entender qué pasó). Pedir algo que ningún manejador anterior dejó es un error de armado de la cadena, no un valor vacío.
- `AlertHandlerChain.Of(...)` enlaza una lista en orden, y `DefaultAlertHandlerChain.Create(...)` es el **único lugar** donde
  está el orden de producción (validar → registrar → clasificar → críticas → graves → dejar abierta); las pruebas usan esa
  misma cadena, no una copia.
- `AlertHandlingTripAlertService` es un **Decorator** de `ITripAlertService`: `CreateAlertAsync` pasa por la cadena y lo demás
  (`GetAllAsync`, `GetByRouteAsync`, `ResolveAlertAsync`) se delega tal cual. `TripAlertService`, su Bridge y sus Decorators no
  cambiaron. Se registra igual que se hizo con las reglas de asignación:

```csharp
builder.Services.AddScoped<TripAlertService>();
builder.Services.AddScoped<ITripAlertService>(provider =>
{
    var alerts = provider.GetRequiredService<TripAlertService>();
    var chain = DefaultAlertHandlerChain.Create(
        provider.GetRequiredService<AlertHandlingOptions>(),
        alerts,
        provider.GetRequiredService<INotificationChannel>(),
        provider.GetRequiredService<IFleetAuditLogger>());
    return new AlertHandlingTripAlertService(alerts, chain);
});
```

- `RegisterAlertHandler` recibe el servicio **sin** la cadena: si recibiera el decorador, se llamaría a sí mismo sin fin.
- No confundir con `NotificationChain` (`Notifications/Decorators/`): esa clase solo sirve para recorrer los decoradores de una
  notificación y no tiene relación con esta cadena.

**Para agregar un paso** (por ejemplo, avisar por SMS a un tercero) se escribe una clase que herede de `AlertHandler` y se pone
en `DefaultAlertHandlerChain`; ningún otro manejador cambia (la prueba `ANewHandler_CanBeInsertedBetweenTheExistingOnes_WithoutChangingThem`
lo demuestra).

**Cómo cumple SOLID y qué se evitó.**

| Principio | En este patrón |
|-----------|----------------|
| **S** | Cada manejador tiene una sola tarea (validar, registrar, clasificar, escalar, dejar abierta); la gravedad la decide `AlertSeverityPolicy` y los límites salen de `AlertHandlingOptions`. |
| **O** | Un paso nuevo es una clase nueva y una línea en `DefaultAlertHandlerChain`; no se modifica ningún manejador ni `TripAlertService`. |
| **L** | Cualquier `IAlertHandler` puede ir en cualquier lugar de la cadena; la base garantiza que un manejador que no puede atender un reporte nunca lo corta. |
| **I** | `IAlertHandler` tiene dos métodos; los manejadores reciben solo lo que usan (opciones, política, canal, bitácora). |
| **D** | Los manejadores dependen de interfaces (`ITripAlertService`, `INotificationChannel`, `IFleetAuditLogger`), no de `TripAlertService` (una prueba de arquitectura lo vigila); la cadena se arma en un solo lugar. |

Smells evitados a propósito: el `if`/`switch` gigante por gravedad dentro de un método (cada nivel es un manejador), los números
y destinatarios escritos dentro de las clases (salen de la configuración) y una cadena "mágica" armada en varios sitios.

**Qué cambia para quien usaba `POST /api/alerts`.** Una alerta válida responde igual (200 con la alerta). Lo nuevo: ahora se
rechaza con **400** y un mensaje claro una descripción vacía o solo con espacios (antes se registraba), una de más de 500
caracteres y un retraso de más de 1440 minutos (antes se aceptaban). Lo demás no cambia: sin el campo `description` sigue
respondiendo 400 con el aviso estándar de ASP.NET, una ruta inexistente responde 404, un tipo inválido 400 y un retraso negativo
se guarda como 0. Además, las alertas graves y críticas generan un aviso adicional (visible en la consola del backend y en
`GET /api/audit/logs`).

### 8.15 Iterator — recorrer la carga de una ruta sin conocer su estructura

La carga de una ruta es un árbol (el **Composite** de la [sección 8.7](#87-patrones-estructurales-adapter-bridge-composite-y-decorator)): la
ruta contiene grupos «Prioridad X» y cada grupo contiene artículos. Quien necesita **los artículos uno por uno** —por ejemplo, para
mostrarlos en una lista plana— no debería tener que saber cuántos niveles tiene el árbol ni cómo guarda sus hijos. Para eso está el
**Iterator**: el cliente solo pregunta `HasNext()` y pide `Next()`.

**Qué ya existía y qué se agregó.** El método `Traverse()` de `CargoComponentExtensions` ya recorría el árbol en profundidad con
`yield return`. Es un iterador *interno* (la propia colección entrega los elementos) y sigue exactamente igual: lo usa `CargoGroup.Add`
para impedir ciclos. Lo que no había era el patrón de forma explícita: un objeto que lleve el estado del recorrido y que el cliente
maneje a su ritmo (un iterador *externo*). Eso es lo nuevo, y está en el mismo módulo del Composite (`Domain/Cargo/`).

| Participante | Clase | Qué hace |
|--------------|-------|----------|
| **Iterator** | `ICargoIterator` | El contrato: `HasNext()` (no consume nada) y `Next()` (entrega el siguiente y avanza; si ya no queda nada lanza `InvalidOperationException`). |
| **Concrete Iterator** | `CargoDepthFirstIterator` | Recorre en profundidad (pre-orden) con una pila propia, sin recursión. Entrega cada componente **una sola vez** y acepta un filtro opcional que no cambia el orden. |
| **Aggregate** | `ICargoComponent` (`CargoGroup`, `CargoItemComponent`) | El Composite de siempre: **no cambió**. |
| **CreateIterator** | `CargoIteratorExtensions` (`CreateIterator()`, `CreateItemIterator()`) | Métodos de extensión: para pedir un iterador no hace falta tocar el Composite. |
| **Client** | `CargoManifestService.GetCargoItemsAsync` | Obtiene la lista plana de artículos de una ruta pidiendo «el siguiente», sin saber cuántos niveles hay. |

```mermaid
classDiagram
    direction LR
    class ICargoIterator {
        <<interface>>
        +HasNext() bool
        +Next() ICargoComponent
    }
    class CargoDepthFirstIterator {
        -Stack pending
        -HashSet visited
        -Func accepts
    }
    class ICargoComponent {
        <<interface>>
        Composite sin cambios
    }
    class CargoGroup
    class CargoItemComponent
    class CargoIteratorExtensions {
        <<static>>
        +CreateIterator(root) ICargoIterator
        +CreateItemIterator(root) ICargoIterator
    }
    class CargoManifestService {
        +GetManifestAsync(routeId)
        +GetCargoItemsAsync(routeId)
    }
    ICargoIterator <|.. CargoDepthFirstIterator
    ICargoComponent <|.. CargoGroup
    ICargoComponent <|.. CargoItemComponent
    CargoGroup o-- ICargoComponent : hijos
    CargoDepthFirstIterator ..> ICargoComponent : lee Children
    CargoIteratorExtensions ..> CargoDepthFirstIterator : crea
    CargoManifestService ..> CargoIteratorExtensions : pide el iterador
    CargoManifestService ..> ICargoIterator : HasNext y Next
```

**Qué cumple.**

- **Profundidad y orden:** entrega los componentes en el mismo orden que `Traverse()` (una prueba los compara en árboles anidados).
- **Grupos y artículos:** `CreateIterator()` entrega todo; `CreateItemIterator()` solo los artículos. Un artículo es un componente sin hijos
  que cuenta como carga, así que un grupo vacío no se confunde con uno.
- **Sin duplicados:** un mismo componente agregado a dos grupos se entrega una vez, y una implementación propia de `ICargoComponent` que
  formara un ciclo no deja el recorrido en un bucle infinito (`CargoGroup.Add` ya impide los ciclos; esto es una segunda defensa).
  Dos artículos *distintos* con el mismo nombre sí se entregan los dos.
- **Estructuras vacías:** un grupo sin hijos entrega solo a sí mismo; con el filtro de artículos no entrega nada; `HasNext()` es `false`.
- **Sin recursión:** un árbol de miles de niveles no desborda la pila de llamadas.
- **No modifica nada:** solo **lee** `Children`.

**Dónde se usa: la lista plana de una ruta.** El manifiesto jerárquico (`GET /api/routes/{id}/cargo-manifest`) sigue igual. Se agregó un
endpoint **adicional** con los mismos artículos como lista plana, en el orden del recorrido en profundidad del manifiesto:

```
GET /api/routes/{id}/cargo-items
{
  "routeId": "…", "itemCount": 6, "totalWeightKg": 70.75, "totalVolumeM3": 1.08,
  "items": [
    { "position": 1, "description": "Cajas estándar A", "weightKg": 30,  "volumeM3": 0.5,  "priority": "Standard" },
    { "position": 2, "description": "Cajas estándar C", "weightKg": 5,   "volumeM3": 0.05, "priority": "Standard" },
    { "position": 3, "description": "Repuestos alta",   "weightKg": 20,  "volumeM3": 0.2,  "priority": "High" },
    { "position": 4, "description": "Documento urgente", "weightKg": 1,  "volumeM3": 0.01, "priority": "Urgent" },
    …
  ]
}
```

Una ruta sin carga responde 200 con la lista vacía y los totales en cero; una ruta inexistente, 404 con `{ "message": "Ruta no encontrada." }`
(igual que el manifiesto). `position` es el lugar del artículo en el recorrido, empezando en 1; `priority` es la del artículo (en el
manifiesto jerárquico se deduce del grupo que lo contiene); los pesos y volúmenes se redondean a 3 decimales como allí. El frontend no
cambió: no consume este endpoint.

**Convive con lo anterior sin tocarlo.** El Composite (`ICargoComponent`, `CargoGroup`, `CargoItemComponent`, `CargoManifest`,
`CargoComponentExtensions`) conserva su código: el Iterator solo se apoya en su contrato. `CargoManifestService` conserva
`GetManifestAsync` y agrega un método; `ICargoManifestService` agrega esa firma (no hay otras implementaciones ni dobles de esa interfaz).
Ningún DTO ni endpoint existente cambió.

### 8.16 Mediator — coordinar el estado de la ruta con el del vehículo

Cuando una ruta cambia de estado, su vehículo debe acompañarla: al salir queda «En ruta» y al terminar queda «Disponible» (si no sigue
ocupado en otro viaje). Esa coordinación estaba escrita dentro de `DeliveryRouteService.UpdateRouteStatusAsync`, que además de cambiar la
ruta tocaba directamente al vehículo, y `TripAlertService` cambiaba la ruta por su cuenta al registrar un retraso. El **Mediator** reúne esa
coordinación en un solo lugar: quien cambia la ruta no conoce al vehículo, quien cambia al vehículo no conoce la ruta, y se hablan a través
del mediador. Las reglas son **exactamente las de antes**; lo que cambió es quién las aplica.

| Rol | Clase | Responsabilidad |
|-----|-------|-----------------|
| **Mediator** | `IFleetLifecycleMediator` | El contrato: dos avisos, «la ruta cambió de estado» y «la ruta quedó retrasada por una alerta». |
| **Concrete Mediator** | `FleetLifecycleMediator` | Las reglas que relacionan el estado de la ruta con el del vehículo (tabla de abajo). Depende solo de `IVehicleColleague`. |
| **Colleague de rutas** | `IRouteColleague` / `RouteColleague` | Es dueño del estado de la ruta: lo guarda y se lo comunica al mediador. También responde a la pregunta «¿ese vehículo sigue en otro viaje activo?». No conoce al participante de vehículos. |
| **Colleague de vehículos** | `IVehicleColleague` / `VehicleColleague` | Es dueño de la disponibilidad del vehículo: lo deja «En ruta» o lo libera si corresponde. No conoce al participante de rutas ni al mediador. |
| **Quienes inician** | `DeliveryRouteService`, `TripAlertService` | Validan la petición, piden el cambio al participante de rutas y siguen auditando y notificando como antes. Ya no tocan al vehículo ni se conocen entre sí. |

```mermaid
classDiagram
    direction LR
    class IFleetLifecycleMediator {
        <<interface>>
        +RouteStatusChangedAsync(sender, route)
        +RouteDelayReportedAsync(sender, route)
    }
    class FleetLifecycleMediator
    class IRouteColleague {
        <<interface>>
        +ChangeStatusAsync(route, newStatus)
        +ReportDelayAsync(route, delayMinutes)
        +HasOtherActiveTripAsync(vehicleId, exceptRouteId)
    }
    class RouteColleague
    class IVehicleColleague {
        <<interface>>
        +MarkEnRouteAsync(vehicleId)
        +ReleaseAfterTripAsync(vehicleId, hasOtherActiveTrip)
    }
    class VehicleColleague
    class DeliveryRouteService
    class TripAlertService
    class IDeliveryRouteRepository {
        <<interface>>
    }
    class IVehicleRepository {
        <<interface>>
    }
    IFleetLifecycleMediator <|.. FleetLifecycleMediator
    IRouteColleague <|.. RouteColleague
    IVehicleColleague <|.. VehicleColleague
    DeliveryRouteService --> IRouteColleague : cambia el estado
    TripAlertService --> IRouteColleague : reporta el retraso
    RouteColleague --> IFleetLifecycleMediator : avisa
    FleetLifecycleMediator --> IVehicleColleague : le ordena
    FleetLifecycleMediator ..> IRouteColleague : le pregunta (llega en cada aviso)
    RouteColleague --> IDeliveryRouteRepository
    VehicleColleague --> IVehicleRepository
```

**Qué pasa con el vehículo** (las cuatro reglas de negocio de siempre):

| La ruta pasa a… | El vehículo asignado… |
|-----------------|-----------------------|
| **En progreso** o **Retrasada** | queda **En ruta** (regla 1). |
| **Completada** o **Cancelada** | queda **Disponible** solo si iba En ruta **y** ninguna *otra* ruta suya está En progreso o Retrasada (regla 2). Uno en **Mantenimiento** o **Fuera de servicio** conserva su estado: terminar una ruta no lo repara (regla 3). |
| **Planificada** | no cambia. |
| (sin vehículo asignado, o el vehículo ya no existe) | no hay nada que coordinar: la ruta cambia igual y no se produce ningún error (regla 4). |

```mermaid
sequenceDiagram
    participant API as PATCH status
    participant S as DeliveryRouteService
    participant R as RouteColleague
    participant M as FleetLifecycleMediator
    participant V as VehicleColleague
    API->>S: UpdateRouteStatusAsync(id, Completed)
    S->>S: valida que la ruta existe y que el estado es válido
    S->>R: ChangeStatusAsync(route, Completed)
    R->>R: guarda la ruta
    R->>M: RouteStatusChangedAsync(this, route)
    M->>R: HasOtherActiveTripAsync(vehicleId, route.Id)
    R-->>M: false
    M->>V: ReleaseAfterTripAsync(vehicleId, false)
    V->>V: EnRoute pasa a Available (los demás estados se conservan)
    S->>S: audita «Ruta cambió su estado a Completed»
```

**Cómo es una comunicación real y no una fachada.** El participante de rutas inicia la conversación (avisa al mediador); el mediador le
*pregunta* algo al participante que avisó (¿sigue en otro viaje?) y con la respuesta *ordena* al de vehículos; los dos participantes nunca
se hablan. Si mañana hubiera un tercer participante (por ejemplo, el conductor), solo se agregaría al mediador.

**Lo que no cambió.**

- **Auditoría:** cada cambio de estado deja la **misma línea de siempre**, una sola vez («Ruta '…' cambió su estado a …»), la escribe
  `DeliveryRouteService` y el mediador no agrega ninguna propia ni ningún aviso.
- **Errores:** ruta inexistente (404, «Ruta no encontrada.») y estado inválido (400) se rechazan antes de que el mediador intervenga.
- **Decorator de reglas de asignación** ([8.12](#812-decorator--reglas-de-asignación-sobre-el-servicio-de-rutas)): sigue envolviendo a
  `DeliveryRouteService` y validando antes de iniciar un viaje.
- **Chain of Responsibility, Bridge y Decorators de avisos** ([8.14](#814-chain-of-responsibility--de-la-alerta-reportada-a-quien-debe-atenderla)):
  la cadena sigue procesando la alerta y `TripAlertService` sigue enviando su aviso por el mismo puente y los mismos decoradores.
- **Facade del dashboard:** solo lee; su resumen refleja lo que el mediador dejó en la flota.

**Decisión conservada: el retraso que reporta una alerta.** `TripAlertService` siempre marcó la ruta como Retrasada y sumó los minutos
**sin tocar al vehículo ni auditar un cambio de estado**, y así sigue (ahora a través de `RouteColleague.ReportDelayAsync`, que se lo
comunica al mediador con `RouteDelayReportedAsync`; este, a propósito, no hace nada con el vehículo). Es distinto de pedir el estado
`Delayed` por la API, que sí deja al vehículo En ruta. Es una diferencia que **ya existía**; se conservó para no cambiar efectos
secundarios, y una prueba la fija. Si se decide que la regla 1 también debe aplicar a las alertas, el cambio es una línea en
`FleetLifecycleMediator.RouteDelayReportedAsync` (llamar a `_vehicles.MarkEnRouteAsync`) y ajustar esa prueba.

**Qué servicios no son participantes, y por qué.** `VehicleService` (cambio manual del estado de un vehículo) y `MaintenanceService`
(registrar un mantenimiento) se revisaron: no coordinan nada con las rutas, así que meterlos en el mediador sería una abstracción
innecesaria. El participante de vehículos tampoco usa `IVehicleService` a propósito: ese servicio audita y notifica cada cambio, y el
ajuste automático por el ciclo de la ruta nunca lo hizo.

**Registro en `Program.cs`** (por petición, como los servicios que los usan; sin referencias circulares:
servicios → participante de rutas → mediador → participante de vehículos → repositorio):

```csharp
builder.Services.AddScoped<IVehicleColleague, VehicleColleague>();
builder.Services.AddScoped<IFleetLifecycleMediator, FleetLifecycleMediator>();
builder.Services.AddScoped<IRouteColleague, RouteColleague>();
```

`DeliveryRouteService` y `TripAlertService` reciben un parámetro nuevo, `IRouteColleague`; las pruebas que los construyen a mano lo arman con
`Support/FleetMediators.cs` (10 archivos de prueba y de apoyo solo cambiaron esa línea). No se instaló ningún paquete NuGet (ni MediatR).

### 8.17 Memento — volver al orden anterior de las paradas

Optimizar una ruta reordena sus paradas y cambia la distancia y el tiempo estimados. Para poder **deshacerlo** hace falta recordar cómo
estaba la ruta antes, sin que quien guarda ese recuerdo tenga que conocer sus detalles ni pueda alterarlo. Eso es el **Memento**. Ya existía
en el proyecto como parte del Command ([8.13](#813-command--deshacer-y-rehacer-operaciones-de-rutas)); en esta versión se revisó, se
fortaleció y se documenta como patrón independiente. **No se creó otro sistema de recuerdos.**

| Rol | Clase | Responsabilidad |
|-----|-------|-----------------|
| **Originator** | `RouteSnapshotService` (`IRouteSnapshotService`) | Crea el recuerdo (`CaptureAsync`) y es el único que sabe restaurarlo (`RestoreStopOrderAsync`). Es un servicio y no la entidad `DeliveryRoute` para no agregarle comportamiento de deshacer ni exponer sus paradas. |
| **Memento** (interfaz ancha) | `RouteSnapshot` | La foto: la posición de cada parada (`WaypointPosition`: id de la parada y su orden) y la distancia y el tiempo estimados. Nada más. |
| **Memento** (interfaz estrecha) | `IRouteMemento` | Lo único que ve quien solo lo guarda: de qué ruta es (`RouteId`, `RouteName`). |
| **Caretaker** | `ApplyRouteOptimizationCommand` | Guarda el recuerdo en un campo `IRouteMemento` y se lo devuelve al Originator al deshacer; no lo abre, no lo modifica y no sabe reconstruir nada. |

```mermaid
classDiagram
    direction LR
    class ApplyRouteOptimizationCommand {
        -IRouteMemento before
        +ExecuteAsync()
        +UndoAsync()
        +RedoAsync()
    }
    class IRouteMemento {
        <<interface>>
        +RouteId Guid
        +RouteName string
    }
    class RouteSnapshot {
        <<sealed>>
        +Stops IReadOnlyList
        +EstimatedDistanceKm double
        +EstimatedDurationMinutes double
    }
    class WaypointPosition {
        <<record>>
        +WaypointId Guid
        +Order int
    }
    class IRouteSnapshotService {
        <<interface>>
        +CaptureAsync(routeId) RouteSnapshot
        +RestoreStopOrderAsync(memento)
    }
    class RouteSnapshotService
    class CommandInvoker
    IRouteMemento <|.. RouteSnapshot
    RouteSnapshot *-- WaypointPosition
    IRouteSnapshotService <|.. RouteSnapshotService
    RouteSnapshotService ..> RouteSnapshot : crea y lee
    ApplyRouteOptimizationCommand --> IRouteMemento : solo lo guarda
    ApplyRouteOptimizationCommand --> IRouteSnapshotService : captura y restaura
    CommandInvoker --> ApplyRouteOptimizationCommand : historial de deshacer y rehacer
```

**Cómo se cumple cada requisito.**

| Requisito | Cómo |
|-----------|------|
| Estado anterior de las paradas | `Stops`: una `WaypointPosition(WaypointId, Order)` por parada. Se identifican por id y no por etiqueta (las etiquetas pueden repetirse). |
| Distancia y duración | `EstimatedDistanceKm` y `EstimatedDurationMinutes`; vuelven exactas. |
| Captura independiente de cambios posteriores | El recuerdo copia valores: las posiciones son inmutables, la lista es una copia propia de solo lectura (no se puede convertir en una lista modificable) y no guarda la entidad ni referencias a ella. |
| Restaurar cuando se puede | Solo si la ruta sigue **planificada** y con **las mismas paradas**: reasigna el orden, reordena la lista, restaura las estimaciones y guarda. |
| Rechazar cuando ya no cumple | `CommandNotUndoableException` (la API responde **409**) si la ruta ya no existe, ya salió o cambió de paradas; se verifica **antes** de cambiar nada (todo o nada). `ArgumentException` si el recuerdo no lo creó este servicio y `ArgumentNullException` si es nulo. |
| Compatible con Deshacer y Rehacer | Cada ejecución (también «rehacer») toma un recuerdo nuevo, así que deshacer vuelve siempre al estado inmediatamente anterior a esa ejecución, también tras varias vueltas. Si no se puede restaurar, el invocador quita la orden del historial. |
| Sin información innecesaria | No guarda la entidad, coordenadas, etiquetas, carga ni el estado de la ruta: solo lo que cambia al optimizar. |

**Qué se fortaleció en esta versión** (el diseño ya era correcto; se cerraron los huecos de encapsulamiento):

- **`IRouteMemento` (nueva):** la interfaz estrecha. `ApplyRouteOptimizationCommand` declara con ella su campo y sus variables, así que ya
  no puede leer las paradas ni las estimaciones; `IRouteSnapshotService.RestoreStopOrderAsync` recibe un `IRouteMemento`.
- **`RouteSnapshot`:** pasó de `record` a clase sellada con constructor **`internal`**: nadie fuera de la capa puede fabricar un recuerdo
  con datos a medida ni copiarlo con cambios (`with`), y no tiene setters.
- **`RouteSnapshotService`:** rechaza un recuerdo que no creó y uno nulo, sin tocar la ruta.
- **Pruebas:** la carpeta `Memento/` (35) lo demuestra como patrón independiente y junto con Command; incluye pruebas de arquitectura que
  vigilan que el Caretaker solo lo vea por la interfaz estrecha.

**Qué no cambió.** `CaptureAsync` sigue devolviendo `RouteSnapshot`; los endpoints de Command y sus respuestas son los mismos (deshacer una
optimización de una ruta que ya salió sigue respondiendo 409 con el motivo); las pruebas anteriores de `RouteSnapshotServiceTests`,
`RouteCommandsTests` y `CommandInvokerTests` no se modificaron.

**Cómo se ve por la API** (con la API en marcha y la sesión de un administrador; el historial es por usuario):

```bash
# crear una ruta con paradas A, B, C por Command, optimizarla, deshacer y rehacer
POST /api/commands/routes                       → ruta con paradas A→B→C · 155.67 km · 169.8 min
POST /api/commands/routes/{id}/optimization     → paradas B→C→A · 66.72 km · 72.8 min      (el Caretaker guarda el recuerdo)
POST /api/commands/undo                         → paradas A→B→C · 155.67 km · 169.8 min    (el Originator lo restaura)
POST /api/commands/redo                         → paradas B→C→A · 66.72 km · 72.8 min
```


### 8.18 Observer — publicar la telemetría del recorrido sin que el simulador sepa quién escucha

Cada paso de la simulación mueve a los vehículos que están en viaje. Hasta ahora, quien quería enterarse de ese movimiento tenía que
**preguntarlo** (`TripProgressService` calcula el avance de los viajes activos cuando se le consulta) y nada dejaba constancia de los
hitos del recorrido (paradas alcanzadas) ni conservaba lo último que se supo de un vehículo cuando su viaje terminaba. Agregar eso
dentro de `VehicleMovementSimulator` habría hecho que el simulador conociera la auditoría y cualquier consumidor futuro. El
**Observer** invierte esa dependencia: el simulador **publica** eventos de telemetría y los observadores registrados los reciben
sin que el simulador sepa cuántos son ni qué hacen.

#### 1. Descripción del patrón

Observer define una dependencia de uno a muchos: cuando el **sujeto** (*Subject*) publica un evento, todos los **observadores**
(*Observers*) registrados lo reciben automáticamente. El sujeto solo conoce la interfaz del observador; los observadores se registran
y se retiran en tiempo de ejecución. Aquí el sujeto es `VehicleTelemetryPublisher`, los eventos son los tres hitos de un recorrido
(movimiento, parada alcanzada y llegada) y los observadores concretos son el auditor de paradas (`TripAuditObserver`) y el tablero
en vivo (`LiveTelemetryBoard`).

#### 2. Justificación de su utilización

| Problema real del proyecto | Cómo lo resuelve el Observer |
|----------------------------|------------------------------|
| Solo el simulador sabe cuándo termina el paso de un viaje y qué recorrió; quien quiera reaccionar tendría que ser llamado desde él | El simulador publica **un** evento por hito y no conoce a los consumidores (`ITelemetryPublisher.Publish`). |
| Hay dos consumidores con responsabilidades distintas (dejar constancia de las paradas y mantener lo último conocido de cada vehículo) y puede haber más | Cada uno es un observador independiente; agregar otro es registrarlo en `Program.cs`, sin tocar al publicador ni al simulador (Open/Closed). |
| `TripProgressService` solo informa los viajes **activos** y recalcula en cada consulta | El tablero guarda lo **último publicado**: sigue visible al terminar el viaje (`arrived: true`) y recuerda la última parada alcanzada, sin leer repositorios. |
| Un observador que falla, o que publica a su vez, no debe detener la simulación ni provocar bucles | El publicador aísla los fallos (los audita), serializa la entrega con una cola y corta los ciclos. |

**Qué le toca a cada patrón (no se duplican responsabilidades):**

| Patrón | Responsabilidad que **conserva** | Qué hace el Observer en esa frontera |
|--------|----------------------------------|--------------------------------------|
| **Bridge** ([8.7](#87-patrones-estructurales-adapter-bridge-composite-y-decorator)) | Separar las notificaciones de sus canales de envío (correo, SMS, push). | Nada: no envía mensajes ni conoce canales. Ningún observador usa `INotificationChannel`. |
| **Mediator** ([8.16](#816-mediator--coordinar-el-estado-de-la-ruta-con-el-del-vehículo)) | Coordinar el estado de la ruta con el del vehículo. | Nada: no cambia estados ni coordina participantes. Los observadores solo **reciben** hechos ya ocurridos. |
| **Observer** | Distribuir los eventos de monitoreo del recorrido. | Es lo único que hace. |

#### 3. Ubicación de la implementación

| Archivo | Qué contiene |
|---------|--------------|
| `src/FleetManagement.Application/Monitoring/TelemetryEvents.cs` | El evento base `TelemetryEvent` y sus tres tipos: `VehicleMovedEvent`, `TripStopReachedEvent`, `TripArrivedEvent` (records inmutables). |
| `src/FleetManagement.Application/Monitoring/ITelemetryObserver.cs` | Contrato del observador (un solo método). |
| `src/FleetManagement.Application/Monitoring/ITelemetryPublisher.cs` | Lado del sujeto que usa el simulador: solo publicar. |
| `src/FleetManagement.Application/Monitoring/ITelemetrySubject.cs` | Lado del sujeto que registra observadores: `Subscribe`, `Unsubscribe`, `ObserverCount`. |
| `src/FleetManagement.Application/Monitoring/VehicleTelemetryPublisher.cs` | El sujeto concreto: registro, cola de entrega, aislamiento de fallos, corte de ciclos. |
| `src/FleetManagement.Application/Monitoring/TripAuditObserver.cs` | Observador concreto 1: deja en la auditoría cada parada alcanzada. |
| `src/FleetManagement.Application/Monitoring/LiveTelemetryBoard.cs` y `ILiveTelemetryBoard.cs` | Observador concreto 2 y modelo de lectura que consulta la API. |
| `src/FleetManagement.Application/Simulation/TripTelemetryEvents.cs` | Función pura que traduce un paso de la simulación en sus eventos. |
| `src/FleetManagement.Application/DTOs/TelemetryDtos.cs` | `VehicleLiveStatusDto`: lo que devuelve la API. |
| `src/FleetManagement.Api/Controllers/MonitoringController.cs` | `GET api/monitoring/telemetry` y `GET api/monitoring/telemetry/{vehicleId}`. |
| **Modificados:** `src/FleetManagement.Application/Simulation/VehicleMovementSimulator.cs`, `src/FleetManagement.Api/Program.cs` | Ver la sección 7 de este apartado (cambios mínimos y compatibles). |
| Pruebas: `tests/FleetManagement.Application.Tests/Observer/` | 6 archivos de pruebas y `ObserverTestDoubles.cs` (dobles escritos a mano). |

#### 4. Diagrama UML

```mermaid
classDiagram
    direction LR
    class ITelemetryPublisher {
        <<interface>>
        +Publish(telemetry)
    }
    class ITelemetrySubject {
        <<interface>>
        +ObserverCount int
        +Subscribe(observer) IDisposable
        +Unsubscribe(observer) bool
    }
    class VehicleTelemetryPublisher {
        <<sealed>>
        +MaxReentrantPublishes int
        -observers copia inmutable
        -pending ConcurrentQueue
    }
    class ITelemetryObserver {
        <<interface>>
        +OnTelemetry(telemetry)
    }
    class TripAuditObserver {
        <<sealed>>
    }
    class LiveTelemetryBoard {
        <<sealed>>
    }
    class ILiveTelemetryBoard {
        <<interface>>
        +GetAll() IReadOnlyList
        +Get(vehicleId) VehicleLiveStatusDto
    }
    class TelemetryEvent {
        <<abstract record>>
        +VehicleId
        +LicensePlate
        +RouteId
        +OccurredAt
    }
    class VehicleMovedEvent {
        <<record>>
    }
    class TripStopReachedEvent {
        <<record>>
    }
    class TripArrivedEvent {
        <<record>>
    }
    class TripTelemetryEvents {
        <<static>>
        +ForStep(trip, path, speed, previousKm, currentKm, at)
    }
    class VehicleMovementSimulator
    class MonitoringController
    class IFleetAuditLogger {
        <<interface>>
    }
    ITelemetryPublisher <|.. VehicleTelemetryPublisher
    ITelemetrySubject <|.. VehicleTelemetryPublisher
    ITelemetryObserver <|.. TripAuditObserver
    ITelemetryObserver <|.. LiveTelemetryBoard
    ILiveTelemetryBoard <|.. LiveTelemetryBoard
    TelemetryEvent <|-- VehicleMovedEvent
    TelemetryEvent <|-- TripStopReachedEvent
    TelemetryEvent <|-- TripArrivedEvent
    VehicleTelemetryPublisher o-- ITelemetryObserver : notifica a todos
    VehicleMovementSimulator --> ITelemetryPublisher : publica
    VehicleMovementSimulator ..> TripTelemetryEvents : pide los eventos del paso
    TripAuditObserver --> IFleetAuditLogger
    MonitoringController --> ILiveTelemetryBoard : consulta
```

```mermaid
sequenceDiagram
    participant S as VehicleMovementSimulator
    participant E as TripTelemetryEvents
    participant P as VehicleTelemetryPublisher
    participant A as TripAuditObserver
    participant B as LiveTelemetryBoard
    participant API as GET api/monitoring/telemetry
    S->>S: guarda el movimiento y, si llegó, completa la ruta
    S->>E: ForStep(viaje, poligonal, velocidad, km antes, km después, hora)
    E-->>S: [movimiento, parada alcanzada, llegada]
    loop cada evento, en ese orden
        S->>P: Publish(evento)
        P->>A: OnTelemetry(evento)
        A->>A: si es una parada, audita «El vehículo ... alcanzó la parada ...»
        P->>B: OnTelemetry(evento)
        B->>B: reemplaza la entrada del vehículo (registro inmutable)
    end
    API->>B: GetAll()
    B-->>API: última telemetría de cada vehículo, por placa
```

#### 5. Interfaces y clases participantes

| Rol del patrón | Clase o interfaz | Responsabilidad |
|----------------|------------------|-----------------|
| **Subject** (lado de publicar) | `ITelemetryPublisher` | Lo único que necesita quien emite telemetría: `Publish`. El simulador depende de esta interfaz y no puede registrar ni retirar observadores. |
| **Subject** (lado de registrar) | `ITelemetrySubject` | Registrar (`Subscribe`, devuelve un `IDisposable`), retirar (`Unsubscribe`) y contar observadores. Segregado del anterior (ISP). |
| **Concrete Subject** | `VehicleTelemetryPublisher` | Guarda el registro de observadores y les distribuye cada evento. No decide qué hace cada observador, no envía notificaciones por canales ni coordina vehículos con rutas. |
| **Observer** | `ITelemetryObserver` | `OnTelemetry(TelemetryEvent)`: un solo método. Cada observador decide por tipo qué eventos le interesan. |
| **Concrete Observer 1** | `TripAuditObserver` | Audita únicamente las paradas alcanzadas (categoría «Telemetría»). No audita cada movimiento (llenaría el historial, que guarda 1000 eventos) ni la llegada (`DeliveryRouteService` ya audita «Ruta … cambió su estado a Completed»). |
| **Concrete Observer 2** | `LiveTelemetryBoard` | Mantiene la última telemetría conocida de cada vehículo (`ConcurrentDictionary` de registros inmutables). Es además el modelo de lectura (`ILiveTelemetryBoard`) que consulta la API. |
| **Event** | `TelemetryEvent` y `VehicleMovedEvent`, `TripStopReachedEvent`, `TripArrivedEvent` | Lo que se comunica: posición GPS simulada, kilometraje y progreso; parada alcanzada; llegada. Inmutables: todos los observadores reciben la misma instancia y ninguno puede alterarla para los demás. |
| **Productor de eventos** | `TripTelemetryEvents.ForStep` | Traduce un paso de la simulación en sus eventos, con los mismos números que usa `TripProgressService` (`RoutePath.ProgressAt`). Es una función pura. |
| **Cliente del sujeto** | `VehicleMovementSimulator` | Publica al terminar el paso de cada viaje. |
| **Consulta** | `MonitoringController` | Lee `ILiveTelemetryBoard`; no publica ni se suscribe. |

#### 6. Funcionamiento dentro del sistema

**Qué se publica y cuándo.** Al terminar el paso de un viaje activo, el simulador publica, en este orden: (1) `VehicleMovedEvent`
—posición, kilometraje, progreso, próxima parada, km restantes y ETA—; (2) un `TripStopReachedEvent` por cada parada
intermedia que el vehículo cruzó en ese paso, en el orden del recorrido; (3) si llegó, un `TripArrivedEvent`. La publicación ocurre
**después** de guardar el movimiento y de completar la ruta, de modo que un observador nunca ve algo que todavía no está guardado.

```csharp
// VehicleMovementSimulator.cs — al final de AdvanceTripAsync, después de guardar el movimiento y de completar la ruta
// Se publica al final: solo se informa un paso que se completó (si la ruta no pudo completarse no hay llegada que avisar).
PublishTelemetry(trip, path, speedKmh, previousKm, currentKm);

private void PublishTelemetry(ActiveTrip trip, RoutePath path, double speedKmh, double previousKm, double currentKm)
{
    if (_telemetry is null)
        return;

    foreach (var telemetry in TripTelemetryEvents.ForStep(trip, path, speedKmh, previousKm, currentKm, _clock.GetUtcNow()))
        _telemetry.Publish(telemetry);
}
```

**Cómo evita los problemas típicos del patrón** (todos comprobados con pruebas, ver el punto 9):

| Riesgo | Cómo se evita |
|--------|---------------|
| **Suscripciones duplicadas** | Un observador queda registrado una sola vez, comparado **por referencia**: suscribirlo de nuevo no le duplica los eventos. Dos instancias distintas del mismo tipo sí son dos observadores. |
| **Suscripciones que nunca se liberan** | `Subscribe` devuelve un `IDisposable` que lo retira (una sola vez, aunque se libere dos veces). Además los observadores de la aplicación viven lo mismo que el publicador (ambos Singleton), así que no queda ninguno huérfano. |
| **Notificaciones recursivas** | Los eventos pasan por una cola y los entrega **un solo hilo a la vez**. Si un observador publica mientras recibe un evento, el nuevo evento espera en la cola y se entrega al terminar el actual: la pila no crece. Si los observadores se publican eventos entre sí más de `MaxReentrantPublishes` (1000) veces en una misma entrega (un ciclo), se descarta el exceso y se deja constancia en la auditoría (categoría «Monitoreo»). |
| **Concurrencia** | El registro es una **copia inmutable** que se reemplaza completa (copy-on-write): quien publica recorre la copia que leyó, sin candados (solo registrar y retirar usan un candado corto entre sí), y un observador puede suscribir o retirar a otro mientras recibe un evento sin romper la entrega en curso. Al haber un solo hilo entregando, ningún observador recibe dos eventos a la vez ni desordenados, y no necesita sincronizar su propio estado. El tablero usa un `ConcurrentDictionary` y reemplaza cada valor completo, así que un lector nunca ve un vehículo a medio actualizar. |
| **Un observador que falla** | La excepción se aísla: los demás reciben el evento, la simulación sigue y el fallo queda en la auditoría con el nombre del observador, el tipo de evento y el vehículo. Si ni siquiera se puede auditar, se descarta sin detener nada. |
| **Dependencias circulares** | `Monitoring` solo depende de `IFleetAuditLogger` y de `VehicleLiveStatusDto`; no conoce `Simulation`. El simulador conoce a `Monitoring` (un solo sentido) y los observadores no dependen del publicador. |

```csharp
// VehicleTelemetryPublisher.cs — registro sin duplicados (por referencia) y entrega en cola, un solo hilo a la vez
public IDisposable Subscribe(ITelemetryObserver observer)
{
    ArgumentNullException.ThrowIfNull(observer);

    lock (_registryLock)
    {
        if (!IsRegistered(_observers, observer))
            Volatile.Write(ref _observers, _observers.Append(observer).ToArray());
    }

    return new Subscription(this, observer);
}

public void Publish(TelemetryEvent telemetry)
{
    ArgumentNullException.ThrowIfNull(telemetry);

    if (IsCalledByAnObserver() && ++_reentrantPublishes > MaxReentrantPublishes)
    {
        ReportFailure(
            $"Se descartó un evento {telemetry.GetType().Name} del vehículo {telemetry.LicensePlate}: los observadores publicaron " +
            $"más de {MaxReentrantPublishes} eventos como reacción a otros (posible ciclo de notificaciones).");
        return;
    }

    _pending.Enqueue(telemetry);
    DeliverPending();
}

private void DeliverPending()
{
    var thisThread = Environment.CurrentManagedThreadId;

    while (!_pending.IsEmpty)
    {
        if (Interlocked.CompareExchange(ref _delivererThreadId, thisThread, 0) != 0)
            return;

        try
        {
            while (_pending.TryDequeue(out var next))
                Deliver(next);
        }
        finally
        {
            _reentrantPublishes = 0;
            Volatile.Write(ref _delivererThreadId, 0);
        }
    }
}

private void Deliver(TelemetryEvent telemetry)
{
    // Se recorre la copia que había al empezar: registrar o retirar observadores durante la entrega no la altera.
    foreach (var observer in Volatile.Read(ref _observers))
    {
        try
        {
            observer.OnTelemetry(telemetry);
        }
        catch (Exception failure)
        {
            ReportFailure($"El observador {observer.GetType().Name} falló al recibir {telemetry.GetType().Name} del vehículo {telemetry.LicensePlate}: {failure.Message}");
        }
    }
}
```

**Los observadores concretos.** `TripAuditObserver` filtra por tipo y deja una línea por parada; `LiveTelemetryBoard` reemplaza la
entrada del vehículo con cada movimiento (y conserva la última parada **solo dentro del mismo viaje**), marca `arrived` con la
llegada y no inventa una posición si recibe una parada o una llegada de un vehículo que todavía no vio moverse.

```csharp
// TripAuditObserver
public void OnTelemetry(TelemetryEvent telemetry)
{
    if (telemetry is not TripStopReachedEvent stop)
        return;

    var percent = stop.ProgressPercent.ToString("0.#", CultureInfo.InvariantCulture);
    _audit.LogEvent(AuditCategory, $"El vehículo {stop.LicensePlate} alcanzó la parada '{stop.StopLabel}' ({percent} % del recorrido).");
}
```

**Agregar un observador nuevo** (por ejemplo, uno que cuente kilómetros por día) es escribir una clase que implemente
`ITelemetryObserver` y registrarla con una línea en `Program.cs`. No se modifica el publicador, el simulador ni los observadores existentes.

#### 7. Integración con los demás componentes

**Cambios en archivos existentes** (los únicos, mínimos y compatibles):

| Archivo | Cambio | Por qué era necesario |
|---------|--------|-----------------------|
| `Application/Simulation/VehicleMovementSimulator.cs` | Dos parámetros **opcionales al final** del constructor (`ITelemetryPublisher? telemetry = null`, `TimeProvider? clock = null`) y una llamada a `PublishTelemetry` al terminar el paso. | Es el único lugar que sabe cuándo un paso termina; sin esta llamada no habría quién publique. Con los valores por omisión se comporta **exactamente como antes** (una prueba lo fija) y las pruebas existentes que lo construyen compilan sin cambios. |
| `Api/Program.cs` | El bloque de registro de la sección «OBSERVER» (ver abajo). | Es la raíz de composición. No se eliminó ni se reemplazó ningún registro. |

**Registro en `Program.cs`** (cada ciclo de vida está justificado en el propio código):

```csharp
builder.Services.AddSingleton<VehicleTelemetryPublisher>();
builder.Services.AddSingleton<ITelemetryPublisher>(provider => provider.GetRequiredService<VehicleTelemetryPublisher>());
builder.Services.AddSingleton<ITelemetrySubject>(provider => provider.GetRequiredService<VehicleTelemetryPublisher>());
builder.Services.AddSingleton<ITelemetryObserver, TripAuditObserver>();
builder.Services.AddSingleton<LiveTelemetryBoard>();
builder.Services.AddSingleton<ITelemetryObserver>(provider => provider.GetRequiredService<LiveTelemetryBoard>());
builder.Services.AddSingleton<ILiveTelemetryBoard>(provider => provider.GetRequiredService<LiveTelemetryBoard>());
```

| Registro | Ciclo de vida | Por qué |
|----------|---------------|---------|
| `VehicleTelemetryPublisher` (y sus dos interfaces) | **Singleton** | Guarda el registro de observadores y la cola de entrega, que deben ser **únicos** y sobrevivir a cada paso de la simulación (el simulador es Scoped y se crea de nuevo en cada paso). Las dos interfaces resuelven **la misma instancia**: no hay duplicados. |
| `TripAuditObserver` | **Singleton** | Sin estado y solo depende de `IFleetAuditLogger` (Singleton): no captura ninguna dependencia de vida más corta. |
| `LiveTelemetryBoard` (como observador y como `ILiveTelemetryBoard`) | **Singleton** | Su estado debe ser único y compartido entre el paso de la simulación (escribe) y las peticiones HTTP (leen). Se registra **una vez** y se expone por sus dos interfaces. |
| Los observadores como `IEnumerable<ITelemetryObserver>` | (los recibe el publicador al construirse) | El publicador recibe **todos** los registrados y los suscribe al construirse: agregar un observador es registrarlo. Los observadores no dependen del publicador (no hay ciclo). |

**Qué no cambió.**

- **Bridge y Decorators de notificación:** siguen siendo los únicos que envían mensajes por canales; la telemetría no genera ningún aviso.
- **Mediator:** el cambio de estado de la ruta y del vehículo sigue siendo suyo. La llegada la completa `DeliveryRouteService` (que audita «cambió su estado a Completed»); por eso el observador de auditoría **no** repite la llegada.
- **`TripProgressService` y `GET /api/progress`:** no se tocaron; siguen siendo la consulta por demanda de los viajes activos. El tablero añade lo que esa consulta no tiene (lo último publicado sigue visible al terminar el viaje) y redondea igual, así que las dos nunca se contradicen (una prueba lo comprueba en cada paso del viaje).
- **`FleetController`, `TripProgressController`, la simulación (`VehicleSimulationBackgroundService`) y la auditoría (Singleton):** sin cambios. Los controladores nuevos son aparte.

#### 8. Principios SOLID aplicados

| Principio | Cómo se cumple |
|-----------|----------------|
| **S** — Responsabilidad única | Una clase por motivo de cambio: publicar y entregar (`VehicleTelemetryPublisher`), traducir un paso en eventos (`TripTelemetryEvents`), auditar paradas (`TripAuditObserver`), guardar lo último conocido (`LiveTelemetryBoard`), servir HTTP (`MonitoringController`). |
| **O** — Abierto/Cerrado | Un observador nuevo se agrega implementando `ITelemetryObserver` y registrándolo; el publicador, el simulador y los demás observadores no se modifican. Un tipo de evento nuevo es un `record` más. |
| **L** — Sustitución de Liskov | Cualquier `ITelemetryObserver` puede sustituir a otro sin alterar al publicador: si falla, se aísla; si publica, se encola. Las pruebas usan observadores de prueba (que registran, lanzan excepciones o publican) en lugar de los reales. |
| **I** — Segregación de interfaces | `ITelemetryObserver` tiene un método; el sujeto se divide en `ITelemetryPublisher` (publicar, lo que usa el simulador) y `ITelemetrySubject` (registrar y retirar); el tablero separa `ILiveTelemetryBoard` (consultar) del contrato de observador (alimentar). |
| **D** — Inversión de dependencias | El simulador depende de `ITelemetryPublisher`; los observadores, de `IFleetAuditLogger`; el controlador, de `ILiveTelemetryBoard`. Nada concreto cruza esas fronteras y todo se compone en `Program.cs`. |

#### 9. Pruebas de funcionamiento

Carpeta `tests/FleetManagement.Application.Tests/Observer/` — **98 pruebas**, sin librerías de mocking (dobles escritos a mano en
`ObserverTestDoubles.cs`):

| Archivo | Pruebas | Qué demuestra |
|---------|--------:|----------------|
| `VehicleTelemetryPublisherTests.cs` | 27 | **Registro**, **retirada** (por método y con el `IDisposable`, también dos veces y tras volver a suscribirse), **sin duplicados**, **sin observadores** (publicar no hace nada), **notificación** a todos, en orden, **recepción** del mismo evento; aislamiento de fallos (el fallo queda en la auditoría con el nombre del observador, el del evento y la placa; y sigue aislándose sin auditor o si el auditor también falla); un evento publicado por un observador se encola y **no hay recursión**; el corte de ciclos y que después todo sigue normal; cambios de observadores durante la entrega; y con **muchos hilos** publicando y suscribiendo (cada evento llega una vez, de a uno, en el orden de cada productor). |
| `TripAuditObserverTests.cs` | 11 | Una línea por parada con placa, parada y avance; el decimal siempre con punto (cualquiera sea la cultura del servidor); ni el movimiento ni la llegada se auditan; no se puede construir sin auditor y no conoce al publicador. |
| `LiveTelemetryBoardTests.cs` | 16 | Tablero vacío; primer movimiento crea la entrada; redondeo igual al de la consulta de avance; una entrada por vehículo; la última parada se recuerda solo dentro del mismo viaje; la llegada marca `Arrived` y conserva los datos; un viaje nuevo limpia la marca; no inventa posiciones; orden por placa; instantánea que los eventos posteriores no cambian; y con muchos escritores y lectores nunca se ve un vehículo a medio actualizar. |
| `TripTelemetryEventsTests.cs` | 18 | Los eventos de un paso (movimiento → paradas en orden de recorrido → llegada), una parada exactamente en el extremo del paso (se informa una vez), un paso grande que cruza varias, recorrer toda la ruta en pasos pequeños (cada parada una sola vez), origen y destino nunca son paradas, etiqueta de reemplazo, mismos datos → mismos eventos, y que calcular los eventos no cambia el viaje ni el vehículo. |
| `SimulatorTelemetryIntegrationTests.cs` | 16 | **Integración con el monitoreo real**: el `VehicleMovementSimulator` real con `DeliveryRouteService`, Mediator y State reales y el reloj manual: un viaje completo publica `moved, moved, stop:Primera, moved, moved, stop:Segunda, moved, moved, moved, arrived`; se publica **después** de guardar el paso; la auditoría tiene una línea por parada y la llegada solo la audita el servicio de rutas; el tablero coincide con `TripProgressService` en cada paso; un vehículo que no está En ruta, o una ruta que no está activa, no publican nada; dos viajes a la vez; un observador que falla no detiene la simulación; sin publicador la simulación hace lo mismo que antes. |
| `ObserverDesignTests.cs` | 10 | Pruebas de arquitectura (revisan las **firmas** de los tipos): `Monitoring` solo depende del puerto de auditoría y del DTO; solo la simulación conoce a `Monitoring`; el Mediator y el Bridge no conocen al Observer; ningún observador es participante del Mediator ni canal de notificación; el simulador conserva su constructor original y publica por el puerto estrecho; los eventos son inmutables; los observadores concretos son `sealed` y solo reciben abstracciones. |

**Comandos exactos** (desde la raíz de la solución, `FleetManagementSystem/`):

```bash
dotnet test --filter "FullyQualifiedName~FleetManagement.Application.Tests.Observer"
dotnet test                       # toda la suite: las anteriores y las nuevas
```

#### 10. Evidencias de ejecución

*(Todo lo que sigue es salida real; el origen y la forma de reproducirlo están en la [sección 12](#12-pruebas-automatizadas-y-cómo-demostrar-el-factory-method).)*

**a) Resultado de las pruebas** (ejecutor compatible con xUnit; ver las limitaciones en la [sección 11](#11-limitaciones-conocidas-del-prototipo)):

```
Carpeta/namespace             Total     OK  Fallos  Omitidas
Bridge                           20     20       0         0     (sin modificar: los canales de notificación siguen igual)
Mediator                        102    102       0         0     (sin modificar: la coordinación sigue igual)
Simulation                       95     95       0         0     (sin modificar: la simulación sigue igual)
Observer                         98     98       0         0
```

**b) Recorrido en vivo** (API en Development con la simulación acelerada ×5; el viaje de `VAN-002` tiene dos paradas). Lecturas
reales de `GET /api/monitoring/telemetry` (se muestra la fila de `VAN-002`; en 51 s se tomaron 35 lecturas, del 2 % al 100 %). A los
16.6 s de arrancar la API, el vehículo ya pasó por la primera parada y va hacia la segunda:

```json
{
  "vehicleId": "17d3c6ec-4eb8-4d7c-84e1-11e9bcdf42f3",
  "licensePlate": "VAN-002",
  "routeId": "07537f98-adc9-4633-be46-89dd80296199",
  "latitude": 7.095375,
  "longitude": -73.15655,
  "mileageKm": 18342.361,
  "percent": 33.9,
  "nextStopLabel": "Puente La Flora",
  "lastStopReached": "Terminal de carga Girón",
  "remainingKm": 4.594,
  "etaMinutes": 0.6,
  "arrived": false,
  "updatedAt": "2026-11-15T12:20:44.9102912+00:00"
}
```

Y a los 51.1 s, ya llegó (`arrived: true`) y el tablero conserva lo último que se publicó:

```json
{
  "vehicleId": "17d3c6ec-4eb8-4d7c-84e1-11e9bcdf42f3",
  "licensePlate": "VAN-002",
  "routeId": "07537f98-adc9-4633-be46-89dd80296199",
  "latitude": 7.1193,
  "longitude": -73.1227,
  "mileageKm": 18346.955,
  "percent": 100,
  "nextStopLabel": "Destino",
  "lastStopReached": "Puente La Flora",
  "remainingKm": 0,
  "etaMinutes": 0,
  "arrived": true,
  "updatedAt": "2026-11-15T12:21:18.921138+00:00"
}
```

Un vehículo que no ha estado en viaje (`MOT-004`) responde `404` con «No hay telemetría de ese vehículo: no ha estado en viaje desde que
arrancó la aplicación.», y `GET /api/progress` (que no cambió) devuelve `[]` porque el viaje ya terminó; la telemetría, en cambio, sigue
disponible. La auditoría (`GET /api/audit/logs`, filtrada por las categorías «Telemetría» y «Ruta») quedó así:

```json
[
  { "category": "Telemetría", "message": "El vehículo VAN-002 alcanzó la parada 'Terminal de carga Girón' (8.2 % del recorrido)." },
  { "category": "Telemetría", "message": "El vehículo VAN-002 alcanzó la parada 'Puente La Flora' (57.9 % del recorrido)." },
  { "category": "Ruta",       "message": "Ruta 'Ruta Girón - Bucaramanga Centro' cambió su estado a Completed." }
]
```

Dos paradas auditadas por el observador y **una sola** línea de llegada (la del servicio de rutas): la llegada no se audita dos veces.

**c) Pruebas de mutación** (se rompieron a propósito 14 reglas del código del Observer, de a una, en una copia; ver el detalle y el
resultado en la [sección 12](#12-pruebas-automatizadas-y-cómo-demostrar-el-factory-method)):

| Id | Rotura deliberada (se aplicó de a una, en una copia) | Resultado | Una de las pruebas que la detecta |
|----|------|------|------|
| O1 | Se permiten suscripciones duplicadas | Detectada · 3 pruebas fallan | `VehicleTelemetryPublisherTests.TheSameObserver_SubscribedTwice_IsRegisteredOnce_AndReceivesEachEventOnce` |
| O2 | El fallo de un observador se propaga | Detectada · 6 pruebas fallan | `SimulatorTelemetryIntegrationTests.AFailingObserver_DoesNotStopTheSimulation_NorTheOtherObservers` |
| O3 | Un evento publicado por un observador se entrega recursivamente (sin cola) | Detectada · 2 pruebas fallan y la ejecución termina con `Stack overflow` | `VehicleTelemetryPublisherTests.AnEventPublishedByAnObserver_IsQueued_AndDeliveredAfterTheCurrentOneReachedEveryone` |
| O4 | El tablero no reinicia la última parada al cambiar de viaje | Detectada · 1 prueba falla | `LiveTelemetryBoardTests.AReachedStop_IsRemembered_UntilTheTripChanges` |
| O5 | Una parada en el punto de partida del paso se informa dos veces | Detectada · 1 prueba falla | `TripTelemetryEventsTests.AStepThatStartsExactlyOnAStop_DoesNotReportItAgain` |
| O6 | El simulador publica ANTES de guardar el movimiento | Detectada · 3 pruebas fallan | `SimulatorTelemetryIntegrationTests.AFullTrip_PublishesEachMovement_EachStopOnce_AndOneArrival_InOrder` |
| O7 | La categoría de auditoría de las paradas cambia | Detectada · 1 prueba falla | `TripAuditObserverTests.AReachedStop_LeavesOneAuditEntry_WithTheVehicleTheStopAndTheProgress` |
| O8 | El límite de notificaciones en cadena deja de cortar (x1000) | Detectada · 2 pruebas fallan | `VehicleTelemetryPublisherTests.ACycleOfNotifications_IsCut_AfterTheLimit_AndAudited` |
| O9 | La llegada se informa aunque el vehículo no haya llegado | Detectada · 1 prueba falla | `TripTelemetryEventsTests.BeforeTheDestination_ThereIsNoArrival` |
| O10 | Un fallo al auditar el fallo de un observador se propaga | Detectada · 1 prueba falla | `VehicleTelemetryPublisherTests.IfTheAuditLoggerFailsToo_TheDeliveryStillGoesOn` |
| O11 | Retirar a un observador no lo retira | Detectada · 5 pruebas fallan | `VehicleTelemetryPublisherTests.Unsubscribe_StopsTheDelivery_AndReportsWhetherItWasRegistered` |
| O12 | La entrega deja de serializarse (cualquier hilo entrega, también dentro de un observador) | Detectada · 3 pruebas fallan | `VehicleTelemetryPublisherTests.AnEventPublishedByAnObserver_IsQueued_AndDeliveredAfterTheCurrentOneReachedEveryone` |
| O13 | El tablero no marca la llegada | Detectada · 3 pruebas fallan | `LiveTelemetryBoardTests.Arrival_MarksTheVehicleAsArrived_AndKeepsItsLastKnownData` |
| O14 | El tablero entrega los vehículos sin ordenar por placa | Detectada · 1 prueba falla | `SimulatorTelemetryIntegrationTests.TwoTripsAtOnce_ProduceTheirOwnEvents_AndTheBoardListsBothByPlate` |

**Conclusión: 14 de 14 roturas detectadas; ninguna sobrevivió.** Cada rotura se comparó contra la suite completa (1438 pruebas); «detectada» significa que la compilación falló, que al menos una prueba falló o que la ejecución se interrumpió.

En la rotura O3 (entrega recursiva, sin cola) dos pruebas fallan: en una el orden de entrega deja de ser el esperado (`AnEventPublishedByAnObserver_IsQueued_…`) y en la otra las notificaciones llegan a anidarse 50 veces en lugar de 1 (`NotificationsNeverNest_EvenWhenObserversKeepPublishing`); la prueba siguiente, con un observador que sigue publicando sin fin (`ACycleOfNotifications_IsCut_AfterTheLimit_AndAudited`), termina el proceso de pruebas con `Stack overflow`. En la corrida completa de las 36 roturas el arnés la registró como «la ejecución se interrumpió»; volví a ejecutar solo esta rotura, sobre el código final, para leer cómo terminaba.

### 8.19 State — qué puede hacer un vehículo según su estado

El estado operativo de un vehículo (`VehicleStatus`: Disponible, En ruta, Mantenimiento y Fuera de servicio) decidía comportamiento
comparando el enumerado en **cuatro sitios distintos**: el participante de vehículos del Mediator (`ReleasedStatus`), las reglas de
asignación (un `switch` en `OperabilityViolation`), el seguimiento de viajes (`ActiveTrips.IsEnRoute`) y el registro de mantenimientos
(`if (vehicle.Status == Maintenance) …` en `MaintenanceService`). Y había un quinto sitio donde la falta de una regla era el problema:
el cambio manual por la API (`UpdateVehicleStatusAsync`), que asignaba **cualquier** estado a **cualquier** estado, incluso un número
que no es un estado. El **State** pone cada regla en el estado al que pertenece: el vehículo se comporta según el objeto de estado que
le corresponde y nadie más tiene que comparar el enumerado.

#### 1. Descripción del patrón

State permite que un objeto (el **contexto**) cambie su comportamiento cuando cambia su estado interno: en lugar de condicionales
sobre el estado repartidos por el código, hay una clase por estado y el contexto delega en la que está vigente. Cada estado concreto
sabe qué permite, cómo reacciona a los eventos y a qué estados se puede pasar. Aquí el contexto es `VehicleStateContext` (envuelve a
un `Vehicle`), los estados son `AvailableState`, `EnRouteState`, `MaintenanceState` y `OutOfServiceState`, y los eventos son los tres
que ya existían en el sistema: el ciclo de vida de una ruta, el cambio manual de estado y el registro de un mantenimiento.

#### 2. Justificación de su utilización

| Problema real del proyecto | Cómo lo resuelve el State |
|----------------------------|---------------------------|
| La misma pregunta —«¿este vehículo puede operar?»— se respondía con comparaciones distintas en las reglas de asignación y en el seguimiento de viajes, y se habría repetido en los análisis nuevos. | La responde **el estado** (`CanOperate`, `CanTrackTrip`, `OperationBlockReason`). Los demás preguntan; ya no comparan. |
| El cambio manual de estado aceptaba cualquier valor: un vehículo en el taller podía pasar a «En ruta» con un solo `PATCH`, y también un número que no es un estado (`"99"`). | Cada estado declara a cuáles se puede pasar; el resto se rechaza con `InvalidVehicleTransitionException` (400 con el motivo) **antes** de guardar, auditar o notificar. |
| Agregar un estado (por ejemplo, «Reservado») obligaba a buscar y modificar los condicionales de cuatro clases. | Se agrega **una clase** (y su fila en el resolutor); las clases que consultan al estado no se tocan (Open/Closed). |
| Las reglas de ciclo de vida de las rutas (Mediator) estaban mezcladas con la decisión de qué estado le toca al vehículo. | El Mediator sigue coordinando **quién** avisa a quién; la decisión «qué le pasa a este vehículo» la toma su estado. |

**Qué le toca a cada patrón (no se duplican responsabilidades):**

| Patrón | Responsabilidad que **conserva** | Qué hace el State en esa frontera |
|--------|----------------------------------|-----------------------------------|
| **Mediator** ([8.16](#816-mediator--coordinar-el-estado-de-la-ruta-con-el-del-vehículo)) | Coordinar el estado de la ruta con el del vehículo: cuándo y a quién se avisa. Sus reglas no cambian. | Es quien decide **qué le pasa al vehículo** cuando el Mediator le avisa: `VehicleColleague` ya no tiene un `if`, le pasa el evento al estado. |
| **Observer** ([8.18](#818-observer--publicar-la-telemetría-del-recorrido-sin-que-el-simulador-sepa-quién-escucha)) | Distribuir la telemetría. | Nada: el simulador solo mueve a los vehículos que su estado deja seguir un viaje. |
| **Visitor** ([8.20](#820-visitor--analizar-la-flota-sin-cargar-las-entidades-con-análisis)) | Recorrer vehículos, rutas y mantenimientos para analizarlos. | Los visitantes le **preguntan** al estado si el vehículo puede operar y por qué no; no repiten esas reglas. |
| **Bridge y Decorators de notificación** | Enviar el aviso por un canal y decidir su prioridad. | Nada: `VehicleService` sigue notificando cada cambio **aceptado** con el mismo puente y los mismos decoradores. |

#### 3. Ubicación de la implementación

El estado y sus reglas son conocimiento del **dominio** (`Vehicle.Status` y `VehicleStatus` ya viven allí), así que el patrón está en la
capa Domain y no depende de nada más (una prueba lo vigila).

| Archivo (`src/FleetManagement.Domain/States/`) | Qué contiene |
|-----------------------------------------------|--------------|
| `IVehicleStateRules.cs` | Lado de **consulta**: `Status`, `CanOperate`, `CanTrackTrip`, `OperationBlockReason(placa)`. |
| `IVehicleStateBehavior.cs` | Lado de **comportamiento**: `StartTrip`, `FinishTrip`, `ChangeTo`, `CompleteMaintenance`. |
| `IVehicleState.cs` | Une las dos mitades; no agrega miembros. |
| `VehicleState.cs` | Estado abstracto: lo común (regla de transición manual, comportamientos por defecto). |
| `AvailableState.cs`, `EnRouteState.cs`, `MaintenanceState.cs`, `OutOfServiceState.cs` | Los cuatro estados concretos. |
| `VehicleStateContext.cs` | El contexto: envuelve a un `Vehicle` y delega en su estado vigente. |
| `IVehicleStateResolver.cs`, `VehicleStateResolver.cs` | Entrega el objeto de estado de cada `VehicleStatus` (una instancia compartida por estado). |
| `InvalidVehicleTransitionException.cs` | La transición manual no permitida. |

**Archivos existentes que usan los estados** (los cambios, mínimos y compatibles, están en la sección 7 de este apartado):
`Application/Mediators/VehicleColleague.cs`, `Application/Services/VehicleService.cs`, `Application/Services/MaintenanceService.cs`,
`Application/Assignment/RouteAssignmentValidator.cs`, `Application/Simulation/ActiveTrips.cs`, `Api/Controllers/FleetController.cs` y
`Api/Program.cs`. La entidad `Vehicle` **no se modificó**.

Pruebas: `tests/FleetManagement.Application.Tests/State/` (8 archivos) y los apoyos `Support/ConfigurableStates.cs` y
`Support/SignatureDependencies.cs`.

#### 4. Diagrama UML

```mermaid
classDiagram
    direction TB
    class VehicleStateContext {
        <<sealed>>
        +Vehicle Vehicle
        +State IVehicleState
        +CanOperate bool
        +CanTrackTrip bool
        +OperationBlockReason() string
        +StartTrip()
        +FinishTrip(hasOtherActiveTrip)
        +ChangeTo(target)
        +CompleteMaintenance()
        ~TransitionTo(target)
    }
    class IVehicleStateRules {
        <<interface>>
        +Status VehicleStatus
        +CanOperate bool
        +CanTrackTrip bool
        +OperationBlockReason(licensePlate) string
    }
    class IVehicleStateBehavior {
        <<interface>>
        +StartTrip(context)
        +FinishTrip(context, hasOtherActiveTrip)
        +ChangeTo(context, target)
        +CompleteMaintenance(context)
    }
    class IVehicleState {
        <<interface>>
    }
    class VehicleState {
        <<abstract>>
        #ManualTargets
        #RejectionHint(target)
    }
    class AvailableState
    class EnRouteState
    class MaintenanceState
    class OutOfServiceState
    class IVehicleStateResolver {
        <<interface>>
        +For(status) IVehicleState
    }
    class VehicleStateResolver {
        <<sealed>>
        +Default
    }
    class Vehicle {
        +Status VehicleStatus
    }
    class InvalidVehicleTransitionException
    IVehicleStateRules <|-- IVehicleState
    IVehicleStateBehavior <|-- IVehicleState
    IVehicleState <|.. VehicleState
    VehicleState <|-- AvailableState
    VehicleState <|-- EnRouteState
    VehicleState <|-- MaintenanceState
    VehicleState <|-- OutOfServiceState
    IVehicleStateResolver <|.. VehicleStateResolver
    VehicleStateResolver o-- IVehicleState : una instancia por estado
    VehicleStateContext --> Vehicle : lee y escribe Status
    VehicleStateContext --> IVehicleStateResolver : obtiene el estado vigente
    VehicleStateContext ..> IVehicleState : delega
    VehicleState ..> InvalidVehicleTransitionException : lanza
    class VehicleColleague
    class VehicleService
    class MaintenanceService
    class RouteAssignmentValidator
    class ActiveTrips
    VehicleColleague --> VehicleStateContext : StartTrip y FinishTrip
    VehicleService --> VehicleStateContext : ChangeTo
    MaintenanceService --> VehicleStateContext : CompleteMaintenance
    RouteAssignmentValidator --> VehicleStateContext : OperationBlockReason
    ActiveTrips --> VehicleStateContext : CanTrackTrip
```

```mermaid
stateDiagram-v2
    direction LR
    [*] --> Available : alta del vehículo
    Available --> EnRoute : salir a una ruta o a mano
    EnRoute --> Available : termina el viaje o se libera a mano
    Available --> Maintenance : a mano
    EnRoute --> Maintenance : a mano
    Maintenance --> Available : solo al registrar el mantenimiento
    Available --> OutOfService : a mano
    EnRoute --> OutOfService : a mano
    Maintenance --> OutOfService : a mano
    OutOfService --> Available : a mano
    OutOfService --> Maintenance : a mano
```

```mermaid
sequenceDiagram
    participant API as PATCH /api/fleet/vehicles/{id}/status
    participant C as FleetController
    participant S as VehicleService
    participant X as VehicleStateContext
    participant E as Estado vigente
    participant R as IVehicleRepository
    API->>C: {"status": "EnRoute"}
    C->>S: UpdateVehicleStatusAsync(id, request)
    S->>R: GetByIdAsync(id)
    S->>X: new VehicleStateContext(vehicle).ChangeTo(EnRoute)
    X->>E: State.ChangeTo(context, EnRoute)
    Note over X,E: el estado vigente se obtiene de Vehicle.Status
    alt el estado permite pasar a EnRoute
        E->>X: TransitionTo(EnRoute)
        S->>R: UpdateAsync(vehicle)
        S->>S: audita el cambio y lo notifica (Bridge y Decorators)
        C-->>API: 204
    else no lo permite (por ejemplo, Maintenance)
        E-->>S: InvalidVehicleTransitionException
        Note over S,R: no se guarda, no se audita y no se notifica
        C-->>API: 400 con el motivo
    end
```

#### 5. Interfaces y clases participantes

| Rol del patrón | Clase o interfaz | Responsabilidad |
|----------------|------------------|-----------------|
| **Context** | `VehicleStateContext` | Envuelve a un `Vehicle` y ofrece sus operaciones (`StartTrip`, `FinishTrip`, `ChangeTo`, `CompleteMaintenance`) y sus consultas; cada una se delega en el estado vigente. **No tiene ningún `if` ni `switch` sobre el estado.** Es un objeto liviano de un solo uso. Solo los estados pueden mover al vehículo (`TransitionTo` es `internal`). |
| **State** (consulta) | `IVehicleStateRules` | Lo que el estado **permite**: `CanOperate`, `CanTrackTrip`, `OperationBlockReason`. Es lo único que necesitan quienes solo preguntan. |
| **State** (comportamiento) | `IVehicleStateBehavior` | Lo que el estado **hace** ante cada evento. Cada operación recibe el contexto; es el estado quien decide si el vehículo cambia, se rechaza o se ignora. |
| **State** | `IVehicleState` | Une las dos interfaces anteriores. Segregación (ISP): un cliente que solo consulta no depende de las operaciones que cambian el estado. |
| **Abstract State** | `VehicleState` | Lo común: la regla de transición manual (`ChangeTo`: pedir el mismo estado no hace nada; un destino que el estado no permite lanza la excepción), `StartTrip` y los comportamientos por defecto. Cada estado concreto declara solo lo suyo. |
| **Concrete State** | `AvailableState` | Libre: puede operar, aún no sigue un viaje; puede ir a En ruta, Mantenimiento o Fuera de servicio. |
| **Concrete State** | `EnRouteState` | En recorrido: opera y **es el único que sigue un viaje**; al terminar uno queda Disponible salvo que siga en otro viaje activo; puede ir a Disponible, Mantenimiento o Fuera de servicio. |
| **Concrete State** | `MaintenanceState` | En el taller: no opera (con su motivo); conserva el estado al terminar un viaje; **al registrarse un mantenimiento queda Disponible, y esa es la única forma de llegar a Disponible desde aquí**; a mano solo puede pasar a Fuera de servicio (pedir Disponible se rechaza y el mensaje indica que hay que registrar el mantenimiento). |
| **Concrete State** | `OutOfServiceState` | Fuera de servicio: no opera (con su motivo); conserva el estado al terminar un viaje; solo puede pasar a mano a Disponible o Mantenimiento. |
| **Resolutor** | `IVehicleStateResolver` / `VehicleStateResolver` | Entrega el estado de cada valor de `VehicleStatus` (una instancia por estado, compartida). Los servicios dependen de la interfaz (DIP) y no construyen estados. Rechaza un valor que no es un estado. |
| **Almacén del estado** | `Vehicle.Status` | La **única** copia del estado (lo que ya persisten los repositorios, los DTO y la API). El contexto deriva el estado vigente de ahí en cada consulta, así que no hay una segunda copia que pueda desincronizarse. |
| **Error de dominio** | `InvalidVehicleTransitionException` | Hereda de `InvalidOperationException`, como `AssignmentRuleException`: una regla de negocio incumplida. Dice de qué estado a cuál y a cuáles sí se puede pasar, y —cuando el estado de origen tiene otro camino— una indicación (`Hint`). |
| **Clientes** | `VehicleColleague`, `VehicleService`, `MaintenanceService`, `RouteAssignmentValidator`, `ActiveTrips`, y los visitantes de 8.20 | Consultan o accionan el estado; ninguno compara `VehicleStatus` para decidir su comportamiento. |

#### 6. Funcionamiento dentro del sistema

**Qué hace cada estado.**

| Estado | ¿Opera? | ¿Sigue un viaje? | Si no opera, motivo | Una ruta se pone en marcha | Termina o se cancela un viaje | Se registra un mantenimiento | Cambio manual permitido a |
|--------|:------:|:----------------:|---------------------|----------------------------|------------------------------|-----------------------------|---------------------------|
| **Available** | sí | no | — | queda En ruta | no cambia | no cambia | EnRoute, Maintenance, OutOfService |
| **EnRoute** | sí | **sí** | — | sigue En ruta | queda Available **si no sigue en otro viaje activo** | no cambia | Available, Maintenance, OutOfService |
| **Maintenance** | no | no | «El vehículo {placa} está en mantenimiento; no se puede usar en una ruta.» | queda En ruta (regla conservada del Mediator) | no cambia | **queda Available** | OutOfService (Available **solo** registrando el mantenimiento) |
| **OutOfService** | no | no | «El vehículo {placa} está fuera de servicio; no se puede usar en una ruta.» | queda En ruta (regla conservada del Mediator) | no cambia | no cambia | Available, Maintenance |

Pedir a mano el estado en el que ya está no cambia nada (es inofensivo, como siempre) y sigue guardándose, auditándose y notificándose
igual que antes.

**Transiciones no permitidas.** Solo el cambio **manual** valida transiciones; las del ciclo de vida de las rutas y la del
mantenimiento siguen las reglas que ya tenían:

| Desde \ Hacia | Available | EnRoute | Maintenance | OutOfService |
|---------------|:---------:|:-------:|:-----------:|:------------:|
| **Available** | = | ✔ | ✔ | ✔ |
| **EnRoute** | ✔ | = | ✔ | ✔ |
| **Maintenance** | **✘** | **✘** | = | ✔ |
| **OutOfService** | ✔ | **✘** | ✔ | = |

(= : se pide el mismo estado, no pasa nada). Lo que se rechaza es sacar de golpe a la carretera a un vehículo que está en el taller
o fuera de servicio: antes debe quedar **Disponible**, un paso consciente. Es coherente con las reglas de asignación, que ya impedían
usar ese vehículo en una ruta. Además se rechaza un valor que no corresponde a ninguno de los cuatro estados (por ejemplo, `"99"`).

**Del taller solo se sale registrando el mantenimiento.** Un vehículo en Mantenimiento **no puede pasarse a Disponible a mano**
(`PATCH /api/fleet/vehicles/{id}/status` responde 400): la única forma de dejarlo Disponible es registrar su mantenimiento
(`POST /api/maintenance`), que llama a `CompleteMaintenance` del estado. La regla vive en un solo lugar —`MaintenanceState` ya no
incluye Available entre sus destinos manuales—, sin condicionales nuevos en los servicios. El rechazo añade una indicación
(`InvalidVehicleTransitionException.Hint`, que cada estado puede aportar con `RejectionHint`): «Para dejarlo Disponible, registre el
mantenimiento del vehículo.». Desde Mantenimiento sigue siendo posible pasar a mano a Fuera de servicio, y desde Fuera de servicio
a Disponible (esa transición ya existía y no se tocó).

**Tres orígenes de eventos, a propósito separados** (así ya funcionaba el sistema, y por eso no se unificaron):

1. **Ciclo de vida de las rutas** (`StartTrip`, `FinishTrip`), que llega por el Mediator. Son las reglas de coordinación de siempre;
   **no se endurecieron**: iniciar un viaje deja al vehículo En ruta desde cualquier estado, y quien impide que un vehículo en el taller
   salga a una ruta son las reglas de asignación, que se ejecutan antes de llegar al Mediator.
2. **Cambio manual** (`ChangeTo`), que pide un administrador por la API. Es el único que valida transiciones.
3. **Registro de un mantenimiento** (`CompleteMaintenance`), que llega desde `MaintenanceService.CreateAsync`: un vehículo que
   estaba en el taller sale de él y los demás **no cambian**. Era un `if` sobre el estado dentro del servicio; ahora es
   comportamiento del estado, con el mismo resultado.

**Código esencial** (extractos literales de `Domain/States/`):

```csharp
// VehicleState.cs — la regla de la transición manual, común a los cuatro estados
public void ChangeTo(VehicleStateContext context, VehicleStatus target)
{
    ArgumentNullException.ThrowIfNull(context);

    if (target == Status)
        return;

    if (!ManualTargets.Contains(target))
        throw new InvalidVehicleTransitionException(context.Vehicle.LicensePlate, Status, target, ManualTargets);

    context.TransitionTo(target);
}
```

```csharp
// EnRouteState.cs — solo En ruta libera al vehículo, y solo si no sigue en otro viaje activo
public override void FinishTrip(VehicleStateContext context, bool hasOtherActiveTrip)
{
    ArgumentNullException.ThrowIfNull(context);

    if (!hasOtherActiveTrip)
        context.TransitionTo(VehicleStatus.Available);
}
```

```csharp
// MaintenanceState.cs — qué responde cuando alguien pregunta si puede operar, y qué hace al registrarse un mantenimiento
public override bool CanOperate => false;

public override string? OperationBlockReason(string licensePlate)
    => $"El vehículo {licensePlate} está en mantenimiento; no se puede usar en una ruta.";

public override void CompleteMaintenance(VehicleStateContext context)
{
    ArgumentNullException.ThrowIfNull(context);
    context.TransitionTo(VehicleStatus.Available);
}
```

```csharp
// VehicleStateContext.cs — el contexto no tiene ningún if/switch sobre el estado: delega
public IVehicleState State => _states.For(Vehicle.Status);

public void StartTrip() => State.StartTrip(this);
public void FinishTrip(bool hasOtherActiveTrip) => State.FinishTrip(this, hasOtherActiveTrip);
public void ChangeTo(VehicleStatus target) => State.ChangeTo(this, target);
public void CompleteMaintenance() => State.CompleteMaintenance(this);

internal void TransitionTo(VehicleStatus target)
{
    _states.For(target); // rechaza un valor que no sea un estado conocido antes de tocar al vehículo
    Vehicle.Status = target;
}
```

**Decisiones de diseño.**

- **El estado se deriva de `Vehicle.Status`, no se guarda aparte.** Los repositorios, los DTO, la API y los datos de demostración siguen
  usando el enumerado; el contexto resuelve el objeto de estado en cada consulta. No hay dos copias que puedan quedar desincronizadas
  ni cambios en el contrato de la entidad.
- **Los estados no tienen datos: son inmutables y se comparten** (una instancia por estado, para todos los vehículos y todos los
  hilos). Sus constructores son `internal` y se obtienen por el resolutor; una prueba vigila que no tengan campos modificables.
- **`TransitionTo` es `internal`:** solo los estados (en este ensamblado) pueden decidir una transición. Nadie de afuera puede
  saltarse las reglas con un `context.TransitionTo(...)`.

#### 7. Integración con los demás componentes

**Cambios en archivos existentes** (los únicos; cada uno reemplaza una comparación del enumerado por una pregunta o una orden al estado):

| Archivo | Cambio | Por qué era necesario |
|---------|--------|-----------------------|
| `Application/Mediators/VehicleColleague.cs` | `vehicle.Status = VehicleStatus.EnRoute` → `new VehicleStateContext(vehicle, _states).StartTrip()`; y `ReleasedStatus(...)` → `.FinishTrip(hasOtherActiveTrip)`. Parámetro **opcional** al final del constructor (`IVehicleStateResolver? states = null`). | Es donde estaban las reglas 1, 2 y 3 del Mediator. El Mediator y los participantes de rutas **no cambiaron**. |
| `Application/Services/VehicleService.cs` | `vehicle.Status = newStatus` → `…ChangeTo(newStatus)`, **antes** de guardar, auditar o notificar. Séptimo parámetro opcional del constructor. | Es el único cambio manual de estado; sin esto no habría validación de transiciones. |
| `Application/Services/MaintenanceService.cs` | `if (vehicle.Status == Maintenance) vehicle.Status = Available` → `…CompleteMaintenance()`. Parámetro opcional al final. | Era otra comparación del enumerado con una regla propia; mismo resultado, ahora por el estado. |
| `Application/Assignment/RouteAssignmentValidator.cs` | El `switch` de `OperabilityViolation` → `…OperationBlockReason()`. Quinto parámetro opcional. | Los mensajes de rechazo son **los mismos** (ahora los dan los estados). |
| `Application/Simulation/ActiveTrips.cs` | `vehicle.Status == EnRoute` → `…CanTrackTrip`. | Define qué vehículos sigue la simulación: sigue siendo solo «En ruta». |
| `Api/Controllers/FleetController.cs` | Un `catch (InvalidVehicleTransitionException)` que responde 400 con el motivo, igual que los demás errores de regla. | Sin él, una transición rechazada sería un 500. |
| `Api/Program.cs` | Un registro: `AddSingleton<IVehicleStateResolver>(VehicleStateResolver.Default)`. | Raíz de composición. |

Las pruebas existentes no se modificaron **al introducir el patrón**; solo se ajustaron después, al adoptar la regla «del taller solo se sale registrando el mantenimiento» (las que daban por válido el cambio manual Mantenimiento → Disponible). Los constructores de `VehicleColleague`, `VehicleService`, `MaintenanceService` y
`RouteAssignmentValidator` solo ganaron un parámetro opcional al final, así que las pruebas que los construyen siguen compilando
(una prueba de diseño lo fija).

**Registro en `Program.cs`:**

```csharp
builder.Services.AddSingleton<IVehicleStateResolver>(VehicleStateResolver.Default);
```

| Pieza | Ciclo de vida | Por qué |
|-------|---------------|---------|
| `IVehicleStateResolver` (`VehicleStateResolver.Default`) | **Singleton** | Los estados no guardan datos (son inmutables): una sola tabla compartida por toda la aplicación. Es **la misma instancia** (`Default`) que usa quien construye un servicio sin inyectarla (por ejemplo `ActiveTrips`, que es una clase estática, y las pruebas). Lo consumen servicios **Scoped**, y un Singleton dentro de un Scoped es la dirección segura (no hay dependencia capturada). |
| `VehicleStateContext` | **No se registra** | Es un objeto de un solo uso que envuelve a un vehículo concreto (un dato de tiempo de ejecución) y vive lo que dura la operación: se crea con `new` alrededor del vehículo que se acaba de leer. Registrarlo como Singleton compartiría un vehículo entre peticiones, y como Scoped o Transient no tendría de dónde sacar el vehículo. |
| Los cuatro estados concretos | **No se registran** | Nadie los pide directamente; se obtienen por el resolutor. |

**Endpoints.** `PATCH /api/fleet/vehicles/{id}/status` responde **204** como siempre cuando el cambio se acepta; **404** si el
vehículo no existe y **400** si el texto no es un estado («Estado de vehículo inválido: 'Volando'.»), igual que antes; y ahora
**400** con el motivo cuando el estado actual no permite pasar al pedido. No se agregó ningún endpoint. El dashboard no se modificó: su
selector de estado (`frontend/js/dashboard.js`) ya mostraba el `message` de cualquier error de ese `PATCH` en un aviso y volvía a leer el
estado real del vehículo, de modo que un 400 por transición no permitida se ve igual que cualquier otro rechazo (esto se verificó leyendo el
código del frontend; no se probó en un navegador en esta entrega).

**Qué no cambió.**

- **Mediator:** la coordinación, sus cuatro reglas y su auditoría («Ruta … cambió su estado a …») son las mismas; lo comprueban las
  102 pruebas de `Mediator/` (sin modificar) y 30 pruebas nuevas de compatibilidad.
- **Bridge y Decorators de notificación:** `VehicleService` notifica cada cambio aceptado con el mismo puente y la misma prioridad
  (derivada del nuevo estado: Fuera de servicio = alta, Mantenimiento = normal, resto = baja). Un cambio rechazado no notifica.
- **Entidad `Vehicle`, DTO y repositorios:** sin cambios; el estado sigue siendo `Vehicle.Status`.
- **Comparaciones del enumerado que se dejaron a propósito**, porque no deciden comportamiento por estado: el estado inicial
  `Available` de las fábricas y del dominio, los datos de demostración, el conteo de vehículos por estado en `FleetMetrics`, el mensaje
  «figura como en ruta» de `DispatchPlanningService` y la prioridad de la notificación en `VehicleService`. Cambiarlos habría ampliado
  el alcance sin un beneficio en el comportamiento.

**Cambios de comportamiento (se informan, no se ocultan).** Solo hay dos, ambos en el cambio **manual** de estado por la API:

1. Pasar de **Mantenimiento** o **Fuera de servicio** directamente a **En ruta** ahora responde **400** (antes se aceptaba). Es la
   regla nueva que el State hace explícita.
2. Un número que no corresponde a ninguno de los cuatro estados (por ejemplo `"99"`) ahora responde **400**. La API original lo
   aceptaba y **guardaba un estado que no existe**; en la sección de evidencias se ve el contraste real.

#### 8. Principios SOLID aplicados

| Principio | Cómo se cumple |
|-----------|----------------|
| **S** — Responsabilidad única | Cada estado concreto solo sabe lo que ese estado permite y hace; el contexto solo delega; el resolutor solo entrega estados; los servicios solo coordinan su caso de uso. Las reglas del estado ya no están repartidas en varias clases. |
| **O** — Abierto/Cerrado | Un estado nuevo es una clase más (y una fila en el resolutor); los clientes que consultan al estado no se modifican. Una prueba lo demuestra con un estado alternativo que cambia el resultado sin tocar al Mediator. |
| **L** — Sustitución de Liskov | Los cuatro estados sustituyen a `VehicleState` sin que el contexto lo note; las pruebas usan un estado de prueba (`ConfigurableState`) en lugar de los reales y los componentes se comportan según lo que ese estado responde. |
| **I** — Segregación de interfaces | `IVehicleState` se divide en `IVehicleStateRules` (consultar) y `IVehicleStateBehavior` (cambiar). Una prueba comprueba que quien consulta no arrastra el comportamiento. |
| **D** — Inversión de dependencias | Los servicios dependen de `IVehicleStateResolver` y no de estados concretos (una prueba revisa las firmas y que el ensamblado de Application no contenga ningún estado concreto). |

#### 9. Pruebas de funcionamiento

Carpeta `tests/FleetManagement.Application.Tests/State/` — **192 pruebas**, sin librerías de mocking:

| Archivo | Pruebas | Qué demuestra |
|---------|--------:|----------------|
| `VehicleStateBehaviorTests.cs` | 58 | Cada estado concreto: qué permite (`CanOperate`, `CanTrackTrip`), su motivo de bloqueo con los mensajes originales, cómo reacciona a iniciar y terminar un viaje, **la matriz completa de transiciones manuales (16 casos, incluidas las no permitidas)**, que el rechazo explica el motivo y a dónde sí se puede ir, que un valor indefinido se rechaza, que los estados son inmutables y compartidos, y que el contexto no guarda una segunda copia del estado. |
| `VehicleServiceStateTransitionTests.cs` | 32 | **Transiciones no permitidas a través del servicio real**: un cambio permitido se guarda, se audita y se notifica; uno rechazado **no se guarda, no se audita y no se notifica**; el texto del estado sigue sin distinguir mayúsculas; vehículo inexistente y texto inválido responden como antes; la prioridad de la notificación sigue saliendo del nuevo estado. |
| `VehicleStateMediatorCompatibilityTests.cs` | 30 | **Compatibilidad con el Mediator**: el estado guardado del vehículo sigue las reglas de siempre desde cada estado inicial; un vehículo que sigue en otro viaje activo no se libera; una ruta sin vehículo o con vehículo inexistente no coordina nada; un retraso por alerta no toca al vehículo; el participante de vehículos le pregunta al estado y un estado alternativo cambia el resultado sin tocar al Mediator. |
| `VehicleStateMaintenanceRegistrationTests.cs` | 26 | Qué le hace a cada estado el registro de un mantenimiento (solo el taller sale de él, y solo así llega a Disponible; En ruta y Fuera de servicio no cambian); que el servicio sigue guardando el registro, la fecha, la auditoría y el aviso; que quien decide es el estado (con un estado alternativo). |
| `VehicleStateTripTrackingTests.cs` | 16 | Un viaje solo lo sigue la simulación mientras la ruta está activa **y** el vehículo está En ruta; mandar el vehículo al taller detiene su viaje y devolverlo a En ruta lo vuelve a mover; la simulación y la consulta de avance coinciden en qué viajes existen. |
| `VehicleStateAssignmentTests.cs` | 15 | Las reglas de asignación aceptan o rechazan según lo que el estado permite, con los mismos mensajes; el motivo del estado sale junto con las demás infracciones; un estado alternativo cambia el resultado sin tocar al validador. |
| `VehicleStateDesignTests.cs` | 11 | Pruebas de arquitectura: exactamente cuatro estados; el espacio `States` solo conoce el dominio; los componentes consultan al resolutor y nunca a un estado concreto; los constructores existentes se conservan con el parámetro nuevo opcional; la entidad `Vehicle` sigue con su enumerado y no conoce los estados. |
| `VehicleStateLifecycleScenarioTests.cs` | 4 | Escenarios completos: un vehículo que va al taller a mitad de un viaje se queda allí cuando el viaje termina y solo sale al registrarse su mantenimiento (a mano no pasa a Disponible); un viaje normal libera al vehículo por el estado; una ruta planificada no toca al vehículo; solo un cambio aceptado envía aviso. |

> Los totales de la tabla corresponden a la última medición con `dotnet test`. Después se agregaron pruebas de la regla «del taller solo se
> sale registrando el mantenimiento» y se ajustaron las que daban por válido el cambio manual Mantenimiento → Disponible
> (`VehicleStateBehaviorTests`, `VehicleServiceStateTransitionTests`, `VehicleStateMaintenanceRegistrationTests`,
> `VehicleStateTripTrackingTests`, `VehicleStateLifecycleScenarioTests`); los totales por archivo cambiaron y se actualizan al volver a
> ejecutar la suite.

**Comandos exactos** (desde la raíz de la solución, `FleetManagementSystem/`):

```bash
dotnet test --filter "FullyQualifiedName~FleetManagement.Application.Tests.State"
dotnet test                       # toda la suite: las anteriores y las nuevas
```

#### 10. Evidencias de ejecución

*(Todo lo que sigue es salida real; el origen y la forma de reproducirlo están en la [sección 12](#12-pruebas-automatizadas-y-cómo-demostrar-el-factory-method).)*

**a) Resultado de las pruebas** (ejecutor compatible con xUnit; ver las limitaciones en la [sección 11](#11-limitaciones-conocidas-del-prototipo)):

```
Carpeta/namespace             Total     OK  Fallos  Omitidas
Mediator                       102    102       0         0     (sin modificar: la coordinación sigue igual)
State                          192    192       0         0
```

**b) Transiciones no permitidas, con la API en marcha** (Development; `$TOKEN` es la sesión de `admin`). `CAR-003` está en
Mantenimiento y `VAN-005` Fuera de servicio:

```text
$ curl -s -X PATCH http://localhost:5080/api/fleet/vehicles/{id de CAR-003}/status -H "Authorization: Bearer $TOKEN" \
       -H 'Content-Type: application/json' -d '{"status": "EnRoute"}'
HTTP 400
{ "message": "Transición de estado no permitida: el vehículo CAR-003 está en Maintenance y no puede pasar a EnRoute. Desde Maintenance solo puede pasar a: OutOfService. Para dejarlo Disponible, registre el mantenimiento del vehículo." }

Auditoría de categoría «Vehículo» antes: 0 · después: 0  → el cambio rechazado no dejó rastro
Estado de CAR-003 después del rechazo: Maintenance

$ curl -s -X PATCH .../vehicles/{id de VAN-005}/status ... -d '{"status": "EnRoute"}'
HTTP 400
{ "message": "Transición de estado no permitida: el vehículo VAN-005 está en OutOfService y no puede pasar a EnRoute. Desde OutOfService solo puede pasar a: Available, Maintenance." }
```

> **Actualización posterior a estas mediciones — regla «del taller solo se sale registrando el mantenimiento».** Las salidas de esta
> sección se registraron **antes** de adoptar esa regla y no se volvieron a ejecutar. Lo que cambia respecto de lo mostrado:
> el mensaje de rechazo de `CAR-003` termina ahora con «Para dejarlo Disponible, registre el mantenimiento del vehículo.», y
> `PATCH … {"status": "Available"}` sobre un vehículo en Mantenimiento responde 400 (antes `204`). El camino permitido para volver a
> la carretera es: `POST /api/maintenance` (deja al vehículo Disponible y queda auditado en la categoría «Mantenimiento») y luego
> `PATCH … {"status": "EnRoute"}` (`204`). La auditoría de la categoría «Vehículo» deja **solo** ese último cambio aceptado
> (`Vehículo CAR-003 cambió de estado: Available -> EnRoute.`): los rechazos no dejan rastro y salir del taller no es un cambio manual.
> Esto lo fijan las pruebas `VehicleStateLifecycleScenarioTests` y `VehicleServiceStateTransitionTests`; los totales de pruebas y la
> tabla de mutación de abajo corresponden a la medición anterior.

**c) Lo de siempre se conserva y el único cambio de comportamiento, frente a la API original** (mismo `PATCH` a `MOT-004`):

```text
{"status": "Volando"}  → HTTP 400  { "message": "Estado de vehículo inválido: 'Volando'." }          (igual que antes)

{"status": "99"}       → API nueva     (5080): HTTP 400  { "message": "Transición de estado no permitida: el vehículo MOT-004 está en Available y no puede pasar a 99. Desde Available solo puede pasar a: EnRoute, Maintenance, OutOfService." }
                       → API original  (5081): HTTP 204  y el estado guardado para MOT-004 quedó «99»
```

**d) El estado actúa dentro del flujo real** (mismo recorrido en vivo):

```text
Se manda MOT-004 al taller (PATCH Maintenance → 204)
POST /api/maintenance (mantenimiento de MOT-004) → 200      Estado de MOT-004 después: Available      (CompleteMaintenance: el taller se libera)
POST /api/maintenance (mantenimiento de VAN-005) → 200      Estado de VAN-005 después: OutOfService   (los demás estados no cambian)

POST /api/routes (ruta para TRK-001, Disponible) → 201      Estado de TRK-001 con la ruta planificada: Available  (una ruta planificada no toca al vehículo)
PATCH /api/routes/{id}/status {"status": "InProgress"} → 204   Estado de TRK-001: EnRoute          (el Mediator avisa y el estado lo deja En ruta)
```

**e) Pruebas de mutación** (se rompieron a propósito 13 reglas del código de State, de a una, en una copia; ver el detalle y el
resultado en la [sección 12](#12-pruebas-automatizadas-y-cómo-demostrar-el-factory-method)):

| Id | Rotura deliberada (se aplicó de a una, en una copia) | Resultado | Una de las pruebas que la detecta |
|----|------|------|------|
| S1 | Mantenimiento permite pasar a mano a En ruta | Detectada · 10 pruebas fallan | `VehicleServiceStateTransitionTests.AVehicleThatCannotOperate_CannotBeSentStraightEnRoute` |
| S2 | En ruta libera al vehículo aunque tenga otro viaje activo | Detectada · 14 pruebas fallan | `DeliveryRouteServiceVehicleReleaseTests.EndingARoute_KeepsTheVehicleEnRoute_WhileAnotherActiveRouteUsesIt` |
| S3 | Empezar un viaje ya no deja al vehículo En ruta | Detectada · 28 pruebas fallan | `DeliveryRouteServiceAssignmentTests.StartingATrip_StillMarksTheVehicleEnRoute` |
| S4 | VehicleService asigna el estado sin preguntar al State | Detectada · 11 pruebas fallan | `VehicleServiceStateTransitionTests.AVehicleThatCannotOperate_CannotBeSentStraightEnRoute` |
| S5 | ActiveTrips sigue viajes de cualquier vehículo que no esté fuera de servicio | Detectada · 15 pruebas fallan | `SimulatorTelemetryIntegrationTests.AVehicleThatIsNotEnRoute_PublishesNothing_EvenIfItsRouteIsActive` |
| S6 | El validador de asignación ya no rechaza vehículos que no pueden operar | Detectada · 21 pruebas fallan | `RouteAssignmentValidatorTests.NewRoute_VehicleThatIsNotOperational_IsRejected` |
| S7 | Fuera de servicio pasa a poder operar | Detectada · 9 pruebas fallan | `VehicleStateBehaviorTests.EachState_DeclaresWhatItAllows` |
| S8 | Registrar un mantenimiento ya no saca al vehículo del taller | Detectada · 5 pruebas fallan | `VehicleStateMaintenanceRegistrationTests.EachState_DecidesWhatARegisteredMaintenanceDoesToTheVehicle` |
| S9 | MaintenanceService vuelve a decidir por su cuenta, sin preguntar al estado | Detectada · 2 pruebas fallan | `VehicleStateMaintenanceRegistrationTests.TheService_UsesTheResolverItIsGiven_ToDecideWhatHappensToTheVehicle` |
| S10 | Registrar un mantenimiento libera a cualquier vehículo (también al En ruta y al Fuera de servicio) | Detectada · 5 pruebas fallan | `VehicleStateMaintenanceRegistrationTests.EachState_DecidesWhatARegisteredMaintenanceDoesToTheVehicle` |
| S11 | Pedir el estado en el que ya está deja de ser inofensivo (se rechaza) | Detectada · 8 pruebas fallan | `VehicleServiceStateTransitionTests.AnAllowedChange_IsStored_Audited_AndNotified` |
| S12 | El cambio manual deja de validar la tabla de transiciones | Detectada · 16 pruebas fallan | `VehicleServiceStateTransitionTests.AVehicleThatCannotOperate_CannotBeSentStraightEnRoute` |
| S13 | Terminar un viaje libera a cualquier vehículo (también al de Mantenimiento y Fuera de servicio) | Detectada · 24 pruebas fallan | `DeliveryRouteServiceAssignmentTests.FinishingOrCancelling_OnlyReleasesAVehicleThatWasEnRoute` |

**Conclusión: 13 de 13 roturas detectadas; ninguna sobrevivió.** Cada rotura se comparó contra la suite completa (1438 pruebas); «detectada» significa que la compilación falló, que al menos una prueba falló o que la ejecución se interrumpió.

### 8.20 Visitor — analizar la flota sin cargar las entidades con análisis

La flota está hecha de cosas de **tipos distintos** —vehículos, rutas de reparto y registros de mantenimiento— y quien administra
quiere preguntas que las cruzan: ¿cuántos vehículos están realmente en uso?, ¿cuántos kilómetros hay planificados, en curso y
completados?, ¿qué vehículos nunca tuvieron mantenimiento?, ¿qué ruta en curso no va a poder avanzar? Cada pregunta nueva obligaría a
agregar métodos de análisis a las entidades o a engordar un servicio con un `switch` por tipo. El **Visitor** separa el análisis de los
objetos que se analizan: cada pregunta es una clase (un *visitante*) y los elementos solo saben **aceptar** visitantes.

#### 1. Descripción del patrón

Visitor representa una operación que se ejecuta sobre los elementos de una estructura de objetos, sin modificar las clases de esos
elementos. Cada elemento implementa un método `Accept(visitor)` que llama al método del visitante que corresponde a **su** tipo
(`visitor.VisitVehicle(this)`, `visitor.VisitRoute(this)`…): es el **doble despacho**, y por eso el visitante recibe cada elemento con su
tipo concreto sin `if`, `switch` ni conversiones. Agregar una operación nueva es escribir un visitante nuevo. Aquí hay tres tipos de
elemento (vehículo, ruta y mantenimiento), una estructura que los reúne y los recorre, y cuatro visitantes concretos.

#### 2. Justificación de su utilización

| Problema real del proyecto | Cómo lo resuelve el Visitor |
|----------------------------|-----------------------------|
| Los análisis de la flota cruzan datos de tres entidades; si vivieran en ellas, `Vehicle`, `DeliveryRoute` y `MaintenanceRecord` acabarían conociendo análisis que no les corresponden. | Las entidades **no se tocan**: el análisis ve **adaptadores** inmutables (`VehicleElement`, `RouteElement`, `MaintenanceRecordElement`) que copian lo que necesita. |
| Cada análisis nuevo (utilización, distancias, historial, hallazgos) tendría que recorrer la flota con su propio `switch` por tipo. | Un visitante por análisis; la estructura recorre una sola vez y cada elemento decide qué método del visitante se ejecuta. |
| Conviene que agregar un quinto análisis no obligue a modificar nada de lo existente. | Es una clase más (un `FleetVisitor<T>`); los elementos y la estructura no cambian (una prueba lo demuestra con un visitante nuevo). |
| Las reglas de «qué vehículo puede operar» ya viven en los estados (8.19); repetirlas en el análisis las duplicaría. | Los visitantes **le preguntan al estado** (`CanOperate`, `CanTrackTrip`, `OperationBlockReason`) y reutilizan sus mensajes. |

**Por qué no es otro Decorator.** El proyecto ya tiene un Decorator para los reportes ([8.8](#88-decorator-de-reportes-y-facade-del-resumen)).
Los dos patrones resuelven problemas distintos y no comparten código:

| | **Decorator de reportes** (8.8) | **Visitor** (8.20) |
|---|---|---|
| Problema | Componer **un** reporte apilando secciones: cada decorador envuelve al anterior y le suma la suya. | Ejecutar **operaciones nuevas** sobre elementos de **tipos distintos** sin modificarlos. |
| Estructura | Una cadena de objetos que implementan la misma interfaz (`IFleetReportGenerator`). | Elementos que aceptan visitantes + una estructura que los recorre. |
| Agregar algo nuevo | Otro decorador (otra sección del reporte). | Otro visitante (otro análisis). |
| Qué ve | Los datos que cada decorador lee para armar su sección. | Una foto inmutable de vehículos, rutas y mantenimientos. |
| Salida | Un reporte con secciones. | Un resultado propio por visitante. |

El análisis **no usa** `IFleetReportGenerator` ni sus decoradores, y los reportes no saben que el análisis existe: ningún tipo de un
espacio de nombres referencia al del otro (una prueba de diseño lo vigila), y `ReportsController` y su cadena **no se modificaron**.

**Qué le toca a cada patrón (no se duplican responsabilidades):**

| Patrón | Responsabilidad que **conserva** | Qué hace el Visitor en esa frontera |
|--------|----------------------------------|-------------------------------------|
| **Decorator de reportes y Facade** | Componer el reporte por secciones y el resumen del dashboard. | Nada: es un análisis aparte, con su propio servicio y endpoint. |
| **State** ([8.19](#819-state--qué-puede-hacer-un-vehículo-según-su-estado)) | Decidir qué permite cada estado. | **Pregunta** al estado; no copia sus reglas. |
| **Iterator** ([8.15](#815-iterator--recorrer-la-carga-de-una-ruta-sin-conocer-su-estructura)) | Recorrer la carga de una ruta. | Nada: la estructura del Visitor recorre elementos de la flota, no la carga. |
| **Observer** ([8.18](#818-observer--publicar-la-telemetría-del-recorrido-sin-que-el-simulador-sepa-quién-escucha)) | Publicar la telemetría. | Nada: el análisis lee los repositorios en el momento de la petición. |

#### 3. Ubicación de la implementación

| Archivo | Qué contiene |
|---------|--------------|
| `src/FleetManagement.Application/Analysis/IFleetElement.cs` | El **Element**: un único método, `Accept`. |
| `src/FleetManagement.Application/Analysis/IFleetVisitor.cs` | El **Visitor** (un método por tipo de elemento) y su variante `IFleetVisitor<TResult>` con `Result`. |
| `src/FleetManagement.Application/Analysis/FleetVisitor.cs` | Base de los visitantes concretos: ignora por defecto cada tipo, para que cada uno sobrescriba solo los que le interesan. |
| `src/FleetManagement.Application/Analysis/FleetElements.cs` | Los tres **elementos concretos**, como adaptadores: `VehicleElement`, `RouteElement`, `MaintenanceRecordElement`. |
| `src/FleetManagement.Application/Analysis/FleetElementStructure.cs` | La **estructura de objetos**: construye los elementos a partir de las entidades y los recorre. |
| `src/FleetManagement.Application/Analysis/VehicleUtilizationVisitor.cs` | Visitante 1: utilización de los vehículos. |
| `src/FleetManagement.Application/Analysis/RouteDistanceVisitor.cs` | Visitante 2: recorridos y distancias. |
| `src/FleetManagement.Application/Analysis/MaintenanceHistoryVisitor.cs` | Visitante 3: historial de mantenimiento. |
| `src/FleetManagement.Application/Analysis/OperationalInsightsVisitor.cs` y `OperationalInsight.cs` | Visitante 4: hallazgos operativos; códigos, severidades y umbrales. |
| `src/FleetManagement.Application/Analysis/MaintenanceMileage.cs`, `ActiveRouteVehicles.cs` | Dos reglas únicas que comparten visitantes (kilómetros desde el último mantenimiento; qué vehículos tienen una ruta en curso), para no escribirlas dos veces. |
| `src/FleetManagement.Application/DTOs/FleetAnalysisDtos.cs` | Los resultados que entrega cada visitante (registros). |
| `src/FleetManagement.Application/Interfaces/Services/IFleetAnalysisService.cs`, `Services/FleetAnalysisService.cs` | El caso de uso: lee, adapta y pasa un visitante por análisis. |
| `src/FleetManagement.Api/Controllers/AnalysisController.cs` | `GET api/analysis/fleet`. |
| **Modificado:** `src/FleetManagement.Api/Program.cs` | Un solo registro (ver la sección 7). **Ninguna entidad, servicio, controlador ni reporte existente se modificó.** |
| Pruebas: `tests/FleetManagement.Application.Tests/Visitor/` | 7 archivos de pruebas y `FleetData.cs` (datos de prueba). |

#### 4. Diagrama UML

```mermaid
classDiagram
    direction LR
    class IFleetElement {
        <<interface>>
        +Accept(visitor)
    }
    class VehicleElement {
        <<record>>
    }
    class RouteElement {
        <<record>>
        +IsActive bool
    }
    class MaintenanceRecordElement {
        <<record>>
    }
    class IFleetVisitor {
        <<interface>>
        +VisitVehicle(vehicle)
        +VisitRoute(route)
        +VisitMaintenanceRecord(record)
    }
    class IFleetVisitorResult["IFleetVisitor#lt;TResult#gt;"] {
        <<interface>>
        +Result TResult
    }
    class FleetVisitorBase["FleetVisitor#lt;TResult#gt;"] {
        <<abstract>>
    }
    class VehicleUtilizationVisitor
    class RouteDistanceVisitor
    class MaintenanceHistoryVisitor
    class OperationalInsightsVisitor
    class FleetElementStructure {
        <<sealed>>
        +Build(vehicles, routes, records)$
        +Accept(visitor)
        +Analyze(visitor) TResult
        +Count() int
    }
    class FleetAnalysisService
    class Vehicle
    class DeliveryRoute
    class MaintenanceRecord
    class IVehicleStateResolver {
        <<interface>>
    }
    IFleetElement <|.. VehicleElement
    IFleetElement <|.. RouteElement
    IFleetElement <|.. MaintenanceRecordElement
    IFleetVisitor <|-- IFleetVisitorResult
    IFleetVisitorResult <|.. FleetVisitorBase
    FleetVisitorBase <|-- VehicleUtilizationVisitor
    FleetVisitorBase <|-- RouteDistanceVisitor
    FleetVisitorBase <|-- MaintenanceHistoryVisitor
    FleetVisitorBase <|-- OperationalInsightsVisitor
    IFleetElement ..> IFleetVisitor : Accept llama a VisitX(this)
    FleetElementStructure o-- IFleetElement : reúne y recorre
    FleetElementStructure ..> Vehicle : lee y adapta
    FleetElementStructure ..> DeliveryRoute : lee y adapta
    FleetElementStructure ..> MaintenanceRecord : lee y adapta
    FleetAnalysisService --> FleetElementStructure : construye y recorre
    FleetAnalysisService ..> VehicleUtilizationVisitor : crea uno por análisis
    VehicleUtilizationVisitor ..> IVehicleStateResolver : le pregunta al estado
    OperationalInsightsVisitor ..> IVehicleStateResolver : le pregunta al estado
```

```mermaid
sequenceDiagram
    participant API as GET /api/analysis/fleet
    participant S as FleetAnalysisService
    participant R as Repositorios
    participant E as FleetElementStructure
    participant X as VehicleElement
    participant V as VehicleUtilizationVisitor
    participant ST as Estado del vehículo
    API->>S: AnalyzeFleetAsync()
    S->>R: lee vehículos, rutas y mantenimientos
    S->>E: Build(vehículos, rutas, mantenimientos)
    Note over E: adapta cada entidad a un elemento inmutable y fija el orden
    S->>E: Analyze(new VehicleUtilizationVisitor(estados))
    loop cada elemento, en orden
        E->>X: Accept(visitante)
        X->>V: VisitVehicle(this)
        Note over X,V: doble despacho, el elemento elige el método
        V->>V: acumula lo que le interesa
    end
    E->>V: Result
    V->>ST: CanOperate
    V-->>S: VehicleUtilizationDto
    Note over S: repite lo mismo con los otros tres visitantes
    S-->>API: FleetAnalysisDto
```

#### 5. Interfaces y clases participantes

| Rol del patrón | Clase o interfaz | Responsabilidad |
|----------------|------------------|-----------------|
| **Element** | `IFleetElement` | Cualquier cosa de la flota que se puede analizar. Su único método, `Accept(IFleetVisitor)`, hace el doble despacho. |
| **Concrete Element** (adaptador) | `VehicleElement` | Presenta un vehículo como elemento analizable: copia id, placa, tipo, estado y kilometraje. `Accept` llama a `VisitVehicle(this)`. |
| **Concrete Element** (adaptador) | `RouteElement` | Presenta una ruta: id, nombre, estado, vehículo asignado, distancia estimada, número de paradas, duración y retraso. Sabe si está en curso (`IsActive`: en progreso o retrasada). Llama a `VisitRoute(this)`. |
| **Concrete Element** (adaptador) | `MaintenanceRecordElement` | Presenta un mantenimiento realizado: id, vehículo, tipo, fecha y kilometraje. Llama a `VisitMaintenanceRecord(this)`. |
| **Visitor** | `IFleetVisitor` | Un método por tipo de elemento: `VisitVehicle`, `VisitRoute`, `VisitMaintenanceRecord`. |
| **Visitor** (con resultado) | `IFleetVisitor<TResult>` | Agrega `Result`. Separado del anterior (ISP): la estructura solo recorre y acepta, no sabe qué devuelve cada análisis. |
| **Visitor base** | `FleetVisitor<TResult>` | Ignora por defecto cada tipo de elemento; no contiene lógica de análisis. |
| **Concrete Visitor 1** | `VehicleUtilizationVisitor` | Visita vehículos y rutas: cuántos vehículos pueden operar, cuántos están en uso, kilometraje total y promedio, reparto por tipo. |
| **Concrete Visitor 2** | `RouteDistanceVisitor` | Visita solo rutas: kilómetros completados, en curso y planificados, ruta más larga, promedio y retrasos acumulados. |
| **Concrete Visitor 3** | `MaintenanceHistoryVisitor` | Visita vehículos y mantenimientos: cuántos de cada tipo, qué vehículos nunca tuvieron uno, el último de cada vehículo, kilómetros desde entonces y promedio entre mantenimientos. |
| **Concrete Visitor 4** | `OperationalInsightsVisitor` | Visita los tres tipos y los cruza: señala lo que un operador debe atender, del más al menos urgente. |
| **Object Structure** | `FleetElementStructure` | Reúne los elementos y sabe recorrerlos (`Accept`, `Analyze`). Se construye una vez por análisis con `Build` y no cambia. Orden determinista: vehículos por placa, rutas por nombre y mantenimientos por fecha. |
| **Cliente** | `FleetAnalysisService` | Lee los repositorios, construye la estructura y le pasa **un visitante por análisis**. No contiene ningún cálculo. |
| **Entidades** (no se modifican) | `Vehicle`, `DeliveryRoute`, `MaintenanceRecord` | Se adaptan a elementos; no conocen a los visitantes. |

#### 6. Funcionamiento dentro del sistema

**Los cuatro análisis.**

| Visitante | Visita | Qué responde (resultado) |
|-----------|--------|--------------------------|
| `VehicleUtilizationVisitor` | vehículos, rutas | Total y operables (los decide el estado), **en uso** (operables con una ruta en curso), porcentaje de utilización, kilometraje total y promedio, y el desglose por tipo (siempre aparecen todos los tipos). |
| `RouteDistanceVisitor` | rutas | Distancia total, completada, en curso y planificada; promedio; ruta más larga; rutas retrasadas y minutos de retraso; desglose por estado. **Las rutas canceladas no suman** a los totales (no se recorren) pero el desglose las muestra aparte. |
| `MaintenanceHistoryVisitor` | vehículos, mantenimientos | Total y por tipo; vehículos con y sin historial; por vehículo, el último mantenimiento y los km desde entonces; promedio de km entre mantenimientos consecutivos (un intervalo negativo, dato mal cargado, se ignora). |
| `OperationalInsightsVisitor` | vehículos, rutas, mantenimientos | Los hallazgos de la tabla siguiente. |

**Hallazgos operativos** (los códigos son estables; el mensaje es para leer). Salen ordenados por severidad, luego por código y luego
por sujeto:

| Código | Severidad | Cuándo se emite |
|--------|-----------|-----------------|
| `ActiveRouteWithoutOperableVehicle` | Critical | Ruta en curso sin vehículo, con un vehículo que ya no existe, o cuyo vehículo **no puede operar** (el motivo lo da su estado). |
| `ActiveRouteVehicleNotEnRoute` | Warning | Ruta en curso cuyo vehículo opera pero **no está En ruta** (por ejemplo, alguien lo dejó Disponible a mano): nada la mueve. |
| `ActiveRouteDelayed` | Warning | Ruta en curso con un retraso de 30 minutos o más (umbral configurable). |
| `PlannedRouteVehicleNotOperable` | Warning | Ruta planificada cuyo vehículo no puede operar (con el motivo del estado) o ya no existe. |
| `EnRouteVehicleWithoutActiveRoute` | Warning | Vehículo En ruta sin ninguna ruta en curso: la simulación no lo mueve. |
| `ServiceDistanceExceeded` | Warning | Vehículo con 10 000 km o más desde su último mantenimiento (o desde siempre, si nunca tuvo uno). |
| `PlannedRouteWithoutVehicle` | Info | Ruta planificada sin vehículo asignado. |

Los umbrales (30 minutos y 10 000 km) tienen nombre y valor por defecto en `InsightThresholds` y se pueden cambiar sin tocar al visitante.
El análisis de historial **describe**; no decide qué mantenimiento está vencido, porque eso es del módulo predictivo
(`IMaintenanceService`) y no se duplica.

**El doble despacho, en código** (extractos literales de `Application/Analysis/`):

```csharp
// FleetElements.cs — cada elemento le dice al visitante cuál de sus métodos ejecutar
public sealed record VehicleElement(Guid Id, string LicensePlate, VehicleType Type, VehicleStatus Status, double MileageKm) : IFleetElement
{
    public static VehicleElement From(Vehicle vehicle) { /* copia los datos que el análisis necesita */ }

    public void Accept(IFleetVisitor visitor) => visitor.VisitVehicle(this);
}
```

```csharp
// FleetElementStructure.cs — la estructura solo ofrece cada elemento y entrega el resultado; no sabe qué hace cada visitante
public void Accept(IFleetVisitor visitor)
{
    ArgumentNullException.ThrowIfNull(visitor);

    foreach (var element in _elements)
        element.Accept(visitor);
}

public TResult Analyze<TResult>(IFleetVisitor<TResult> visitor)
{
    Accept(visitor);
    return visitor.Result;
}
```

```csharp
// FleetAnalysisService.cs — un visitante por análisis, todos sobre los mismos elementos
var structure = FleetElementStructure.Build(vehicles, routes, records);

return new FleetAnalysisDto(
    _clock.GetUtcNow(),
    new AnalyzedElementsDto(structure.Count<VehicleElement>(), structure.Count<RouteElement>(), structure.Count<MaintenanceRecordElement>()),
    structure.Analyze(new VehicleUtilizationVisitor(_vehicleStates)),
    structure.Analyze(new RouteDistanceVisitor()),
    structure.Analyze(new MaintenanceHistoryVisitor()),
    structure.Analyze(new OperationalInsightsVisitor(_vehicleStates, _thresholds)));
```

```csharp
// VehicleUtilizationVisitor.cs — qué hace con lo que visita, y a quién le pregunta si un vehículo puede operar
public override void VisitVehicle(VehicleElement vehicle) => _vehicles[vehicle.Id] = vehicle;

public override void VisitRoute(RouteElement route) => _routes[route.Id] = route;

// ... al armar el resultado:
var operable = _vehicles.Values.Count(vehicle => _states.For(vehicle.Status).CanOperate);
```

**Propiedades que se cuidaron** (todas con pruebas):

- **Elementos inmutables que copian los datos.** El análisis ve una foto coherente aunque la simulación siga moviendo vehículos, y
  un visitante no puede modificar una entidad aunque quisiera (los elementos no guardan referencias a las entidades).
- **El resultado no depende del orden ni de visitar dos veces.** Cada visitante guarda lo visitado por id y calcula al leer
  `Result`: visitar el mismo elemento dos veces no lo cuenta dos veces.
- **Un visitante, un análisis.** Los visitantes acumulan estado mientras recorren, por eso se usa uno por análisis y no se comparten
  entre peticiones ni entre hilos.
- **Solo lectura.** El análisis no cambia datos, ni auditoría, ni notificaciones (una prueba lo comprueba).

**Cuándo cuesta agregar algo.** Un análisis nuevo es un visitante nuevo y no toca nada existente. En cambio, un **tipo de elemento**
nuevo obliga a agregar un método a `IFleetVisitor` (el compromiso conocido de este patrón); por eso el conjunto de elementos es
cerrado y deliberado (tres tipos, una prueba lo fija) y la base `FleetVisitor<T>` reduce el impacto con métodos que no hacen nada por defecto.

#### 7. Integración con los demás componentes

**Cambios en archivos existentes: uno solo**, `Api/Program.cs`, con un registro. Ninguna entidad, servicio, repositorio, controlador ni
reporte existente se modificó.

```csharp
builder.Services.AddScoped<IFleetAnalysisService, FleetAnalysisService>();
```

| Pieza | Ciclo de vida | Por qué |
|-------|---------------|---------|
| `IFleetAnalysisService` → `FleetAnalysisService` | **Scoped** | Es un caso de uso sin estado propio, como los demás servicios de aplicación. Lee repositorios y el resolutor de estados (ambos Singleton) y el reloj (`TimeProvider`, Singleton, ya registrado): no captura ninguna dependencia de vida más corta. |
| Los cuatro visitantes | **No se registran** | Acumulan estado mientras recorren: un visitante Singleton o Scoped mezclaría los resultados de análisis o de peticiones distintas. Si se registraran tendrían que ser **Transient**; como el servicio los crea con sus propios parámetros (el resolutor y los umbrales), no hay nada que inyectar. |
| `FleetElementStructure` y los elementos | **No se registran** | Se construyen en cada petición con los datos de ese momento (una foto) y son inmutables. |
| `InsightThresholds` | Opcional (no se registra) | El constructor lo recibe como parámetro opcional: sin registro se usan los valores por defecto (30 min y 10 000 km). |

**Con el resto del sistema.** El análisis lee los repositorios de vehículos, rutas y mantenimientos (los mismos que usan los demás
servicios) y consulta los estados por `IVehicleStateResolver` (8.19). Refleja lo que dejaron los otros patrones: un vehículo que el
Mediator liberó al completarse una ruta aparece Disponible, y uno que un administrador mandó al taller aparece sin poder operar (una
prueba hace ambas cosas con los servicios reales).

**Endpoint.** `GET /api/analysis/fleet` (nuevo; de lectura y sin rol, igual que los reportes y la consulta de vehículos). Devuelve
`generatedAt`, `elements` (cuántos elementos de cada tipo se analizaron), `utilization`, `routes`, `maintenance` e `insights`.

**Qué no cambió.** El generador de reportes y su cadena de Decorators, `ReportsController`, el Facade del dashboard, el módulo
predictivo de mantenimiento, el Iterator de la carga y todas las entidades de dominio.

#### 8. Principios SOLID aplicados

| Principio | Cómo se cumple |
|-----------|----------------|
| **S** — Responsabilidad única | Cada visitante es **un** análisis; la estructura solo reúne y recorre; los elementos solo adaptan y aceptan; el servicio solo orquesta; el controlador solo sirve HTTP. |
| **O** — Abierto/Cerrado | Un análisis nuevo es un visitante nuevo: los elementos, la estructura y los demás visitantes no cambian (una prueba define un visitante nuevo y lo ejecuta sin tocar nada). Lo que sí obliga a cambiar es agregar un **tipo de elemento**; por eso ese conjunto es cerrado a propósito. |
| **L** — Sustitución de Liskov | La estructura acepta cualquier `IFleetVisitor<T>` sin conocer cuál es; los visitantes sobrescriben solo lo que necesitan y la base ignora el resto, sin alterar el recorrido. |
| **I** — Segregación de interfaces | Tres contratos pequeños: `IFleetElement` (aceptar), `IFleetVisitor` (visitar cada tipo) e `IFleetVisitor<TResult>` (entregar un resultado). La estructura depende solo del segundo. |
| **D** — Inversión de dependencias | Los visitantes dependen de `IVehicleStateResolver`; el servicio, de las interfaces de repositorio, del resolutor y de `TimeProvider`. Una excepción deliberada: el servicio crea sus visitantes con `new`, porque acumulan estado, son parte del algoritmo y no una dependencia externa; inyectar una fábrica agregaría una abstracción sin una segunda implementación. |

#### 9. Pruebas de funcionamiento

Carpeta `tests/FleetManagement.Application.Tests/Visitor/` — **135 pruebas**, sin librerías de mocking:

| Archivo | Pruebas | Qué demuestra |
|---------|--------:|----------------|
| `OperationalInsightsVisitorTests.cs` | 47 | Cada regla de hallazgo con su severidad, su mensaje y su umbral (desde el límite exacto); que un vehículo bloqueado sale una sola vez y como crítico; que el motivo lo da el estado (con un estado alternativo); ordenamiento por urgencia; umbrales configurables; sin dependencia del orden ni de visitar dos veces. |
| `FleetElementsTests.cs` | 23 | **Adaptadores**: copian lo que el análisis necesita, son una foto que no sigue a la entidad, no guardan referencias a ella y no la modifican; **doble despacho**: cada elemento llama al método de su propio tipo, una sola vez; la estructura ordena, cuenta y recorre; un visitante nuevo no requiere cambiar elementos ni estructura. |
| `FleetAnalysisServiceTests.cs` | 15 | **Integración con datos reales**: el servicio con los repositorios y servicios de la aplicación (rutas con su Mediator, vehículos con su State); el análisis refleja cambiar el estado de un vehículo y completar un viaje; no cambia datos, auditoría ni avisos; cada repositorio se lee una vez; cada llamada crea sus propios visitantes. |
| `VehicleUtilizationVisitorTests.cs` | 14 | Totales, por vehículo, desglose por tipo (con ceros), quién opera lo decide el estado, redondeos, flota vacía. |
| `MaintenanceHistoryVisitorTests.cs` | 14 | Totales y por tipo, último mantenimiento y km desde él, promedio entre mantenimientos consecutivos, intervalos negativos descartados, vehículos sin historial, registros de un vehículo desconocido. |
| `RouteDistanceVisitorTests.cs` | 11 | Las rutas canceladas no suman a totales, promedio, más larga ni retrasos; desglose por estado; orden; redondeos; sin rutas. |
| `VisitorDesignTests.cs` | 11 | Pruebas de arquitectura (revisan las **firmas** de los tipos): `Analysis` solo depende del dominio, de los estados y de los DTO de resultado; los visitantes nunca nombran una entidad; **no es otro Decorator** (no envuelve ni toca los reportes); el contrato tiene un método por tipo; el conjunto de elementos es cerrado; todo visitante entrega un DTO; el servicio crea sus visitantes por análisis. |

**Comandos exactos** (desde la raíz de la solución, `FleetManagementSystem/`):

```bash
dotnet test --filter "FullyQualifiedName~FleetManagement.Application.Tests.Visitor"
dotnet test                       # toda la suite: las anteriores y las nuevas
```

#### 10. Evidencias de ejecución

*(Todo lo que sigue es salida real; el origen y la forma de reproducirlo están en la [sección 12](#12-pruebas-automatizadas-y-cómo-demostrar-el-factory-method).)*

**a) Resultado de las pruebas** (ejecutor compatible con xUnit; ver las limitaciones en la [sección 11](#11-limitaciones-conocidas-del-prototipo)):

```
Carpeta/namespace             Total     OK  Fallos  Omitidas
ReportDecorator                 46     46       0         0     (sin modificar: los reportes siguen igual)
Visitor                        135    135       0         0
```

**b) Análisis de la flota con la API en marcha** (Development, datos de demostración y la simulación acelerada ×5). Respuesta real de
`GET /api/analysis/fleet` (se muestran los totales; se omiten `generatedAt` y las listas `byType`, `vehicles`, `byStatus` y `routes`, que
repiten el detalle):

```json
{
  "elements":    { "vehicles": 5, "routes": 3, "maintenanceRecords": 4 },
  "utilization": { "totalVehicles": 5, "operableVehicles": 4, "inUseVehicles": 1, "utilizationPercent": 25,
                   "totalMileageKm": 233996.955, "averageMileageKm": 46799.391 },
  "routes":      { "totalRoutes": 3, "totalDistanceKm": 682.15, "completedDistanceKm": 9.5, "activeDistanceKm": 287.65,
                   "plannedDistanceKm": 385, "averageDistanceKm": 227.383, "delayedRoutes": 0, "totalDelayMinutes": 0 },
  "maintenance": { "totalRecords": 4, "vehiclesWithRecords": 4, "vehiclesWithoutRecords": ["VAN-002"] }
}
```

Las cifras se pueden verificar a mano: 9.5 + 287.65 + 385 = 682.15 km; 682.15 / 3 rutas = 227.383 km; 1 vehículo en uso de 4 operables = 25 %.
Los hallazgos de esa misma respuesta, del más al menos urgente:

```json
[
  { "code": "EnRouteVehicleWithoutActiveRoute", "severity": "Warning", "subject": "CAR-003",
    "message": "El vehículo CAR-003 figura En ruta pero no tiene ninguna ruta en curso: la simulación no lo mueve." },
  { "code": "ServiceDistanceExceeded", "severity": "Warning", "subject": "VAN-002",
    "message": "El vehículo VAN-002 no tiene ningún mantenimiento registrado y lleva 18346.955 km (umbral: 10000 km)." },
  { "code": "ServiceDistanceExceeded", "severity": "Warning", "subject": "VAN-005",
    "message": "El vehículo VAN-005 lleva 89250 km desde su último mantenimiento (umbral: 10000 km)." },
  { "code": "PlannedRouteWithoutVehicle", "severity": "Info", "subject": "Ruta Bucaramanga - Bogotá",
    "message": "La ruta planificada 'Ruta Bucaramanga - Bogotá' todavía no tiene vehículo asignado." }
]
```

**c) El análisis reacciona al estado del vehículo** (mismo recorrido). Alguien deja a `TRK-001` «Disponible» a mano mientras su ruta sigue
en marcha (En ruta → Disponible está permitido): el análisis lo avisa.

```json
{ "code": "ActiveRouteVehicleNotEnRoute", "severity": "Warning", "subject": "Ruta de evidencia TRK-001",
  "message": "La ruta 'Ruta de evidencia TRK-001' está en curso pero su vehículo TRK-001 no está En ruta (estado: Available): la simulación no la mueve." }
```

Después se manda a `TRK-001` al taller con la ruta aún en curso: el hallazgo pasa a **crítico** y **el motivo lo dice el estado** del vehículo
(es el mismo mensaje que usan las reglas de asignación):

```json
{ "code": "ActiveRouteWithoutOperableVehicle", "severity": "Critical", "subject": "Ruta de evidencia TRK-001",
  "message": "La ruta 'Ruta de evidencia TRK-001' está en curso pero no puede avanzar. El vehículo TRK-001 está en mantenimiento; no se puede usar en una ruta." }
```

**d) Pruebas de mutación** (se rompieron a propósito 9 reglas del código del Visitor, de a una, en una copia; ver el detalle y el
resultado en la [sección 12](#12-pruebas-automatizadas-y-cómo-demostrar-el-factory-method)):

| Id | Rotura deliberada (se aplicó de a una, en una copia) | Resultado | Una de las pruebas que la detecta |
|----|------|------|------|
| V1 | Las rutas canceladas cuentan en los totales de distancia | Detectada · 3 pruebas fallan | `RouteDistanceVisitorTests.TheTotals_CountOnlyTheRoutesThatWereNotCancelled` |
| V2 | El uso de vehículos compara el enum en vez de preguntar al estado | Detectada · 2 pruebas fallan | `FleetAnalysisServiceTests.TheService_UsesTheStateResolverItIsGiven` |
| V3 | El historial acepta intervalos de kilometraje negativos | Detectada · 1 prueba falla | `MaintenanceHistoryVisitorTests.AnIntervalThatWouldBeNegative_IsDiscarded_ItIsADataEntryMistakeNotADistance` |
| V4 | El umbral de retraso es estricto (> en vez de >=) | Detectada · 1 prueba falla | `OperationalInsightsVisitorTests.ADelayOnAnActiveRoute_IsReportedFromTheThreshold` |
| V5 | La estructura no ordena los vehículos | Detectada · 2 pruebas fallan | `FleetElementsTests.TheStructure_ListsVehiclesThenRoutesThenRecords_EachInAStableOrder` |
| V6 | El aviso "ruta en curso con vehículo que no la sigue" nunca se emite | Detectada · 3 pruebas fallan | `FleetAnalysisServiceTests.ALeftoverAvailableVehicleOnAnActiveRoute_ShowsUpAsAWarning` |
| V7 | Los avisos salen del menos al más urgente | Detectada · 2 pruebas fallan | `FleetAnalysisServiceTests.TheInsights_SayWhatAnOperatorShouldAttendFirst` |
| V8 | El elemento de vehículo pierde el kilometraje al copiarse (queda en 0) | Detectada · 20 pruebas fallan | `FleetAnalysisServiceTests.TheUtilization_ReflectsTheStatesOfTheRealVehicles` |
| V9 | La estructura no ofrece las rutas a los visitantes | Detectada · 48 pruebas fallan | `FleetAnalysisServiceTests.TheUtilization_ReflectsTheStatesOfTheRealVehicles` |

**Conclusión: 9 de 9 roturas detectadas; ninguna sobrevivió.** Cada rotura se comparó contra la suite completa (1438 pruebas); «detectada» significa que la compilación falló, que al menos una prueba falló o que la ejecución se interrumpió.

### 8.21 Template Method — la hoja de ruta: un esqueleto, contenido distinto por audiencia

Una ruta de reparto la leen personas distintas con necesidades distintas. **El conductor** necesita saber qué recorrer y cómo seguirlo
(paradas en orden, enlace de navegación, próxima parada y hora estimada). **Quien despacha** necesita saber cómo va la operación
(cuánta carga lleva frente a lo que el vehículo puede cargar, cuánto se ha retrasado, cuánto lleva recorrido). Los pasos para armar
la hoja son siempre los mismos —cargar y validar, encabezado, paradas, carga, avance, navegación, ensamblar— y lo único que cambia
es **qué aporta cada audiencia en cada paso**. Escribir un servicio con un `if (audiencia == ...)` repetiría el esqueleto y
obligaría a editarlo con cada audiencia nueva. El **Template Method** fija el esqueleto en una clase base y deja que cada variante
complete solo los pasos que le corresponden.

> **Objetivo que cubre.** Complementa la **operación de las rutas y el seguimiento de los viajes**: reutiliza lo que ya existe
> (navegación, avance de los viajes, reglas de capacidad de la asignación) y lo presenta por audiencia, **sin modificar** ninguno de
> los patrones anteriores.

> **No es el Template Method de los comandos.** El proyecto ya tenía un Template Method dentro de `Application/Commands/RouteCreationCommand.cs`
> (el esqueleto «crear la ruta, guardar el recuerdo, registrar el resultado» de las órdenes de creación). **Ese archivo no se tocó
> y no se reutiliza**: esta sección es otro patrón, con otra jerarquía, otro espacio de nombres (`Application/RouteSheets`) y otro
> propósito (presentar datos, no ejecutar una orden con deshacer).

#### 1. Descripción del patrón

Template Method define en una clase base el **esqueleto de un algoritmo** y difiere algunos pasos a las subclases, de modo que estas
redefinan **pasos** del algoritmo sin cambiar su **estructura**. Aquí el esqueleto es `RouteSheetTemplate.BuildAsync(routeId)`, un
método público **no virtual**: ninguna variante puede reordenarlo, saltarse un paso ni dejar de llamar a la parte fija. Los pasos que
varían son miembros `protected`: dos son **abstractos** (toda audiencia debe decidirlos) y tres son **hooks** con un valor por
defecto válido («no aportar nada»).

```
cargar y validar (fijo) → encabezado → paradas → carga → avance → navegación → ensamblar la hoja (fijo)
        │                    abstracto   hook     hook   abstracto   hook
        └─ si la ruta no existe o no tiene vehículo, falla aquí: ningún paso variable llega a ejecutarse
```

#### 2. Justificación de su utilización

| Problema real del proyecto | Cómo lo resuelve el Template Method |
|----------------------------|--------------------------------------|
| La hoja del conductor y la del despachador comparten el mismo recorrido de armado y difieren en el contenido de cada paso. | El recorrido vive **una sola vez** en la clase base; cada audiencia es una clase pequeña que completa sus pasos. |
| Validar la ruta (existe, tiene vehículo, su conductor existe) no debe repetirse ni poder olvidarse en una variante nueva. | La carga y la validación son un paso **fijo** de la base: una variante nueva no puede omitirlo. |
| Hay pasos que a una audiencia no le sirven (el conductor no necesita el resumen de carga; el despachador no necesita las paradas ni el enlace). | Son **hooks** con valor por defecto (sin paradas, sin carga, sin enlace): la variante no implementa un método vacío ni lanza `NotSupportedException`. |
| Una audiencia nueva (por ejemplo, un cliente) no debe obligar a editar lo que ya funciona. | Es una clase nueva y **una línea** de registro; el proveedor y el controlador no cambian. |

**Qué le toca a cada patrón (no se duplican responsabilidades):**

| Patrón | Responsabilidad que **conserva** | Qué hace el Template Method de la hoja en esa frontera |
|--------|----------------------------------|--------------------------------------------------------|
| **Template Method de los comandos** | El esqueleto de la creación de rutas con historial. | Nada: no lo usa, no lo hereda y no lo modifica. |
| **Adapter y Proxy** ([8.7](#87-patrones-estructurales-adapter-bridge-composite-y-decorator), [8.10](#810-proxy--caché-del-proveedor-de-navegación)) | Generar el enlace de navegación (y cachearlo). | La hoja del conductor **le pide** el enlace a `INavigationService`; no construye URLs. |
| **Avance de los viajes** (`ITripProgressService`, [8.18](#818-observer--publicar-la-telemetría-del-recorrido-sin-que-el-simulador-sepa-quién-escucha)) | Decidir qué viajes están activos y cuánto llevan. | La base lo consulta **una vez** y entrega a cada variante el viaje de **esta** ruta; no repite la regla de «viaje activo». |
| **State** ([8.19](#819-state--qué-puede-hacer-un-vehículo-según-su-estado)) | Qué puede hacer un vehículo según su estado. | Nada nuevo: un vehículo que no puede seguir un viaje (por ejemplo, en Mantenimiento) simplemente no tiene viaje activo y la hoja queda sin datos de viaje (porcentaje, próxima parada y ETA). No se cambia ningún estado. |
| **Strategy y reglas de asignación** ([8.11](#811-strategy--optimizar-paradas-y-repartir-cargas), [8.12](#812-decorator--reglas-de-asignación-sobre-el-servicio-de-rutas)) | Decidir cómo se asigna la carga. | La comparación carga/capacidad de la hoja usa el **mismo criterio** (peso contra `CapacityKg`, volumen solo si el vehículo declara `CapacityM3`, con tolerancia mínima), pero **solo informa**: no valida ni rechaza. |

#### 3. Ubicación de la implementación

Todo es nuevo y vive en `src/FleetManagement.Application/RouteSheets/`:

| Archivo | Qué contiene |
|---------|--------------|
| `RouteSheetTemplate.cs` | La **clase abstracta**: `BuildAsync` (no virtual), los dos pasos abstractos, los tres hooks y el registro `RouteSheetCollaborators` (cargador, navegación y avance en un solo parámetro). |
| `DriverRouteSheet.cs` | Variante del **conductor**. |
| `DispatcherRouteSheet.cs` | Variante del **despachador**. |
| `CargoCapacitySummary.cs` | Regla única (interna) de la comparación carga/capacidad. |
| `RouteSheetSourceLoader.cs` | El paso fijo «cargar y validar»: `IRouteSheetSourceLoader` y su implementación sobre los repositorios. |
| `RouteSheetModels.cs` | La audiencia (`RouteSheetAudience`), los datos que reciben los pasos (`RouteSheetSource`) y la hoja resultante (`RouteSheetDto` con su encabezado, carga y avance). |
| `RouteSheetProvider.cs` | `IRouteSheetProvider`: recibe **todas** las plantillas registradas y entrega la de la audiencia pedida. |
| `src/FleetManagement.Api/Controllers/RouteSheetsController.cs` | `GET api/route-sheets/{routeId}?audience=Driver\|Dispatcher`. |
| **Modificado:** `src/FleetManagement.Api/Program.cs` | Los registros (sección «Integración»). **Ningún patrón, servicio, entidad ni prueba existente se modificó.** |
| `frontend/js/routesheet.js` (nuevo) | La presentación, compartida por el panel y el portal del operador: botón, ventana, selector de audiencia y los cinco pasos del esqueleto. Solo dibuja lo que entrega la API. |
| **Modificados (solo conexión):** `frontend/js/api.js`, `frontend/dashboard.html`, `frontend/operator.html` | Una función `getRouteSheet` y una etiqueta `<script>` en cada página. |
| Pruebas: `tests/FleetManagement.Application.Tests/RouteSheets/` y `Architecture/RouteSheetAndInterpreterDesignTests.cs` | Ver «Pruebas de funcionamiento». |

#### 4. Diagrama UML

```mermaid
classDiagram
    direction LR
    class RouteSheetTemplate {
        <<abstract>>
        +Audience RouteSheetAudience
        +BuildAsync(routeId) RouteSheetDto
        #BuildHeader(source) RouteSheetHeader
        #BuildProgress(source) RouteSheetProgress
        #BuildStops(source) TripStopDto list
        #BuildCargo(source) RouteSheetCargo
        #BuildNavigationAsync(source) string
    }
    class DriverRouteSheet {
        paradas + navegacion + proxima parada
    }
    class DispatcherRouteSheet {
        carga frente a capacidad + retraso
    }
    class RouteSheetCollaborators {
        <<record>>
        Source
        Navigation
        Progress
    }
    class IRouteSheetSourceLoader {
        <<interface>>
        +LoadAsync(routeId) RouteSheetSource
    }
    class INavigationService {
        <<interface>>
    }
    class ITripProgressService {
        <<interface>>
    }
    class IRouteSheetProvider {
        <<interface>>
        +For(audience) RouteSheetTemplate
    }
    class RouteSheetProvider
    RouteSheetTemplate <|-- DriverRouteSheet
    RouteSheetTemplate <|-- DispatcherRouteSheet
    RouteSheetTemplate --> RouteSheetCollaborators : recibe
    RouteSheetCollaborators --> IRouteSheetSourceLoader
    RouteSheetCollaborators --> INavigationService
    RouteSheetCollaborators --> ITripProgressService
    IRouteSheetProvider <|.. RouteSheetProvider
    RouteSheetProvider o-- RouteSheetTemplate : todas las registradas
```

```mermaid
flowchart TD
    A["BuildAsync(routeId) - NO virtual"] --> B["1. Cargar y validar - fijo"]
    B -->|"ruta inexistente: 404 / sin vehiculo: 400"| X(["se detiene: ningun paso variable corre"])
    B --> C["2. Encabezado - abstracto"]
    C --> D["3. Paradas - hook, por defecto ninguna"]
    D --> E["4. Carga - hook, por defecto ninguna"]
    E --> F["5. Avance - abstracto"]
    F --> G["6. Navegacion - hook, por defecto ninguna"]
    G --> H["Ensamblar RouteSheetDto - fijo"]
```

#### 5. Interfaces y clases participantes

| Participante | Rol | Detalle |
|--------------|-----|---------|
| `RouteSheetTemplate` | **Clase abstracta** (esqueleto) | `BuildAsync` es el único método público y no es virtual. Recibe sus colaboradores por constructor (`RouteSheetCollaborators`), no por herencia. |
| `BuildHeader`, `BuildProgress` | Pasos **abstractos** | Toda audiencia decide su encabezado y qué dice del avance (aunque sea «nada»). |
| `BuildStops`, `BuildCargo`, `BuildNavigationAsync` | **Hooks** | Valor por defecto válido: lista vacía o `null`. Una variante los sobrescribe solo si aporta algo. |
| `DriverRouteSheet` | Variante concreta (`sealed`) | Paradas en orden (origen, intermedias y destino, con las mismas etiquetas que el avance del viaje), enlace de `INavigationService` y, si el viaje está en curso, próxima parada, kilómetros que faltan y ETA. |
| `DispatcherRouteSheet` | Variante concreta (`sealed`) | Resumen de carga frente a capacidad, retraso de la ruta, porcentaje y kilómetros que faltan (si el viaje está en curso) y nombre del conductor. |
| `IRouteSheetSourceLoader` | Paso fijo, detrás de una interfaz | Carga ruta, vehículo y conductor; lanza `KeyNotFoundException` o `InvalidOperationException`. |
| `RouteSheetProvider` | Selección sin `switch` | Indexa las plantillas por su `Audience`; no conoce ninguna clase concreta. |

#### 6. Funcionamiento dentro del sistema

**Qué aporta cada audiencia en cada paso:**

| Paso | Conductor (`Driver`) | Despachador (`Dispatcher`) |
|------|----------------------|----------------------------|
| Encabezado | «Hoja de ruta del conductor», ruta, estado, placa y fecha programada | «Hoja de despacho», lo mismo y el **nombre del conductor** (si la ruta lo tiene) |
| Paradas | Origen, paradas en orden y destino | — (lista vacía) |
| Carga | — | Artículos, peso total y capacidad, porcentaje de uso, volumen (y su capacidad si el vehículo la declara) y si **excede** la capacidad |
| Avance | Kilómetros que faltan, próxima parada y ETA (solo si el viaje está en curso) | Porcentaje y kilómetros que faltan (si el viaje está en curso) y **minutos de retraso** de la ruta |
| Navegación | Enlace de navegación externa | — |

**Errores.** La parte fija decide, antes de cualquier paso variable:
ruta inexistente → `404` (`Ruta no encontrada.`); ruta sin vehículo → `400` con el nombre de la ruta; vehículo o conductor asignados que ya
no existen → `404` (un dato inconsistente no se imprime en silencio sin conductor). Una **audiencia** ausente o desconocida → `400` con
los valores válidos (`Audiencia desconocida: 'Pilot'. Valores válidos: Dispatcher, Driver.`). Los números del enum (`0`, `1`) **no** son audiencias.

**Endpoint** (nuevo, de lectura y sin rol, igual que las demás consultas de rutas):

```
GET /api/route-sheets/{routeId}?audience=Driver
GET /api/route-sheets/{routeId}?audience=Dispatcher
```

Forma de la respuesta (**ilustrativa**: muestra los campos, no es salida de una ejecución). Las partes que una audiencia no aporta
**no se escriben** (la API omite los nulos) o llegan vacías (`stops: []`):

```json
{
  "audience": "Dispatcher",
  "header": { "title": "Hoja de despacho", "routeName": "...", "status": "InProgress", "licensePlate": "...", "driverName": "..." },
  "stops": [],
  "cargo": { "items": 2, "totalWeightKg": 0, "capacityKg": 0, "weightUsagePercent": 0, "totalVolumeM3": 0, "exceedsCapacity": false },
  "progress": { "percent": 0, "remainingKm": 0, "delayMinutes": 0 }
}
```

**Cómo verlo en la interfaz.**

1. **Panel administrativo** → pestaña «Rutas y cargas» → «📄 Hoja de ruta» en la fila de una ruta. La ventana abre con la audiencia **Conductor**; el selector cambia a **Despachador**, y «Probar audiencia inválida» muestra el 400 con los valores válidos.
2. Se dibujan siempre los **mismos cinco pasos, en el mismo orden**, cada uno con su etiqueta (*abstracto* u *hook*) y el método de la clase base que lo declara. Lo que cambia al cambiar de audiencia es el **contenido**: los pasos que esa audiencia no aporta quedan **atenuados** con la nota «el hook queda en su valor por defecto». Eso es lo que hace visible el patrón.
3. **Portal del operador** → «📄 Hoja de ruta» en la barra inferior: muestra la hoja del **conductor** de su ruta activa (sin selector: el conductor no elige su audiencia).
4. Para ver la próxima parada y la hora estimada, la ruta debe estar **En progreso** y su vehículo **En ruta**; si no hay viaje en curso, el paso «Avance» lo dice.

**Solo lectura.** Armar una hoja no cambia el estado de ningún vehículo ni ruta, no escribe en la bitácora y no envía avisos (una prueba lo comprueba).

#### 7. Integración con los demás componentes

**Cambios en archivos existentes:** `Api/Program.cs` (los `using` y estos registros) y, solo para conectar la pantalla, `frontend/js/api.js` (una función) y las dos páginas HTML (una etiqueta `<script>` cada una). La lógica de la interfaz es un archivo nuevo, `frontend/js/routesheet.js`.

```csharp
builder.Services.AddScoped<IRouteSheetSourceLoader, RouteSheetSourceLoader>();
builder.Services.AddScoped<RouteSheetCollaborators>();
builder.Services.AddScoped<RouteSheetTemplate, DriverRouteSheet>();
builder.Services.AddScoped<RouteSheetTemplate, DispatcherRouteSheet>();
builder.Services.AddScoped<IRouteSheetProvider, RouteSheetProvider>();
```

| Pieza | Ciclo de vida | Por qué |
|-------|---------------|---------|
| Cargador, colaboradores, plantillas y proveedor | **Scoped** | Las plantillas usan servicios Scoped (navegación y avance de los viajes); un Singleton los capturaría. |
| `INavigationService`, `ITripProgressService`, repositorios | Los que ya estaban registrados | No se modificaron. |

**Agregar una audiencia** (por ejemplo, `Customer`): un valor en `RouteSheetAudience`, una clase que herede de `RouteSheetTemplate` y una línea
`AddScoped<RouteSheetTemplate, ...>`. El proveedor, el controlador, el esqueleto y las otras variantes no cambian; el mensaje de «valores válidos»
ya lo incluye porque sale de las plantillas registradas.

**Qué no cambió.** `RouteCreationCommand` y todo `Application/Commands`, el Adapter y el Proxy de navegación, `TripProgressService`,
`DeliveryRouteService`, los estados del vehículo y todas las pruebas existentes.

#### 8. Principios SOLID aplicados

| Principio | Cómo se cumple |
|-----------|----------------|
| **S** — Responsabilidad única | El esqueleto ordena; el cargador carga y valida; cada variante decide el contenido de **su** audiencia; el proveedor solo elige; la comparación de carga es una clase aparte. |
| **O** — Abierto/Cerrado | Una audiencia nueva es una clase y una línea de registro (una prueba registra una plantilla ajena y el proveedor la entrega sin cambios). |
| **L** — Sustitución de Liskov | Cualquier variante puede usarse donde se espera `RouteSheetTemplate`: ninguna lanza `NotSupportedException`, ninguna deja un método vacío ni cambia el orden (no se puede: `BuildAsync` no es virtual). Una prueba paramétrica ejecuta el mismo contrato sobre **todas** las variantes que encuentra por reflexión. |
| **I** — Segregación de interfaces | Contratos de un solo método: `IRouteSheetSourceLoader.LoadAsync` e `IRouteSheetProvider.For`; la plantilla usa `INavigationService` e `ITripProgressService` tal como existen. |
| **D** — Inversión de dependencias | La plantilla depende de interfaces (cargador, navegación y avance), inyectadas por composición; `Application` no referencia `Infrastructure` ni `Api`. |

#### 9. Límites y decisiones

- **Cuántos pasos.** El esqueleto tiene siete etapas, pero solo **cinco** son puntos de extensión (dos abstractos y tres hooks); el resto (cargar y ensamblar) es fijo. Se mantuvo por debajo de seis pasos variables.
- **Hooks con valor por defecto.** «No aportar nada» es legítimo para una audiencia; por eso hay hooks y no métodos abstractos que obligarían a escribir cuerpos vacíos (*Refused Bequest*). Ninguna variante necesita llamar a la base (*Call Super*).
- **Sin política por conductor.** El proyecto no tiene una regla de «un conductor solo ve sus rutas» que reproducir; por eso la consulta no exige rol, igual que `GET api/routes/{id}` y la navegación. Si esa política se crea, es una decisión de acceso aparte.
- **Solo datos existentes.** No se inventó ningún campo: todo sale de la ruta, el vehículo, el conductor de la ruta y el avance de los viajes.
- **Avance una sola vez.** La base consulta todos los viajes activos y filtra el de la ruta: es la única consulta que ofrece `ITripProgressService`; no se agregó otra.
- **Alcance del diseño.** Las pruebas de arquitectura leen las firmas de los tipos, no el cuerpo de los métodos.

#### 10. Pruebas de funcionamiento

Carpeta `tests/FleetManagement.Application.Tests/RouteSheets/` y `Architecture/RouteSheetAndInterpreterDesignTests.cs`, sin librerías de mocking
(dobles escritos a mano y, para la integración, los servicios reales de navegación y de avance sobre los repositorios en memoria):

| Archivo | Métodos de prueba* | Qué demuestra |
|---------|-------------------:|----------------|
| `RouteSheetTemplateTests.cs` | 11 | El **orden exacto** de los pasos con una variante espía (`load, trips, header, stops, cargo, progress, navigation`); que `BuildAsync` no es virtual y es la única operación pública; que una ruta inexistente o sin vehículo se rechaza **antes** de cualquier paso variable (ni siquiera se consulta el avance); que cada paso recibe el viaje de **esta** ruta; que una variante mínima obtiene los valores por defecto y nunca pide la navegación. |
| `RouteSheetVariantTests.cs` | 24 | Un **contrato paramétrico** sobre cada variante (descubiertas por reflexión): solo recibe los colaboradores, arma la hoja con su audiencia, funciona sin conductor, sin carga y sin viaje activo, solo sobrescribe miembros protegidos; el contenido de cada audiencia con los servicios reales (paradas `Origen, Primera, Segunda, Destino`, próxima parada y ETA iguales a los del servicio de avance, carga frente a capacidad, retraso, nombre del conductor); los casos límite de la carga (justo la capacidad, error de redondeo, un gramo de más, volumen no declarado, capacidad 0, sin artículos); que armar una hoja no cambia nada ni escribe auditoría ni avisos. |
| `RouteSheetProviderTests.cs` | 9 | La plantilla correcta por audiencia (sin distinguir mayúsculas ni espacios); audiencia desconocida o ausente con la lista de válidas; los números del enum no valen; los valores válidos salen de lo registrado; una plantilla ajena registrada se entrega sin cambiar el proveedor; dos plantillas para la misma audiencia se rechazan. |
| `RouteSheetSourceLoaderTests.cs` | 6 | Ruta, vehículo y conductor cargados; sin conductor es válido; ruta, vehículo o conductor inexistentes y ruta sin vehículo, con su mensaje. |
| `Architecture/RouteSheetAndInterpreterDesignTests.cs` (parte del Template Method) | — | El código existente no conoce `RouteSheets`; solo depende del dominio y de abstracciones; solo `BuildAsync` y `Audience` son públicos; los puntos de extensión son `protected` y no pasan de seis; solo los dos pasos obligatorios son abstractos; las variantes son `sealed` y solo sobrescriben miembros protegidos; no hay estado estático mutable. |

\* Se cuentan **métodos** `[Fact]`/`[Theory]`; una `[Theory]` se ejecuta una vez por cada fila de datos, así que el total de casos es mayor.

```bash
dotnet test --filter "FullyQualifiedName~FleetManagement.Application.Tests.RouteSheets"
dotnet test --filter "FullyQualifiedName~RouteSheetAndInterpreterDesignTests"
dotnet test                       # toda la suite
```

**Estado de la verificación.** Este código y sus pruebas se escribieron **sin poder compilar ni ejecutar nada** (el entorno de trabajo no tiene
`dotnet`). Se revisaron a mano (lectura, balance de llaves, tamaño de métodos y clases, diff contra el original), pero **no hay resultados
de ejecución que mostrar**: ejecute `dotnet build` y `dotnet test` antes de darlo por bueno.

**Interfaz.** La presentación (`frontend/js/routesheet.js`) se probó en un Chromium sin cabeza contra una **API simulada** (sin Leaflet ni backend reales): botón por ruta sin duplicarse al redibujar la tabla, selector de audiencia, orden fijo de los pasos, pasos atenuados, el 400 de una audiencia inválida, el portal del operador, el escape del HTML y que un enlace que no sea http(s) no se escriba en un `href`. **No** se probó contra el backend real: eso queda por verificar al ejecutarlo.

### 8.22 Interpreter — reglas de mantenimiento y filtros de monitoreo escritos como texto

El módulo predictivo ya avisa cuándo un vehículo necesita mantenimiento, pero sus umbrales están escritos en el código. Quien administra
la flota quiere **formular sus propias reglas** («vehículos con 8 000 km o más desde el último servicio, o más de 180 días sin
revisión») y **filtrar el monitoreo** («camiones disponibles sin conductor») sin esperar un despliegue. Aceptar una expresión de texto y
ejecutarla con `eval`, reflexión o compilación dinámica sería inseguro. El **Interpreter** da un lenguaje pequeño y cerrado, con su
gramática, que se convierte en un **árbol de objetos** y se evalúa contra los datos de cada vehículo.

> **Objetivo que cubre.** Complementa el **mantenimiento predictivo** y el **monitoreo de la flota**: son consultas de **solo lectura**
> que **no reemplazan** al módulo predictivo (`MaintenanceService`) ni al análisis de la flota ([8.20](#820-visitor--analizar-la-flota-sin-cargar-las-entidades-con-análisis)); los complementan. La regla
> «de Mantenimiento a Disponible solo registrando el mantenimiento» ([8.19](#819-state--qué-puede-hacer-un-vehículo-según-su-estado)) sigue intacta: el intérprete nunca cambia un estado.

#### 1. Descripción del patrón

Interpreter representa una gramática como una **jerarquía de clases**: cada regla de la gramática es una expresión y cada expresión sabe
**interpretarse** a sí misma contra un contexto. Aquí hay seis expresiones, todas con una única operación, `Interpret(context)`:

| Expresión | Gramática | Qué devuelve |
|-----------|-----------|--------------|
| `LiteralExpression` | número o `"texto"` | su valor |
| `VariableExpression` | nombre de variable | lo que el contexto responde para ese nombre |
| `ComparisonExpression` | `valor op valor` | `bool` |
| `NotExpression` | `NOT` expresión | `bool` |
| `AndExpression` | expresión `AND` expresión | `bool` (no evalúa el lado derecho si el izquierdo ya es falso) |
| `OrExpression` | expresión `OR` expresión | `bool` (no evalúa el lado derecho si el izquierdo ya es verdadero) |

**Gramática** (la precedencia sale de la forma de las producciones: `NOT` antes que `AND`, y `AND` antes que `OR`):

```
rule       := or
or         := and ("OR" and)*
and        := not ("AND" not)*
not        := "NOT" not | comparison | "(" rule ")"
comparison := value op value
op         := =  |  !=  |  >  |  >=  |  <  |  <=
value      := número | "texto" | variable
```

Las palabras `AND`, `OR` y `NOT` no distinguen mayúsculas, ni los nombres de variable, ni la comparación de textos (`"available"` = `"Available"`).
Los números aceptan signo y decimales (`-5.5`). El árbol de `kmSinceLastService >= 8000 OR daysSinceLastService > 180` es:

```
Or
├─ Comparison(>=)  ── Variable(kmSinceLastService), Literal(8000)
└─ Comparison(>)   ── Variable(daysSinceLastService), Literal(180)
```

#### 2. Justificación de su utilización

| Problema real del proyecto | Cómo lo resuelve el Interpreter |
|----------------------------|----------------------------------|
| Los umbrales del mantenimiento predictivo están fijos en el código; cambiarlos exige compilar y desplegar. | Una regla es un texto que el administrador envía por la API; el árbol se construye en cada petición. |
| Hay que filtrar vehículos por combinaciones de estado, tipo, kilometraje, capacidad y conductor. | Los operadores `AND`, `OR`, `NOT` y los paréntesis combinan comparaciones; no hace falta un endpoint por filtro. |
| Evaluar texto escrito por una persona es una superficie de ataque. | No hay `eval`, reflexión ni compilación dinámica: el analizador **solo puede producir las seis clases de expresión**; las variables permitidas son una lista cerrada; la regla tiene límites de longitud y de anidamiento. |
| Dos usos distintos (mantenimiento y monitoreo) con datos distintos. | Las expresiones **no cambian**: lo que cambia es el **contexto** (qué variables existen y qué valen). |

**Qué le toca a cada patrón (no se duplican responsabilidades):**

| Patrón | Responsabilidad que **conserva** | Qué hace el Interpreter en esa frontera |
|--------|----------------------------------|------------------------------------------|
| **Módulo predictivo de mantenimiento** (`MaintenanceService`) | Calcular qué vehículos están vencidos con sus reglas actuales. | Nada: no lo reemplaza ni lo llama; ofrece reglas adicionales sobre los mismos datos. |
| **State** ([8.19](#819-state--qué-puede-hacer-un-vehículo-según-su-estado)) | Decidir qué estado puede pasar a cuál; Mantenimiento → Disponible solo registrando el mantenimiento. | Solo **lee** `status`; no cambia ningún estado. |
| **Visitor** ([8.20](#820-visitor--analizar-la-flota-sin-cargar-las-entidades-con-análisis)) | Análisis fijos de la flota sobre elementos de tipos distintos. | Es otro problema: aquí lo que varía es la **regla**, no la operación. No comparte código con el análisis. |
| **Strategy** ([8.11](#811-strategy--optimizar-paradas-y-repartir-cargas)) | Intercambiar algoritmos. | Nada: las expresiones no son algoritmos intercambiables, son un árbol que se evalúa. |
| **Template Method de la hoja de ruta** ([8.21](#821-template-method--la-hoja-de-ruta-un-esqueleto-contenido-distinto-por-audiencia)) | Armar la hoja de ruta. | Nada: los dos patrones no se referencian entre sí (una prueba de diseño lo vigila). |

#### 3. Ubicación de la implementación

Todo es nuevo y vive en `src/FleetManagement.Application/Interpreter/`:

| Archivo | Qué contiene |
|---------|--------------|
| `IExpression.cs` | El contrato de las expresiones: **una** operación, `Interpret(context)`, con el tipo de su resultado. |
| `IRuleContext.cs` | El **contexto** de solo lectura: **un** método, `Resolve(variable)`. |
| `TerminalExpressions.cs` | Expresiones **terminales**: `LiteralExpression` y `VariableExpression`. |
| `ComparisonExpression.cs`, `ComparisonOperators.cs` | La comparación y la **tabla** símbolo → operador (sin `switch`). |
| `LogicalExpressions.cs` | **No terminales**: `AndExpression`, `OrExpression`, `NotExpression`. |
| `RuleValue.cs` | Valor de una regla: número, texto o **sin dato** (`Missing`), comparados por doble despacho (sin preguntar por el tipo). |
| `Token.cs`, `RuleTokenizer.cs` | El **analizador léxico**: texto → piezas con su posición. Fuera de las expresiones. |
| `RuleGrammar.cs`, `RuleParser.cs`, `RuleLimits.cs`, `RuleVocabulary.cs` | El **analizador sintáctico** (descenso recursivo, un método por producción), los límites y la lista cerrada de variables. Fuera de las expresiones. |
| `RuleSyntaxException.cs` | Error propio, con **mensaje en español y posición**. |
| `VariableContext.cs`, `MaintenanceRuleContext.cs`, `VehicleMonitoringContext.cs`, `RuleVariables.cs` | Los dos **contextos** y los nombres de las variables. |
| `RuleScopes.cs`, `RuleEvaluationService.cs` | Qué vehículos y variables usa cada caso de uso y el servicio de solo lectura que los une. |
| `src/FleetManagement.Api/Controllers/RulesController.cs` | `POST api/rules/maintenance/evaluate` y `POST api/rules/vehicles/filter`. |
| **Modificado:** `src/FleetManagement.Api/Program.cs` | Los registros (sección «Integración»). **Ningún patrón, servicio, entidad ni prueba existente se modificó.** |
| Pruebas: `tests/FleetManagement.Application.Tests/Interpreter/` y `Architecture/RouteSheetAndInterpreterDesignTests.cs` | Ver «Pruebas de funcionamiento». |

#### 4. Diagrama UML

```mermaid
classDiagram
    direction LR
    class IExpression {
        <<interface>>
        +Interpret(context) TResult
    }
    class IRuleContext {
        <<interface>>
        +Resolve(variable) RuleValue
    }
    class LiteralExpression
    class VariableExpression
    class ComparisonExpression
    class AndExpression
    class OrExpression
    class NotExpression
    class VariableContext {
        <<abstract>>
    }
    class MaintenanceRuleContext
    class VehicleMonitoringContext
    IExpression <|.. LiteralExpression : terminal
    IExpression <|.. VariableExpression : terminal
    IExpression <|.. ComparisonExpression
    IExpression <|.. AndExpression
    IExpression <|.. OrExpression
    IExpression <|.. NotExpression
    AndExpression o-- IExpression : izquierda y derecha
    OrExpression o-- IExpression : izquierda y derecha
    NotExpression o-- IExpression : operando
    ComparisonExpression o-- IExpression : dos valores
    VariableExpression ..> IRuleContext : pregunta
    IRuleContext <|.. VariableContext
    VariableContext <|-- MaintenanceRuleContext
    VariableContext <|-- VehicleMonitoringContext
```

```mermaid
flowchart LR
    T["Texto de la regla"] --> L["RuleTokenizer - piezas con posicion"]
    L --> G["RuleGrammar - descenso recursivo"]
    V["RuleVocabulary - variables permitidas"] --> G
    G -->|"RuleSyntaxException con posicion: 400"| E(["La API responde 400 con message"])
    G --> A["Arbol de expresiones - inmutable"]
    A --> I["Interpret por cada vehiculo"]
    C["Contexto del vehiculo - MaintenanceRuleContext o VehicleMonitoringContext"] --> I
    I --> R["Vehiculos que cumplen la regla"]
```

#### 5. Interfaces y clases participantes

| Participante | Rol | Detalle |
|--------------|-----|---------|
| `IExpression<TResult>` | **Expresión abstracta** | Una sola operación. La gramática queda en los **tipos**: `And`, `Or` y `Not` reciben `IExpression<bool>`; la comparación recibe dos `IExpression<RuleValue>`. |
| `LiteralExpression`, `VariableExpression` | **Terminales** | Un valor y una pregunta al contexto. |
| `ComparisonExpression`, `AndExpression`, `OrExpression`, `NotExpression` | **No terminales** | Se construyen con otras expresiones; son registros **inmutables** (se comparan por estructura y se pueden compartir). |
| `IRuleContext` | **Contexto** | Solo lectura, un método. Una variable sin dato responde `RuleValue.Missing`. |
| `MaintenanceRuleContext` | Contexto de mantenimiento | `mileageKm`, `vehicleAgeYears`, `daysSinceLastService`, `kmSinceLastService`, `type`, `status`. El «hoy» sale de `TimeProvider`. |
| `VehicleMonitoringContext` | Contexto de monitoreo | `status`, `type`, `mileageKm`, `capacityKg`, `hasDriver`. |
| `RuleTokenizer`, `RuleGrammar`, `RuleParser` | **Cliente** que construye el árbol | Separados de las expresiones: estas solo interpretan. |
| `RuleEvaluationService` | Caso de uso | Analiza la regla **antes** de leer los repositorios, y luego la interpreta sobre cada vehículo. |

**Variables.**

| Variable | Contexto | Qué vale |
|----------|----------|----------|
| `mileageKm` | ambos | Kilometraje del vehículo |
| `status`, `type` | ambos | Nombre del estado (`Available`, `EnRoute`, `Maintenance`, `OutOfService`) y del tipo (`Truck`, `Van`, `Car`, `Motorcycle`) |
| `vehicleAgeYears` | mantenimiento | Año actual menos el año del vehículo (nunca negativo) |
| `daysSinceLastService` | mantenimiento | Días **enteros** desde el último mantenimiento (el de fecha más reciente; con la misma fecha, el de más kilómetros); nunca negativo |
| `kmSinceLastService` | mantenimiento | Kilometraje del vehículo menos el del último mantenimiento; nunca negativo |
| `capacityKg` | monitoreo | Capacidad de carga |
| `hasDriver` | monitoreo | **1** con conductor asignado y **0** sin él: la gramática no tiene booleanos, así que se usa un número |

**Una variable sin dato nunca coincide.** Si un vehículo **no tiene ningún mantenimiento**, `daysSinceLastService` y `kmSinceLastService` no
valen 0: **no tienen dato**, y **toda comparación con un dato faltante es falsa** (con cualquier operador, `!=` incluido). No se inventa un 0
ni se lanza una excepción. Consecuencia documentada: `NOT kmSinceLastService > 5` **sí** coincide con un vehículo sin mantenimientos, porque la
comparación es falsa y `NOT` la invierte. Y comparar un número con un texto es igualmente falso.

#### 6. Funcionamiento dentro del sistema

**Endpoints** (nuevos; el controlador exige el rol **Admin**, igual que la consulta de la bitácora; usan `POST` solo porque la regla viaja en el cuerpo, **no modifican nada**):

| Método | Ruta | Cuerpo | Para qué |
|--------|------|--------|----------|
| POST | `/api/rules/maintenance/evaluate` | `{ "rule": "kmSinceLastService >= 8000 OR daysSinceLastService > 180" }` | Vehículos que cumplen una regla de mantenimiento predictivo |
| POST | `/api/rules/vehicles/filter` | `{ "rule": "status = \"Available\" AND type = \"Truck\"" }` | Vehículos que cumplen un filtro de monitoreo |

Respuesta (forma **ilustrativa**, no es salida de una ejecución): `{ "rule": "...", "evaluatedVehicles": 5, "matchedVehicles": 1, "vehicles": [ { "vehicleId": "...", "licensePlate": "...", "type": "Truck", "status": "Available" } ] }`.

**Regla inválida → 400** con `{ "message": ... }` en español y la posición del problema (el primer carácter es el 1):

| Regla | Mensaje |
|-------|---------|
| `mileageKm >` | `Se esperaba un valor (número, texto entre comillas o variable) pero se encontró el final de la regla. (posición 12)` |
| `fuel > 5` | `La variable 'fuel' no existe. Variables permitidas: …. (posición 1)` (lista las del caso de uso) |
| `mileageKm > 5)` | `Sobra ')' después de la regla. (posición 14)` |
| `a = 1; DROP` | `Carácter inesperado ';' (posición 6)` |
| *(vacía)* | `La regla está vacía. (posición 1)` |

**Seguridad.** No hay `eval`, reflexión ni compilación dinámica; no se leen propiedades por nombre (las variables son una lista cerrada
por contexto, y `System.Environment`, `_hidden` o `a.b` ni siquiera se tokenizan); la longitud máxima por defecto es **500** caracteres y el
anidamiento máximo (paréntesis y `NOT` encadenados) es **10**, para que una regla no pueda agotar la pila ni la CPU.

**Solo lectura.** Evaluar una regla no cambia vehículos, estados ni mantenimientos; no audita ni notifica (una prueba lo comprueba).

#### 7. Integración con los demás componentes

**Cambios en archivos existentes:** solo `Api/Program.cs`.

```csharp
builder.Services.AddSingleton(RuleLimits.Default);
builder.Services.AddSingleton<IRuleParser, RuleParser>();
builder.Services.AddScoped<MaintenanceRuleScope>();
builder.Services.AddScoped<MonitoringRuleScope>();
builder.Services.AddScoped<IRuleEvaluationService>(provider => new RuleEvaluationService(
    provider.GetRequiredService<IRuleParser>(),
    provider.GetRequiredService<MaintenanceRuleScope>(),
    provider.GetRequiredService<MonitoringRuleScope>()));
```

| Pieza | Ciclo de vida | Por qué |
|-------|---------------|---------|
| `RuleParser` y `RuleLimits` | **Singleton** | No guardan estado entre reglas (cada regla crea su propio analizador interno). |
| Los dos alcances (`MaintenanceRuleScope`, `MonitoringRuleScope`) y el servicio | **Scoped** | Leen repositorios y el reloj como los demás servicios. Son del mismo tipo de interfaz (`IRuleScope`), por eso se cablean a mano, como el Decorator de las reglas de asignación. |
| Expresiones y contextos | **No se registran** | Se crean en cada petición con los datos de ese momento. |

**Con el resto del sistema.** El alcance de mantenimiento lee los vehículos y sus registros de mantenimiento (los repositorios que ya existían)
y el reloj; el de monitoreo, solo los vehículos. Agregar un tercer caso de uso es un contexto y un alcance nuevos: **las expresiones y el
analizador no cambian** (una prueba define un contexto ajeno y evalúa una regla sobre él).

**Qué no cambió.** `MaintenanceService` y su módulo predictivo, `VehicleStateContext` y los estados, el análisis de la flota (Visitor), los
reportes y todas las entidades de dominio.

#### 8. Principios SOLID aplicados

| Principio | Cómo se cumple |
|-----------|----------------|
| **S** — Responsabilidad única | Cada expresión es una regla de la gramática; el tokenizador reconoce piezas; la gramática arma el árbol; el contexto responde variables; el alcance dice qué vehículos y variables; el servicio orquesta. |
| **O** — Abierto/Cerrado | Un operador de comparación nuevo es **una fila** en la tabla; un contexto nuevo no cambia ninguna expresión. Un operador **lógico** nuevo sí exige una expresión nueva y un cambio en la gramática: ese conjunto es cerrado a propósito. |
| **L** — Sustitución de Liskov | Cualquier `IExpression<bool>` se puede usar donde se espera una; cualquier `IRuleContext` sirve a cualquier expresión; `Missing` es un valor más que se compara como no comparable, sin excepciones. |
| **I** — Segregación de interfaces | Contratos de uno o dos miembros: `IExpression` (1), `IRuleContext` (1), `IRuleParser` (1), `IRuleScope` (2). |
| **D** — Inversión de dependencias | Las expresiones solo conocen `IRuleContext`; el servicio depende de `IRuleParser`, `IRuleScope` y de `TimeProvider`; `Application` no referencia `Infrastructure` ni `Api`. |

#### 9. Límites y decisiones

- **Sin booleanos ni funciones.** La gramática es mínima a propósito; `hasDriver` es 1/0 por eso. Agregar funciones o aritmética sería ampliar el lenguaje, no una consecuencia del patrón.
- **Sin dato ≠ 0.** Un vehículo sin mantenimientos no cumple reglas sobre días o kilómetros desde el último servicio (ni siquiera `>= 0`), pero sí cumple `NOT kmSinceLastService > 5`. Es una consecuencia de la regla «falso si falta el dato», no un error.
- **Solo datos existentes.** No se agregó ningún campo a las entidades: `vehicleAgeYears` sale del año del vehículo y las cifras de servicio del historial de mantenimiento.
- **Las reglas no se guardan.** Se evalúan en la petición; no hay reglas persistentes ni programadas (eso sería otra funcionalidad).
- **Rendimiento.** Cada petición construye el árbol y lo evalúa sobre todos los vehículos: adecuado para una flota de prototipo; con miles de vehículos habría que limitar o paginar.
- **Alcance del diseño.** Las pruebas de arquitectura leen las firmas de los tipos; que no haya `eval` ni reflexión se vigila además a nivel de ensamblado y con pruebas de comportamiento del analizador, no leyendo el cuerpo de cada método.

#### 10. Pruebas de funcionamiento

Carpeta `tests/FleetManagement.Application.Tests/Interpreter/`, sin librerías de mocking (dobles escritos a mano, `ManualTimeProvider` para el reloj
y la flota real en memoria para el caso de uso):

| Archivo | Métodos de prueba* | Qué demuestra |
|---------|-------------------:|----------------|
| `ExpressionTests.cs` | 15 | Cada expresión por separado, sin analizador: literales, variable (pregunta al contexto), `AND`/`OR`/`NOT` con su tabla de verdad y su cortocircuito, los seis operadores con números y textos, número contra texto, dato faltante, la tabla de operadores e inmutabilidad (igualdad por estructura). |
| `RuleTokenizerTests.cs` | 10 | Piezas y posiciones, palabras clave sin distinguir mayúsculas, operadores sin espacios (el más largo primero), textos con símbolos y palabras reservadas, números con signo y decimales, y los errores de carácter con su posición. |
| `RuleParserTests.cs` | 21 | **Tabla de reglas válidas e inválidas** con el mensaje y la posición esperados; la **precedencia** (`NOT` > `AND` > `OR`) con tablas de verdad; paréntesis; variables fuera de la lista; **límites** de longitud y de anidamiento; el analizador no guarda estado y se usa desde varios hilos. |
| `RuleContextTests.cs` | 18 | Los dos contextos con un reloj manual: edad, último servicio (por fecha, y a igualdad por kilómetros), días enteros, nunca negativos, **sin mantenimientos = sin dato**, el vocabulario exacto de cada contexto, `hasDriver` 1/0, la regla predictiva con sus umbrales exactos y que el contexto no modifica nada. |
| `RuleEvaluationServiceTests.cs` | 12 | **Extremo a extremo** con la flota real: reglas de mantenimiento y filtros de monitoreo con los vehículos esperados; una regla inválida se rechaza **antes** de leer los repositorios; es de solo lectura (sin cambios, auditoría ni avisos) y un vehículo en Mantenimiento sigue en Mantenimiento; con los datos de demostración; un caso de uso ajeno se agrega sin cambiar el analizador ni las expresiones. |
| `Architecture/RouteSheetAndInterpreterDesignTests.cs` (parte del Interpreter) | — | El código existente no conoce `Interpreter`; solo depende del dominio y de repositorios; sin Roslyn ni generación dinámica de código; interfaces mínimas; seis expresiones inmutables con una sola operación pública; solo dos terminales; valores inmutables; sin estado estático mutable. |

\* Se cuentan **métodos** `[Fact]`/`[Theory]`; una `[Theory]` se ejecuta una vez por cada fila de datos, así que el total de casos es mayor.

```bash
dotnet test --filter "FullyQualifiedName~FleetManagement.Application.Tests.Interpreter"
dotnet test --filter "FullyQualifiedName~RouteSheetAndInterpreterDesignTests"
dotnet test                       # toda la suite
```

**Estado de la verificación.** Igual que en 8.21: se escribió **sin poder compilar ni ejecutar nada** (sin `dotnet` en el entorno de trabajo). Se
revisó a mano, pero **no hay resultados de ejecución que mostrar** y ninguna cifra de pruebas «verdes»: ejecute `dotnet build` y `dotnet test` antes de darlo por bueno.

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
| GET | `/api/monitoring/telemetry` | Última telemetría conocida de cada vehículo que ha estado en viaje desde que arrancó la aplicación (posición, kilometraje, avance, próxima parada, última parada alcanzada y si ya llegó), ordenada por placa. A diferencia de `/api/progress`, **sigue disponible cuando el viaje termina**. Se alimenta de los eventos que publica el simulador (Observer, [8.18](#818-observer--publicar-la-telemetría-del-recorrido-sin-que-el-simulador-sepa-quién-escucha)) | — |
| GET | `/api/monitoring/telemetry/{vehicleId}` | La misma información de un solo vehículo. 404 si no ha estado en viaje desde que arrancó la aplicación | — |
| POST | `/api/fleet/vehicles` | Crea un vehículo (Factory Method + Abstract Factory) | Admin |
| POST | `/api/fleet/vehicles/{id}/clone` | Clona un vehículo plantilla (Prototype) | Admin |
| PATCH | `/api/fleet/vehicles/{id}/status` | Cambia el estado de un vehículo. El estado actual decide a cuáles se puede pasar (State, [8.19](#819-state--qué-puede-hacer-un-vehículo-según-su-estado)): responde 400 con el motivo si no se permite (por ejemplo, de Mantenimiento o Fuera de servicio directamente a En ruta) o si el texto no es un estado; 404 si el vehículo no existe | Admin |
| GET | `/api/drivers` | Lista de conductores | — |
| GET | `/api/routes` | Lista de rutas | — |
| GET | `/api/routes/{id}` | Detalle de una ruta | — |
| GET | `/api/routes/driver/{driverId}` | Rutas asignadas a un conductor | — |
| GET | `/api/routes/{id}/cargo-manifest` | Manifiesto jerárquico de la carga de una ruta (Composite) | — |
| GET | `/api/routes/{id}/cargo-items` | Los mismos artículos como lista plana, en el orden del recorrido en profundidad del manifiesto (Iterator, [8.15](#815-iterator--recorrer-la-carga-de-una-ruta-sin-conocer-su-estructura)). 404 si la ruta no existe | — |
| POST | `/api/routes` | Crea una ruta (Builder). Responde 400 si la carga excede la capacidad del vehículo, si este no está operativo o tiene mantenimiento vencido ([8.12](#812-decorator--reglas-de-asignación-sobre-el-servicio-de-rutas)) | Admin |
| POST | `/api/routes/express` | Crea una ruta express (Builder + Director). Mismas reglas de asignación que `POST /api/routes` | Admin |
| POST | `/api/routes/{id}/duplicate` | Duplica una ruta (Prototype) | Admin |
| PATCH | `/api/routes/{id}/status` | Cambia el estado de una ruta. Al iniciarla (`InProgress`/`Delayed`) responde 400 si el vehículo o el conductor ya están en otro viaje activo o el vehículo no está operativo. Después de guardar el estado, el vehículo asignado se ajusta (queda En ruta, o Disponible al terminar si no sigue en otro viaje; Mediator, [8.16](#816-mediator--coordinar-el-estado-de-la-ruta-con-el-del-vehículo)) | — |
| GET | `/api/routes/{id}/optimization` | Propone un mejor orden de las paradas **sin modificar la ruta**: lista actual y propuesta, kilómetros y minutos de cada una, ahorro (Strategy) | — |
| POST | `/api/routes/{id}/optimization` | Aplica el orden propuesto (solo rutas planificadas; 400 si no lo está; si ya es el mejor, no cambia nada) | Admin |
| POST | `/api/dispatch/plan` | Calcula, **sin guardar nada**, el reparto de una lista de entregas entre los vehículos disponibles: viajes con sus paradas en orden, carga, kilómetros y minutos; entregas sin asignar con su motivo; vehículos excluidos con su motivo; comparación con el reparto simple (Strategy) | Admin |
| POST | `/api/dispatch/apply` | Recalcula el plan y crea una ruta **planificada** por viaje (todo o nada) | Admin |
| POST | `/api/commands/routes` | Igual que `POST /api/routes`, pero la operación queda en el historial del usuario y se puede deshacer ([8.13](#813-command--deshacer-y-rehacer-operaciones-de-rutas)) | Admin |
| POST | `/api/commands/routes/express` | Igual que `POST /api/routes/express`, con historial | Admin |
| POST | `/api/commands/routes/{id}/duplicate` | Igual que `POST /api/routes/{id}/duplicate`, con historial | Admin |
| POST | `/api/commands/routes/{id}/optimization` | Igual que `POST /api/routes/{id}/optimization`, con historial | Admin |
| POST | `/api/commands/dispatch/apply` | Igual que `POST /api/dispatch/apply`, con historial | Admin |
| GET | `/api/commands/history` | Lo que el usuario puede deshacer y rehacer, de lo más reciente a lo más antiguo | Admin |
| POST | `/api/commands/undo` | Deshace la última operación del usuario (elimina la ruta creada o restaura el orden de las paradas). 400 si no hay nada que deshacer; 409 si ya no se puede | Admin |
| POST | `/api/commands/redo` | Rehace la última operación deshecha (crea rutas nuevas con las reglas de hoy). 400 si no hay nada que rehacer; 409 si ya no se puede (la ruta sobre la que actuaba ya no existe) | Admin |
| GET | `/api/maintenance` | Historial de mantenimiento | — |
| GET | `/api/maintenance/vehicle/{vehicleId}` | Historial de un vehículo | — |
| GET | `/api/maintenance/due` | Vehículos con mantenimiento vencido (predictivo) | — |
| POST | `/api/maintenance` | Registra un mantenimiento | Admin |
| GET | `/api/alerts` | Lista de alertas de viaje | — |
| GET | `/api/alerts/route/{routeId}` | Alertas de una ruta | — |
| POST | `/api/alerts` | Reporta una complicación durante el viaje. Pasa por una cadena de manejadores ([8.14](#814-chain-of-responsibility--de-la-alerta-reportada-a-quien-debe-atenderla)): responde 400 si la descripción está vacía o pasa de 500 caracteres, o si el retraso pasa de 1440 minutos; las alertas graves y críticas se escalan | — (operador) |
| PATCH | `/api/alerts/{id}/resolve` | Marca una alerta como resuelta | Admin |
| GET | `/api/navigation/route/{routeId}` | Enlace de navegación externa (Google Maps). Adapter detrás de un Proxy de caché | — |
| GET | `/api/audit/logs` | Registro de auditoría (Singleton) | Admin |
| GET | `/api/reports/fleet` | Reporte de la flota (vehículos y rutas). Con `?sections=Statistics,MaintenanceAlerts,CargoSummary` agrega esas secciones (Decorator) | — |
| GET | `/api/dashboard/summary` | Resumen general de la flota para el dashboard (Facade) | — |
| GET | `/api/analysis/fleet` | Análisis de la flota con los datos de este momento: utilización de los vehículos, recorridos y distancias de las rutas, historial de mantenimiento y hallazgos operativos del más al menos urgente (Visitor, [8.20](#820-visitor--analizar-la-flota-sin-cargar-las-entidades-con-análisis)). Es de solo lectura y no reemplaza a `GET /api/reports/fleet` | — |

**Ejemplo: planificar un reparto** (Git Bash; el token sale de `POST /api/auth/login` con `admin` / `Admin123!`):

```bash
curl -s -X POST http://localhost:5080/api/dispatch/plan \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{
    "originLat": 7.1193, "originLng": -73.1227,
    "deliveries": [
      { "description": "Piedecuesta", "weightKg": 50, "volumeM3": 0.5, "priority": "Standard", "latitude": 6.9876, "longitude": -73.0505 },
      { "description": "Girón", "weightKg": 20, "volumeM3": 0.3, "priority": "Standard", "latitude": 7.0806, "longitude": -73.1716 },
      { "description": "Floridablanca", "weightKg": 10, "volumeM3": 0.2, "priority": "Standard", "latitude": 7.0622, "longitude": -73.0864 },
      { "description": "Lebrija", "weightKg": 5, "volumeM3": 0.1, "priority": "Standard", "latitude": 7.1139, "longitude": -73.2194 }
    ]
  }'
```

`priority` puede ser `Standard`, `High`, `Urgent` o `Fragile` (si se omite, `Standard`); `vehicleIds` (opcional) limita el plan
a esos vehículos y `scheduledDate` (opcional) se copia a las rutas que cree `apply`. En la respuesta, `trips[].stops` trae
las paradas **ya ordenadas**, `totals` y `baseline` los totales del plan y del reparto simple, y `comparison` el ahorro. Los
datos inválidos (sin entregas, peso ≤ 0, coordenadas fuera de rango, prioridad desconocida, vehículo inexistente) responden
400 con `{ "message": "..." }`.

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
  la fórmula de **Haversine** entre origen, paradas y destino (`Application/Geo/GeoMath`,
  compartida con la simulación y con el plan de reparto) y con la **velocidad del tipo de
  vehículo** (`VehicleSpeed`, perfil Flyweight; sin vehículo, 55 km/h por defecto). El orden de
  las paradas y el reparto de entregas los deciden estrategias ([sección 8.11](#811-strategy--optimizar-paradas-y-repartir-cargas)):
  vecino más cercano + 2-opt, y "best fit" con prioridades. Son heurísticas sobre distancias
  **en línea recta**, no un solver de ruteo (VRP) ni un motor de calles; un siguiente paso
  natural sería implementar `IRouteOptimizer` con un motor de ruteo real (OSRM, Google Routes
  API, etc.) que dé distancias por carretera.
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
- **Telemetría, estados y análisis** ([8.18](#818-observer--publicar-la-telemetría-del-recorrido-sin-que-el-simulador-sepa-quién-escucha),
  [8.19](#819-state--qué-puede-hacer-un-vehículo-según-su-estado) y
  [8.20](#820-visitor--analizar-la-flota-sin-cargar-las-entidades-con-análisis)):
  - El tablero de telemetría (`LiveTelemetryBoard`) es un Singleton **en memoria**, igual que los repositorios: se pierde al reiniciar
    el proceso y no se comparte entre varias instancias de la API. En producción, la telemetría llegaría de los dispositivos o de un
    broker, y se guardaría en una base de series de tiempo o en un caché compartido; bastaría con otro `ITelemetryObserver` (registrado
    en `Program.cs`) y el simulador no cambiaría.
  - Los observadores se ejecutan de forma síncrona, uno a la vez, en el hilo que está entregando (normalmente el de la simulación).
    Uno que haga entrada/salida lenta (red, base de datos) debe encolar su trabajo en lugar de hacerlo dentro de `OnTelemetry`, para no
    atrasar el paso de la simulación.
  - El estado del vehículo sigue siendo la propiedad `Vehicle.Status`: State no agrega tablas ni columnas, así que el paso a EF Core no cambia
    por esto. Las transiciones permitidas viven en el código del dominio, no en la base de datos.
  - `GET /api/analysis/fleet` lee los tres repositorios en cada consulta (como los reportes). Con una base de datos real conviene resolver
    las agregaciones en consultas o cachear el resultado unos segundos.
- **CORS:** configurado como `AllowAnyOrigin()` para simplificar la evaluación
  del prototipo desde cualquier origen local. En producción debe restringirse al
  dominio real del frontend.

---

## 11. Limitaciones conocidas del prototipo

- **Cómo se verificó y qué falta verificar.** El entorno donde se generó esta versión no tenía acceso a NuGet, así que
  **no se ejecutaron `dotnet restore` / `dotnet test` reales**. `Domain`, `Application`, `Infrastructure` (sin `FleetDbContext.cs`,
  que usa EF Core) y `Api` se compilaron con el SDK de .NET 8 y el framework compartido de ASP.NET Core, con sustitutos mínimos
  de `NetTopologySuite` (`Point`) y de Swashbuckle, y las pruebas se ejecutaron con un ejecutor mínimo compatible con xUnit
  (solo para esa verificación, no forma parte del proyecto). La API se levantó y se probó por HTTP, y el frontend se probó en
  un navegador sin interfaz (Chromium) con los mosaicos del mapa simulados. Al recibirlo, ejecute `dotnet build` y
  `dotnet test` (esperado: **1438** pruebas superadas: las 1013 anteriores más las 425 de Observer, State y Visitor,
  [sección 12](#12-pruebas-automatizadas-y-cómo-demostrar-el-factory-method)) y avise de cualquier diferencia. Las secciones 8.18
  a 8.20 se verificaron con el mismo tipo de arnés (compilación sin NuGet y ejecutor mínimo compatible con xUnit, con
  NetTopologySuite compilado desde su código fuente y las aserciones de xUnit reales); su API se levantó y se probó por HTTP, y el
  frontend no se modificó en esa entrega ni se volvió a probar en un navegador. El frontend no tiene pruebas automatizadas dentro
  del proyecto.
- El cálculo de "próximo mantenimiento vencido" compara la fecha/kilometraje
  registrados manualmente en cada `MaintenanceRecord`; no hay integración con
  telemetría real del vehículo para el kilometraje actual (se actualiza junto
  con la simulación de movimiento).
- La simulación recorre la ruta en línea recta entre el origen, las paradas y el
  destino (lo mismo que dibuja el mapa): no sigue las calles ni simula tráfico. El
  avance de los viajes vive en memoria, como el resto de los datos, y se pierde al
  reiniciar la API.
- **Optimización y asignación** ([8.11](#811-strategy--optimizar-paradas-y-repartir-cargas)): las distancias son en línea
  recta (no hay red de calles ni tráfico) y los algoritmos son heurísticas que no garantizan el óptimo. El tiempo y los
  kilómetros de un viaje se cuentan desde el punto de salida: el trayecto del vehículo hasta ese punto solo decide **qué
  vehículo sale primero**, no se suma. El plan no tiene en cuenta ventanas horarias, regreso al depósito, varios depósitos ni
  horas de conducción, y no asigna conductor (las rutas que crea quedan sin conductor). Con capacidad escasa prioriza lo
  urgente y lo pesado, y puede dejar más entregas pequeñas sin asignar que el orden simple (se informa cuáles y por qué).
  Un vehículo con rutas *planificadas* que aún no han salido sigue siendo candidato de un plan nuevo: la doble asignación
  se impide al **iniciar** el viaje, no al planificar. Un plan admite hasta 200 entregas.
- **Reglas de asignación** ([8.12](#812-decorator--reglas-de-asignación-sobre-el-servicio-de-rutas)): la capacidad se
  comprueba al crear la ruta y las reglas de disponibilidad también al iniciarla; duplicar una ruta no repite la
  comprobación al crear la copia. El estado "vencido" del mantenimiento sale del módulo predictivo actual (fecha o kilometraje
  registrados a mano).
- **Deshacer y rehacer** ([8.13](#813-command--deshacer-y-rehacer-operaciones-de-rutas)): solo cubre las operaciones de rutas
  de esa sección (crear una ruta por el formulario, «⚡ Ruta express» o «⧉ Duplicar», aplicar el orden optimizado y aplicar el
  plan de reparto); cambiar el estado de una ruta, el resto de las pantallas (vehículos, mantenimiento, alertas) y los endpoints
  anteriores no dejan historial. Deshacer **elimina** las rutas que la operación creó (no las deja canceladas) y solo se permite
  mientras siguen planificadas; la comprobación y la eliminación no son una operación atómica entre usuarios (si otra persona
  inicia una de esas rutas justo en medio, podría eliminarse una ruta que ya salió). Una ruta eliminada no se recupera: rehacer no
  la devuelve, vuelve a ejecutar la operación con los datos y las reglas de hoy y **crea rutas nuevas** (con otro identificador); si
  una regla ya no se cumple falla con su motivo y la operación sigue disponible para rehacer. Si una operación posterior actuaba
  sobre una ruta que se eliminó (por ejemplo, optimizarla), esa operación ya no se puede rehacer (409) y se quita del historial. El historial es **por nombre de usuario**, vive en memoria, guarda las 20 operaciones
  más recientes y se pierde al reiniciar la API (no se comparte entre varias instancias).
- **Cadena de alertas** ([8.14](#814-chain-of-responsibility--de-la-alerta-reportada-a-quien-debe-atenderla)): escalar solo
  **avisa y deja constancia**; no resuelve ni reasigna nada (resolver sigue siendo manual) y no hay re-escalamiento por tiempo (una
  alerta grave sin atender no sube sola a la administración). Los destinatarios son dos direcciones de la configuración, no se
  eligen por ruta, turno ni conductor, y la gravedad usa reglas simples (tipo y minutos de retraso). Si el aviso de escalamiento
  falla tras agotar los reintentos, la alerta ya quedó guardada pero la API responde con error (no hay bandeja de salida para
  reintentarlo después). `POST /api/alerts` ahora rechaza descripciones vacías o de más de 500 caracteres y retrasos de más de
  1440 minutos, que antes aceptaba.
- **Iterator, Mediator y Memento** ([8.15](#815-iterator--recorrer-la-carga-de-una-ruta-sin-conocer-su-estructura), [8.16](#816-mediator--coordinar-el-estado-de-la-ruta-con-el-del-vehículo) y [8.17](#817-memento--volver-al-orden-anterior-de-las-paradas)): el iterador es de un solo uso y no está pensado para
  recorrer un árbol mientras otro proceso lo modifica (cada nodo se lee al visitarlo); `GET /api/routes/{id}/cargo-items` no lo
  consume ninguna pantalla todavía. El Mediator coordina solo la ruta y su vehículo: `VehicleService` (cambio manual del estado) y
  `MaintenanceService` (registrar un mantenimiento) siguen sin avisar a las rutas, como antes (desde la
  [8.19](#819-state--qué-puede-hacer-un-vehículo-según-su-estado) cambian el estado del vehículo preguntándole a State, pero no pasan por el Mediator).
  Una alerta con retraso deja la ruta Retrasada **sin** tocar al vehículo (comportamiento que ya existía y que se conservó a
  propósito; pedir el estado Retrasada por la API sí lo deja En ruta), y sigue sin comprobar el estado anterior de la ruta (una ruta
  ya terminada que recibe una alerta con retraso queda Retrasada). El Memento cubre solo el orden de las paradas y las estimaciones
  al optimizar (no el estado de la ruta, del vehículo ni la carga) y vive en el historial en memoria de cada usuario, que se pierde al
  reiniciar la API.
- **Observer, State y Visitor** ([8.18](#818-observer--publicar-la-telemetría-del-recorrido-sin-que-el-simulador-sepa-quién-escucha),
  [8.19](#819-state--qué-puede-hacer-un-vehículo-según-su-estado) y
  [8.20](#820-visitor--analizar-la-flota-sin-cargar-las-entidades-con-análisis)):
  - *Observer.* La telemetría solo cubre tres eventos del recorrido (movimiento, parada alcanzada y llegada); no publica alertas ni cambios
    de estado. El tablero y los observadores viven en memoria y por proceso: `GET /api/monitoring/telemetry` solo conoce a los
    vehículos que han viajado desde que arrancó la API y se pierde al reiniciarla. La entrega es síncrona y de un evento a la vez: un
    observador lento atrasa a los demás y al siguiente paso de la simulación. Un observador que falla queda aislado y auditado
    (categoría «Monitoreo»), pero ese evento no se reintenta; si los observadores se publican eventos entre sí en cadena, a partir
    de 1000 publicaciones en una misma entrega se descarta el exceso y también se audita. Ninguna pantalla del frontend consume todavía
    la telemetría.
  - *State.* El estado se sigue guardando solo en `Vehicle.Status`; no hay historial de transiciones y un cambio rechazado no deja
    auditoría ni notificación (solo el 400). La tabla de transiciones manuales está en el código, no es configurable. Hay dos cambios
    de comportamiento respecto a la versión anterior: pasar a mano de Mantenimiento o Fuera de servicio directamente a En ruta, y pedir
    un número que no es un estado (por ejemplo `"99"`), ahora responden 400 (antes se aceptaban). Algunas comparaciones directas del
    enumerado se dejaron a propósito porque no deciden comportamiento por estado (se enumeran en 8.19).
  - *Visitor.* Cada consulta de `GET /api/analysis/fleet` lee y recorre todo (no es incremental ni se cachea). Los umbrales de los
    hallazgos (30 minutos de retraso y 10 000 km sin mantenimiento) son valores por defecto de `InsightThresholds`: no están en
    `appsettings.json`. Los hallazgos solo informan; no corrigen nada. Agregar un análisis nuevo es agregar un visitante; agregar un
    tipo nuevo de elemento a analizar obliga a tocar la interfaz del visitante y a todos los visitantes (la contrapartida conocida del
    patrón). No hay pantalla que lo consuma todavía.
- Los endpoints con "—" en la columna "Rol requerido" de la [sección 9](#9-documentación-de-la-api-rest) (todos los `GET`,
  incluido `GET /api/routes/{id}/optimization`, y algunos de escritura como `PATCH /api/routes/{id}/status`) responden sin exigir
  sesión; antes de producción conviene exigirla en todos.
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

### Pruebas de las reglas de asignación y de la optimización (secciones 8.11 y 8.12)

Con estas pruebas el total esperado en el mismo proyecto es de **603**: las 393 anteriores (sin ningún cambio en sus archivos), más
210 nuevas (cada fila de un `[Theory]` cuenta como una prueba). Siguen sin usar librerías de mocking.

| Carpeta | Qué demuestra |
|---------|----------------|
| `Assignment/` | `RouteAssignmentValidator`: peso y volumen (también justo en el límite y con sumas decimales), vehículo en mantenimiento o fuera de servicio, mantenimiento vencido, conductor inexistente, vehículo o conductor ya en otro viaje activo, y que se informan **todas** las infracciones juntas. `AssignmentValidatingRouteService`: valida antes de delegar, nunca llega al servicio real si falla una regla, no bloquea terminar ni cancelar y delega sin validar las consultas y el duplicado. `DeliveryRouteServiceAssignmentTests`: al terminar o cancelar solo se libera a un vehículo que estaba "en ruta" y el tiempo estimado usa la velocidad del tipo. |
| `Geo/` | `TripEstimate` (minutos = km ÷ velocidad; rechaza velocidades no positivas) y `VehicleSpeed` (velocidad del perfil del tipo; por defecto sin vehículo). |
| `Optimization/RouteOptimizerTests.cs` | `TourLength`, vecino más cercano y 2-opt: corrige un cruce, nunca devuelve algo más largo que su punto de partida (con y sin destino fijo), deja fijos el origen y el destino, es determinista y queda a menos de 1,35 veces del óptimo en instancias pequeñas (comparado con probar todas las permutaciones). |
| `Optimization/CargoAssignmentStrategyTests.cs`, `StopSequencerTests.cs` | Mejor ajuste con prioridades (urgentes primero, consolidación, apertura del vehículo que antes llega, motivos de lo no asignado, peso y volumen) frente al orden simple, y la secuencia de cada viaje (urgentes primero). Dos pruebas son **comprobaciones empíricas sobre un lote sembrado**, no garantías generales. |
| `Optimization/RouteOptimizationServiceTests.cs` | Proponer (sin cambiar nada) y aplicar el orden de una ruta real: kilómetros y minutos, renumeración de las paradas, origen y destino intactos, auditoría, solo rutas planificadas, ruta inexistente, y que una ruta ya en su mejor orden no se empeora. |
| `Optimization/DispatchPlanningServiceTests.cs` | El plan con repositorios reales: vehículos excluidos con motivo, entregas sin asignar con motivo, comparación (válida o no, con nota si recorre más), validación de datos con mensajes claros, y `apply`: una ruta planificada por viaje, todo o nada, sin iniciar viajes ni tocar vehículos, con auditoría y fecha programada. |

Para ejecutar sólo una parte: `dotnet test --filter "FullyQualifiedName~Assignment"` o `...~Optimization`.

**Cómo se comprobó que estas pruebas detectan fallos.** Durante el desarrollo se rompieron a propósito 36 reglas clave del
código (capacidad en kg y en m³, vehículo en mantenimiento o fuera de servicio, mantenimiento vencido, doble asignación de
vehículo y de conductor, liberación del vehículo, el 2-opt, el vecino más cercano, el mejor ajuste, la prioridad, el todo o
nada de `apply`, la protección de "nunca empeorar", la velocidad por tipo, etc.) y se volvieron a ejecutar las pruebas: las 36
rupturas fueron detectadas. Una (la protección de "nunca empeorar") no lo fue al principio y por eso se agregó la prueba que
faltaba. Es una verificación del desarrollo (ejecutada en el arnés descrito en la [sección 11](#11-limitaciones-conocidas-del-prototipo)),
no algo que se ejecute con `dotnet test`.

### Pruebas de Command y de Chain of Responsibility (secciones 8.13 y 8.14)

Con estas pruebas el total esperado en el mismo proyecto es de **840**: las 603 anteriores (ninguna de esas pruebas cambió; solo
ganó una línea un archivo de apoyo, `Simulation/SimulationFakes.cs`, porque su repositorio de rutas de prueba debe implementar el
nuevo `DeleteAsync`) más 237 nuevas (cada fila de un `[Theory]` cuenta como una prueba). Siguen sin usar librerías de mocking: se usan repositorios en
memoria reales y dobles escritos a mano.

| Carpeta o archivo | Qué demuestra |
|-------------------|----------------|
| `Command/` (122) | `CommandHistory`: orden de deshacer y rehacer, capacidad de 20 (descarta la más antigua), una operación nueva descarta lo rehacible y exclusión para que dos peticiones no se pisen. `CommandInvoker`: ejecuta y anota (con la hora del reloj y el usuario en la bitácora), no guarda lo que falla ni lo que no cambió nada, un 409 quita la operación del historial y una falla pasajera la conserva, dos «deshacer» a la vez no deshacen lo mismo dos veces y cada usuario tiene su historial. `InMemoryCommandHistoryStore`: un historial por usuario (sin distinguir mayúsculas ni espacios). Las tres órdenes con repositorios reales: crear (deshacer ELIMINA la ruta, rehacer crea una nueva; repetido cinco veces no acumula rutas), orden optimizado (`RouteSnapshotService` restaura orden, distancia y tiempo; se niega si la ruta salió, ya no existe o sus paradas cambiaron) y plan de reparto (`PlannedRouteDeleter` elimina todas o ninguna, deja constancia en la auditoría y no toca al vehículo). Las órdenes que dependen de una ruta eliminada: rehacer una optimización o una copia cuya ruta ya no existe se niega con el motivo y se quita del historial (no queda un «Rehacer» atascado). `RouteRepositoryDeleteTests` (4): el borrado del repositorio en memoria. `RouteCreationCommandsTests` (25): las dos formas de crear una ruta que faltaban, una por una —«⚡ express» y «⧉ duplicar» se ejecutan y quedan en el historial; deshacer elimina la ruta creada (la copia, no la original); rehacer crea una nueva; una regla incumplida no deja historial; una ruta que ya salió se niega a deshacerse—, y para las tres formas el mismo comportamiento por compartir la base (`[Theory]`), mezcladas y deshechas de la más reciente a la más antigua. |
| `AlertHandling/` (102) | `AlertHandlerChain`: orden, quien no puede atender nunca corta la cadena, quien consume la corta, una falla no se oculta y un manejador nuevo se inserta sin tocar a los demás. `AlertReportContext`. Cada manejador: validar (con límites configurables), registrar, clasificar, escalar (nivel, prioridad, destinatario y bitácora; nunca resuelve la alerta) y dejar abierta. `AlertSeverityPolicy` y `AlertHandlingOptions` (valores por defecto y validación al arrancar). La cadena de producción completa con el servicio real y repositorios reales: leve, avería, accidente, retraso largo, reportes rechazados que no guardan nada y que escalar nunca resuelve. `AlertHandlingTripAlertService`: solo `CreateAlertAsync` pasa por la cadena. |
| `Assignment/DeliveryRouteServiceVehicleReleaseTests.cs` (6) | Terminar o cancelar una ruta no libera a un vehículo que sigue en otra ruta activa (en curso o retrasada) y sí libera al que solo estaba en esa. |
| `Architecture/CommandAndChainDesignTests.cs` (7) | El invocador, el historial y el almacén no conocen ninguna orden concreta; las órdenes y los manejadores solo reciben interfaces y datos (nunca repositorios ni clases concretas); las interfaces nuevas siguen siendo pequeñas; y el código que ya existía no conoce los patrones nuevos. Revisan las **firmas** de los tipos, no el cuerpo de los métodos. |

Para ejecutar sólo una parte: `dotnet test --filter "FullyQualifiedName~Command"` (129: las 122 de `Command/` y las 7 de arquitectura, cuyo nombre también lleva «Command») o `...~AlertHandling` (102).

**Cómo se comprobó que estas pruebas detectan fallos.** Durante el desarrollo se rompieron a propósito 63 reglas clave del
código (el orden y la capacidad del historial, el invocador, el historial por usuario, la foto y la restauración de paradas, el
todo o nada del cancelador (hoy «eliminador»), las tres órdenes, la liberación del vehículo, la validación, la política de gravedad, el
escalamiento —nivel, prioridad, destinatario, bitácora y que una falla no se oculte—, el orden de la cadena de producción, el
decorador, que escalar no resuelva la alerta y 7 violaciones de diseño para las pruebas de arquitectura) y se volvieron a
ejecutar las pruebas: las 63 rupturas fueron detectadas. Al corregir las tres formas de crear una ruta (versión 2) se rompieron
a propósito otras 13 reglas del código nuevo (la base común —deshacer, rehacer, `HasEffect` y el guardado de la ruta creada—, la
descripción y los datos de «⚡ express» y del formulario, que «⧉ duplicar» no pierda la fecha pedida ni el nombre de la copia, y que la fábrica
arme la fuente correcta y una orden distinta cada vez) y las 13 fueron detectadas. Al cambiar «Deshacer» para que elimine la ruta
(versión 3) se repitieron esas 13 (adaptadas al eliminador) y se agregaron 15 más sobre lo nuevo —que el eliminador compruebe todas
antes de borrar y no borre una ruta que ya salió, que borre de verdad en vez de cancelar, que deje constancia en la auditoría, que
el plan de reparto elimine todas sus rutas, el borrado del repositorio, y rehacer o deshacer una orden cuya ruta ya no existe—:
las 28 fueron detectadas. Las 63 primeras siguen sin repetirse con el código actual. Es una verificación del desarrollo (ejecutada en el arnés descrito en
la [sección 11](#11-limitaciones-conocidas-del-prototipo)), no algo que se ejecute con `dotnet test`.

### Pruebas de Iterator, Mediator y Memento (secciones 8.15, 8.16 y 8.17)

Con estas pruebas el total esperado en el mismo proyecto es de **1013**: las 840 anteriores más 173 nuevas (cada fila de un `[Theory]`
cuenta como una prueba). Ninguna prueba anterior cambió de contenido: en 10 archivos de prueba y de apoyo
solo cambió la línea que arma a mano `DeliveryRouteService` o `TripAlertService`, porque ahora reciben el participante de rutas
([8.16](#816-mediator--coordinar-el-estado-de-la-ruta-con-el-del-vehículo)), y `AlertHandlingKit` ganó una propiedad `Vehicles`. Siguen sin usar librerías de mocking: repositorios en memoria
reales y dobles escritos a mano.

| Archivo | Qué demuestra |
|---------|----------------|
| `Iterator/CargoIteratorTests.cs` (25) | El recorrido en profundidad y pre-orden de grupos y artículos (anidados, orden de los hijos, hoja suelta, grupo y ruta vacíos); `HasNext()` que no consume y `Next()` agotado (`InvalidOperationException`); dos iteradores independientes sobre el mismo árbol; sin duplicados (un componente compartido por dos grupos se entrega una vez mientras `Traverse()` lo repite) y un componente propio que forma un ciclo no genera un bucle infinito; un árbol muy profundo sin recursión; el filtro; que no modifica el árbol; y que entrega lo mismo que `Traverse()` (compatibilidad con el Composite). |
| `Iterator/CargoManifestItemsServiceTests.cs` (11) | La lista plana de una ruta real: orden, datos de cada artículo, que coincide con las hojas del manifiesto jerárquico y con los totales de su raíz, redondeo a 3 decimales, ruta sin carga (lista vacía), ruta inexistente (`null`) y que `GetManifestAsync` y la ruta no cambiaron. |
| `Mediator/FleetLifecycleMediatorTests.cs` (19) | El mediador solo, con participantes de prueba que registran lo que reciben: qué le ordena al de vehículos y cuándo le pregunta al de rutas, la respuesta de uno viaja hasta el otro, ruta sin vehículo, ruta planificada y la decisión de las alertas. |
| `Mediator/LifecycleParticipantsTests.cs` (33) | Cada participante por separado: el de vehículos (En ruta sin condiciones, liberar solo si iba En ruta y no sigue en otro viaje, vehículo inexistente) y el de rutas (guarda y *después* avisa al mediador, suma retrasos, qué es un viaje activo). |
| `Mediator/RouteVehicleLifecycleIntegrationTests.cs` (35) | Todo junto, con los servicios y los repositorios reales: las cuatro reglas de negocio por `UpdateRouteStatusAsync`, vehículos con otro viaje activo, vehículos en Mantenimiento o Fuera de servicio, ruta sin vehículo, errores de siempre (404 y 400 antes del mediador), **la misma línea de auditoría de antes** y ninguna notificación nueva, el Decorator de asignación, el retraso por alerta con la cadena y el puente de avisos, y el resumen del dashboard (Facade). |
| `Mediator/MediatorDesignTests.cs` (9) | Pruebas de arquitectura: todo vive en `Mediators`, los participantes no se conocen entre sí, el mediador no conoce repositorios ni servicios, de las piezas del Mediator los servicios solo conocen (por su interfaz) al participante de rutas, y ningún servicio depende de otro. |
| `Mediator/VehicleColleaguePersistenceTests.cs` (6) | Que el participante de vehículos **guarda** lo que cambia, comprobado con un repositorio que entrega copias (con el repositorio en memoria del proyecto, que entrega siempre el mismo objeto, un guardado olvidado pasaría inadvertido). |
| `Memento/RouteMementoTests.cs` (22) | El Originator crea el recuerdo con el orden real de las paradas y las estimaciones, sin información de más; es independiente de cambios posteriores de la ruta y de otros recuerdos; no se puede alterar ni fabricar desde fuera; restaura el orden, la distancia y la duración y deja lo demás igual; y rechaza (sin cambiar nada) una ruta que ya salió, que cambió de paradas o que ya no existe, un recuerdo ajeno y uno nulo. |
| `Memento/RouteMementoWithCommandTests.cs` (10) | El Caretaker (la orden) solo ve la interfaz estrecha y no la filtra; deshacer devuelve el recuerdo al Originator; deshacer y rehacer varias veces sin deriva; cada ejecución toma su propio recuerdo; dos optimizaciones se deshacen en orden inverso; si el Originator rechaza, la entrada sale del historial. |
| `Memento/RouteMementoPersistenceTests.cs` (3) | Lo restaurado queda guardado en el almacén (repositorio que entrega copias); capturar y una restauración rechazada no cambian nada. |
| `Support/CopyingRepositories.cs`, `Support/FleetMediators.cs` | Repositorios de prueba que entregan copias, y el armado del participante de rutas real para las pruebas que construyen los servicios a mano. |

Para ejecutar sólo una parte: `dotnet test --filter "FullyQualifiedName~Application.Tests.Iterator"` (36), `...~Application.Tests.Mediator` (102) o
`...~Application.Tests.Memento` (35).

**Pruebas de mutación de Iterator, Mediator y Memento.** Igual que en las versiones anteriores, se rompieron a propósito **68 reglas** del código
nuevo o reforzado —una por una, en una copia— y se volvieron a ejecutar las pruebas; una mutación «muere» si alguna prueba falla.
Las 68 murieron. Por patrón: **Iterator (16)** —el orden de los hijos, que no repita componentes, que `HasNext()` no consuma, que al terminar
no entregue `null`, que el filtro se aplique y no descarte el subárbol, la raíz nula, qué cuenta como artículo y, en la lista plana, la
posición inicial, el redondeo del peso y del volumen, la prioridad y el total—; **Mediator (31)** —lo que el mediador ordena y pregunta en
cada transición, que no ignore el otro viaje activo, que pregunte por la ruta correcta, la ruta sin vehículo, que el retraso de una alerta no
toque al vehículo, que el participante de rutas guarde y *después* avise, que sume los minutos y qué es un viaje activo, que el de vehículos
guarde de verdad lo que cambia, y que los servicios no se salten al participante de rutas ni dejen de auditar—; y **Memento (21)** —la copia
propia de las paradas, que el recuerdo no se pueda convertir en una lista modificable ni se fabrique desde fuera, que conserve cada dato en su
lugar (distancia y duración no intercambiadas), que el Originator rechace un recuerdo ajeno y una ruta que ya salió o cambió de paradas,
que restaure orden, lista, distancia y duración y que lo guarde, y que el Caretaker tome un recuerdo nuevo en cada ejecución y no deje una
orden irrecuperable en el historial—. En las primeras corridas **sobrevivieron 6**, y eran huecos reales de mis pruebas, no del producto:
2 sobre el redondeo de la lista plana, 3 sobre «guardar de verdad» lo que se cambia (el repositorio en memoria entrega siempre el mismo
objeto, así que un guardado olvidado no se notaba) y 1 sobre la guarda del deshacer. Agregué pruebas (con repositorios de prueba que
entregan copias) y las 6 murieron. Es una verificación del desarrollo (ejecutada en el arnés descrito en la [sección 11](#11-limitaciones-conocidas-del-prototipo)),
no algo que se ejecute con `dotnet test`.

### Pruebas de Observer, State y Visitor (secciones 8.18, 8.19 y 8.20)

Con estas pruebas el total esperado en el mismo proyecto es de **1438**: las 1013 anteriores más 425 nuevas (cada fila de un `[Theory]`
cuenta como una prueba). **Ningún archivo de pruebas anterior se modificó ni se eliminó**: los servicios y el simulador que cambiaron
solo ganaron parámetros opcionales al final de su constructor, de modo que las pruebas anteriores que los arman a mano compilan y
pasan tal cual. Siguen sin usar librerías de mocking: repositorios en memoria reales y dobles escritos a mano.

| Archivo | Qué demuestra |
|---------|----------------|
| `State/VehicleStateBehaviorTests.cs` (58) | Cada estado concreto: qué permite, su motivo de bloqueo con los mensajes originales, cómo reacciona a iniciar y terminar un viaje y **la matriz completa de transiciones manuales (16 casos, incluidas las no permitidas)**; que un valor indefinido se rechaza y que los estados son inmutables y compartidos. |
| `State/VehicleServiceStateTransitionTests.cs` (32) | **Transiciones no permitidas a través del servicio real**: un cambio permitido se guarda, se audita y se notifica; uno rechazado no se guarda, no se audita y no se notifica; texto inválido y vehículo inexistente responden como antes. |
| `State/VehicleStateMediatorCompatibilityTests.cs` (30) | Compatibilidad con el Mediator: las mismas cuatro reglas desde cada estado inicial, el vehículo que sigue en otro viaje no se libera y un estado alternativo cambia el resultado sin tocar al Mediator. |
| `State/VehicleStateMaintenanceRegistrationTests.cs` (26) | Qué le hace a cada estado registrar un mantenimiento (solo el taller sale de él) y que el servicio conserva el registro, la fecha, la auditoría y el aviso. |
| `State/VehicleStateTripTrackingTests.cs` (16) | La simulación solo mueve un viaje mientras la ruta está activa **y** el vehículo En ruta; la simulación y la consulta de avance coinciden. |
| `State/VehicleStateAssignmentTests.cs` (15) | Las reglas de asignación aceptan o rechazan según lo que el estado permite, con los mismos mensajes. |
| `State/VehicleStateDesignTests.cs` (11) | Pruebas de arquitectura: exactamente cuatro estados, los componentes consultan al resolutor y nunca a un estado concreto, `Vehicle` conserva su enumerado y no conoce los estados. |
| `State/VehicleStateLifecycleScenarioTests.cs` (4) | Escenarios completos (un vehículo que va al taller a mitad de un viaje se queda allí cuando el viaje termina y solo sale registrando su mantenimiento). |
| `Observer/VehicleTelemetryPublisherTests.cs` (27) | Registro y retirada sin duplicados, notificación a todos en orden, **sin recursión** (un evento publicado por un observador se encola), corte de ciclos, aislamiento de fallos con auditoría y **muchos hilos** publicando y suscribiendo. |
| `Observer/TripTelemetryEventsTests.cs` (18) | Los eventos de un paso (movimiento, paradas en orden, llegada), una parada justo en el extremo del paso (se informa una vez) y que calcularlos no cambia el viaje. |
| `Observer/LiveTelemetryBoardTests.cs` (16) | El tablero: una entrada por vehículo, la última parada solo dentro del mismo viaje, la llegada conserva los datos, orden por placa e instantáneas inmutables, también con escritores y lectores concurrentes. |
| `Observer/SimulatorTelemetryIntegrationTests.cs` (16) | **Integración con el simulador real**: un viaje completo publica movimientos, cada parada una vez y una llegada, en orden y después de guardar; el tablero coincide con la consulta de avance; un observador que falla no detiene la simulación. |
| `Observer/TripAuditObserverTests.cs` (11) | Una línea de auditoría por parada, con el decimal siempre con punto; ni el movimiento ni la llegada se auditan (la llegada ya la audita el servicio de rutas). |
| `Observer/ObserverDesignTests.cs` (10) | Pruebas de arquitectura: `Monitoring` solo depende de lo que debe, solo la simulación lo conoce, el Observer no se mezcla con el Mediator ni con el Bridge y los eventos son inmutables. |
| `Visitor/OperationalInsightsVisitorTests.cs` (47) | Cada hallazgo con su severidad, su mensaje y su umbral (desde el límite exacto), el motivo que da el estado, el orden por urgencia y que visitar dos veces o en otro orden no cambia el resultado. |
| `Visitor/FleetElementsTests.cs` (23) | Los adaptadores copian lo que el análisis necesita y no modifican la entidad; el **doble despacho** (cada elemento llama al método de su tipo, una vez); la estructura ordena, cuenta y recorre. |
| `Visitor/FleetAnalysisServiceTests.cs` (15) | Integración con datos reales: el análisis refleja cambios de estado y viajes completados, no cambia datos ni auditoría y cada llamada crea sus propios visitantes. |
| `Visitor/VehicleUtilizationVisitorTests.cs` (14), `MaintenanceHistoryVisitorTests.cs` (14), `RouteDistanceVisitorTests.cs` (11) | Cada visitante concreto: totales, desgloses (con ceros), quién opera lo decide el estado, intervalos negativos descartados, rutas canceladas fuera de los totales, redondeos y datos vacíos. |
| `Visitor/VisitorDesignTests.cs` (11) | Pruebas de arquitectura: `Analysis` solo depende del dominio, de los estados y de los DTO de resultado; los visitantes nunca nombran una entidad; no es otro Decorator (no toca los reportes). |
| `Support/ConfigurableStates.cs`, `Support/SignatureDependencies.cs`, `Observer/ObserverTestDoubles.cs`, `Visitor/FleetData.cs` | Apoyo: un estado alternativo para probar que los componentes le preguntan al State, la lectura de las firmas de los tipos para las pruebas de arquitectura, los dobles del Observer y datos de flota con los servicios reales. |

Para ejecutar sólo una parte: `dotnet test --filter "FullyQualifiedName~Application.Tests.State"` (192),
`...~Application.Tests.Observer` (98) o `...~Application.Tests.Visitor` (135).

**Resultado de la verificación.** En el arnés descrito en la [sección 11](#11-limitaciones-conocidas-del-prototipo) (no es un `dotnet test`
real), la solución compila con **0 advertencias** y se superan **1438 de 1438 pruebas**: las 1013 anteriores con los mismos nombres y
cantidades que antes de agregar los tres patrones (ninguna cambió de resultado) y las 425 nuevas (State 192, Observer 98, Visitor 135).
Además, con la API original y la nueva levantadas a la vez, con los mismos datos de demostración y la simulación detenida, las 33
consultas `GET` de los endpoints anteriores devolvieron respuestas idénticas (se normalizaron los identificadores y las marcas de
tiempo, que cambian en cada arranque).

**Pruebas de mutación de Observer, State y Visitor.** Igual que en las versiones anteriores, se rompieron a propósito **36 reglas** del código
nuevo o reforzado —una por una, en una copia— y se ejecutó cada vez la suite completa (1438 pruebas); una rotura queda «detectada» si la
compilación falla, si alguna prueba falla o si la ejecución se interrumpe. Las 36 fueron detectadas; ninguna sobrevivió. Por patrón: **State (13)** —que el
cambio manual valide la tabla de transiciones, que Mantenimiento no pase a mano a En ruta, que En ruta no libere al vehículo con otro viaje activo, que empezar un viaje lo deje En ruta, que
`VehicleService`, `MaintenanceService`, `ActiveTrips` y el validador de asignación le pregunten al estado, que Fuera de servicio no
pueda operar, que registrar un mantenimiento solo saque al vehículo del taller, que pedir el mismo estado no se rechace y que terminar
un viaje no libere a un vehículo en el taller—; **Observer (14)** —sin suscripciones duplicadas, que un observador que falla (o cuyo
fallo no se puede auditar) no detenga a los demás, que no haya recursión ni entregas simultáneas, que retirar un observador funcione,
el corte de ciclos, la parada en el extremo del paso, la llegada solo al llegar, que se publique después de guardar, la categoría de
auditoría y el tablero (última parada, llegada y orden por placa)—; y **Visitor (9)** —rutas canceladas fuera de los totales, el estado
decide quién opera, intervalos negativos descartados, el umbral de retraso desde el límite exacto, el orden de la estructura y de los
hallazgos, el hallazgo del vehículo que no sigue su ruta, que el elemento copie el kilometraje y que la estructura ofrezca las rutas—.
En las corridas intermedias tampoco sobrevivió ninguna (una rotura estaba mal escrita en el propio arnés y se corrigió); el detalle de las
36, con la prueba que detecta cada una, está en las tablas de las secciones [8.18](#818-observer--publicar-la-telemetría-del-recorrido-sin-que-el-simulador-sepa-quién-escucha),
[8.19](#819-state--qué-puede-hacer-un-vehículo-según-su-estado) y [8.20](#820-visitor--analizar-la-flota-sin-cargar-las-entidades-con-análisis).
Es una verificación del desarrollo (ejecutada en el arnés descrito en la [sección 11](#11-limitaciones-conocidas-del-prototipo)),
no algo que se ejecute con `dotnet test`.

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
