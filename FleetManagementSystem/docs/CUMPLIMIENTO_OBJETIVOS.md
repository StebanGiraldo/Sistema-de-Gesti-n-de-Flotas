# Cumplimiento de objetivos: qué se pidió, qué patrones lo resuelven y cómo comprobarlo

Este documento responde tres preguntas sobre el sistema de gestión de flotas:

1. **¿Se cumplieron los objetivos?** Para cada objetivo específico se indica qué se construyó y dónde se ve.
2. **¿Qué patrones se usaron para cumplirlos?** Son los 21 patrones GoF originales más Template Method e Interpreter (23 en total). Cada uno aparece con el problema concreto que resuelve y la sección del [README](../README.md) donde está explicado.
3. **¿Cómo se comprueba?** Hay 12 ejemplos reproducibles con los datos de demostración ampliados (12 operadores, 35 vehículos, 24 rutas con carga).

> ### Cómo leer los ejemplos
> **Los ejemplos de este documento no son salidas de una ejecución.** Se escribieron sin poder compilar ni correr la aplicación
> (el entorno donde se preparó no tiene .NET), así que cada «Resultado esperado» está **derivado a mano** de los datos de
> demostración (`src/FleetManagement.Infrastructure/Persistence/Demo/`) y del código. Para confirmarlos:
> arranque la aplicación, siga los pasos del ejemplo y compare. Las pruebas automáticas que protegen los datos y las reglas
> (`tests/FleetManagement.Application.Tests/Demo/`) también están escritas, y la solución **compila sin errores** (compilación
> hecha por el usuario con `dotnet build`: 0 errores), pero las pruebas **aún no se han ejecutado**: la primera vez que se corra
> `dotnet test` hay que revisar su resultado antes de dar por cierto este documento.
>
> Los resultados dependen de **cuándo** se miran: la simulación mueve los vehículos «En ruta» y, al terminar un viaje, el
> vehículo vuelve a «Disponible». Los conteos de este documento son los **del arranque**, antes de que termine ningún viaje.

---

## 1. Resumen: objetivo → patrones → evidencia

| # | Objetivo específico | Patrones que lo cumplen | Dónde se ve (UI / API) |
|---|---------------------|--------------------------|-------------------------|
| 1 | **Monitoreo en tiempo real** de la flota | Observer (8.18), State (8.19), Flyweight (8.9), Facade (8.8), Visitor (8.20), **Interpreter (8.22)** | Pestaña «Mapa y vehículos»; `GET /api/monitoring/telemetry`; `POST /api/rules/vehicles/filter` |
| 2 | **Optimización de rutas y asignación de cargas** | Builder (8.4), Strategy (8.11), Decorator de reglas (8.12), Command (8.13), Memento (8.17), Mediator (8.16), Composite (8.7), Iterator (8.15), Prototype (8.5), Factory Method (8.2) y Abstract Factory (8.3), **Template Method (8.21)** | Pestaña «Rutas y cargas»; `POST /api/routes`, `POST /api/dispatch/plan`, `GET /api/routes/{id}/optimization` |
| 3 | **Mantenimiento predictivo** | State (8.19), Bridge y Decorator de notificaciones (8.7), Chain of Responsibility (8.14), Visitor (8.20), Flyweight (8.9), **Interpreter (8.22)** | Pestaña «Mantenimiento»; `GET /api/maintenance/due`; `POST /api/rules/maintenance/evaluate` |
| 4 | **Integración con navegación** | Adapter (8.7), Proxy (8.10), Flyweight (8.9), Chain of Responsibility (8.14), **Template Method (8.21)** | «Abrir navegación» y «📄 Hoja de ruta» del portal del operador; `GET /api/navigation/route/{id}` |

Además, el **Singleton** (8.1, `FleetAuditLogger`) deja constancia en la bitácora de todo lo anterior (pestaña «Auditoría»).

La numeración de los objetivos es la de la tabla de módulos de la [sección 1 del README](../README.md#1-descripción-general-y-módulos-funcionales).
El caso de uso que motivó cada patrón nuevo está en el README: el Interpreter cubre los objetivos 1 y 3, y el Template Method, el 2 y el 4.

---

## 2. Objetivo general

> Un sistema de gestión de flotas que permita **supervisar** los vehículos, **planificar y asignar** rutas con su carga,
> **anticipar** el mantenimiento y **guiar** al conductor con navegación.

| Parte del objetivo | Cómo se cubre | Patrones |
|--------------------|----------------|----------|
| Supervisar | Mapa con marcadores por tipo y estado, telemetría del simulador, avance de cada viaje y filtros escritos como texto | Observer, State, Flyweight, Facade, Interpreter |
| Planificar y asignar | Rutas con paradas y carga, reglas de asignación, reparto de lotes y reordenamiento de paradas con deshacer | Builder, Strategy, Decorator, Command, Memento, Mediator |
| Anticipar el mantenimiento | Vencidos por fecha o kilometraje y reglas configurables sin recompilar | State, Interpreter, Bridge, Visitor |
| Guiar al conductor | Enlace de navegación con caché, hoja de ruta con próxima parada y hora estimada, reporte de complicaciones | Adapter, Proxy, Template Method, Chain of Responsibility |
| Todo con trazabilidad y bajo acoplamiento | Bitácora única, capas Domain → Application → Infrastructure → Api, pruebas de diseño que vigilan las dependencias | Singleton, Clean Architecture |

---

## 3. Objetivos específicos, patrón por patrón

### Objetivo 1 — Monitoreo en tiempo real

**Qué se construyó.** El simulador (`VehicleSimulationBackgroundService`) mueve cada segundo los vehículos «En ruta» a lo largo de su ruta.
El panel muestra ubicación y estado de cada vehículo, y `GET /api/progress` informa el avance de cada viaje activo.

| Patrón | Problema concreto que resuelve aquí | Sección |
|--------|--------------------------------------|---------|
| **Observer** | El simulador publica la telemetría sin saber quién la escucha; `GET /api/monitoring/telemetry` entrega lo último de cada vehículo y las paradas alcanzadas quedan en la bitácora | 8.18 |
| **State** | El estado del vehículo (Disponible, En ruta, Mantenimiento, Fuera de servicio) decide qué puede hacer y a qué estado puede pasar | 8.19 |
| **Flyweight** | Un único perfil por tipo de vehículo (velocidad, restricciones, tareas) y un ícono compartido por tipo y estado en el mapa, aunque haya 35 vehículos | 8.9 |
| **Facade** | `GET /api/dashboard/summary` resume la flota en una sola llamada | 8.8 |
| **Visitor** | `GET /api/analysis/fleet`: utilización, recorridos y hallazgos sin cargar las entidades con análisis | 8.20 |
| **Interpreter** | Filtros de monitoreo escritos como texto (`status = "Available" AND type = "Truck"`), sin un endpoint por cada combinación | 8.22 |

**Datos de demostración para verlo.** 11 vehículos en viaje a la vez (VAN-002 original y 10 nuevos: TRK-006, TRK-007, TRK-008,
VAN-015 a VAN-019, MOT-030 y CAR-025), dos de ellos con retraso (VAN-017 y VAN-019), repartidos entre Santander, Bogotá, el Caribe, Antioquia, el Eje Cafetero y Valle.

### Objetivo 2 — Optimización de rutas y asignación de cargas

**Qué se construyó.** Crear rutas con paradas y artículos de carga; asignar vehículo y conductor **respetando las reglas** (capacidad en kg y m³,
disponibilidad, mantenimiento vencido, sin doble asignación); repartir un lote de entregas entre los vehículos disponibles; reordenar las paradas para recorrer menos kilómetros, con deshacer y rehacer.

| Patrón | Problema concreto que resuelve aquí | Sección |
|--------|--------------------------------------|---------|
| **Builder** | Armar una ruta con paradas, carga, vehículo y conductor paso a paso (`POST /api/routes`, «+ Nueva ruta») | 8.4 |
| **Strategy** | Intercambiar el optimizador de paradas (vecino más cercano + 2-opt) y la asignación de carga entre vehículos | 8.11 |
| **Decorator de reglas** | Las reglas de asignación envuelven el servicio de rutas sin modificarlo: capacidad, disponibilidad, mantenimiento vencido, doble asignación | 8.12 |
| **Command** | «Crear ruta», «Duplicar», «Aplicar este orden» son órdenes con historial, deshacer y rehacer | 8.13 |
| **Memento** | Guarda el orden anterior de las paradas para poder volver a él | 8.17 |
| **Mediator** | Pasar la ruta a «En progreso» deja al vehículo «En ruta»; terminarla lo deja «Disponible» | 8.16 |
| **Composite / Iterator** | La carga de una ruta es un árbol de grupos y artículos que se recorre como lista plana (`/cargo-manifest`, `/cargo-items`) | 8.7 y 8.15 |
| **Prototype** | Duplicar una ruta recurrente o un vehículo sin repetir su configuración | 8.5 |
| **Factory Method / Abstract Factory** | Dar de alta vehículos de cualquier tipo con su familia de piezas coherente («+ Vehículo») | 8.2 y 8.3 |
| **Template Method** | La hoja de despacho muestra la carga frente a la capacidad del vehículo y el retraso de la ruta | 8.21 |

**Datos de demostración para verlo.** 24 rutas con carga: 11 activas, 7 planificadas y 6 completadas. **23 vehículos distintos tienen una ruta con carga**
(VAN-002 más 22 nuevos), y hay vehículos libres de todos los tipos para asignarles rutas nuevas (sección 4).

### Objetivo 3 — Mantenimiento predictivo

**Qué se construyó.** Registrar mantenimientos y detectar qué vehículos tienen tareas vencidas por fecha o por kilometraje;
y una forma de definir umbrales nuevos **como texto**, sin compilar.

| Patrón | Problema concreto que resuelve aquí | Sección |
|--------|--------------------------------------|---------|
| **State** | Un vehículo en Mantenimiento **solo pasa a Disponible al registrar su mantenimiento** (`POST /api/maintenance`); `PATCH …/status` con «Available» responde 400 | 8.19 |
| **Interpreter** | `kmSinceLastService >= 8000 OR daysSinceLastService > 180`: reglas de alerta que el administrador escribe y la API evalúa sobre la flota | 8.22 |
| **Bridge + Decorator de notificaciones** | El aviso de un mantenimiento se separa de su canal (correo, SMS, push) y se decora con auditoría, reintento y prioridad | 8.7 |
| **Chain of Responsibility** | Las alertas que reporta el conductor pasan por una cadena; las graves y críticas se escalan | 8.14 |
| **Visitor** | El análisis de la flota incluye el historial de mantenimiento y los hallazgos operativos, ordenados del más al menos urgente | 8.20 |
| **Flyweight** | Las tareas de mantenimiento propias de cada tipo de vehículo viven en el perfil compartido | 8.9 |

**Datos de demostración para verlo.** Tres vehículos con mantenimiento vencido a propósito: CAR-003 (original, en taller),
**CAR-028** (en taller) y **CAR-029** (Disponible, pero vencido: es el caso que prueba que el estado «Disponible» no basta). Dos próximos a vencer
(VAN-017 y VAN-019) y varios vehículos sin historial, para ver cómo trata la regla el dato que falta.

### Objetivo 4 — Integración con navegación

**Qué se construyó.** El conductor abre su ruta en una app de navegación real, ve la próxima parada y la hora estimada, y reporta complicaciones.

| Patrón | Problema concreto que resuelve aquí | Sección |
|--------|--------------------------------------|---------|
| **Adapter** | Convierte la ruta del sistema en un enlace de Google Maps; cambiar de proveedor es escribir otro adaptador | 8.7 |
| **Proxy** | Caché del proveedor: la segunda consulta de la misma ruta sale del caché y no vuelve a llamar al proveedor | 8.10 |
| **Template Method** | La **hoja del conductor** (paradas en orden, enlace, próxima parada, km que faltan y ETA) y la del despachador salen del mismo esqueleto | 8.21 |
| **Chain of Responsibility** | «🚨 Reportar complicación» del portal: avería, retraso, tráfico, accidente o clima | 8.14 |
| **Flyweight** | Las restricciones de navegación por tipo de vehículo | 8.9 |

**Datos de demostración para verlo.** Cada operador (`operador3` … `operador12`) entra al portal y ve **su** viaje activo y, en varios casos, su siguiente viaje planificado.

---

## 4. Datos de demostración ampliados

Todo vive en `src/FleetManagement.Infrastructure/Persistence/Demo/` (tablas de datos más sembradores pequeños) y se carga al final de `InMemoryDataSeeder`,
que **no se modificó** salvo por esa llamada. Los datos originales (admin, operador1, operador2, TRK-001, VAN-002, CAR-003, MOT-004, VAN-005 y sus rutas) siguen igual.

### 4.1 Cuentas

| Rol | Usuario | Contraseña | Notas |
|-----|---------|------------|-------|
| Administrador | `admin` | `Admin123!` | Panel `dashboard.html` |
| Operador | `operador1`, `operador2` | `Operador123!` | Originales |
| Operador | `operador3` … `operador12` | `Operador123!` | **Nuevos.** Cada uno tiene su conductor vinculado y su ruta (tabla 4.3). El login tiene un selector rápido |

### 4.2 Flota: 35 vehículos

| Tipo | Cantidad | Placas |
|------|----------|--------|
| Camión | 10 | TRK-001, TRK-006 … TRK-014 |
| Furgoneta | 12 | VAN-002, VAN-005, VAN-015 … VAN-024 |
| Automóvil | 6 | CAR-003, CAR-025 … CAR-029 |
| Moto | 7 | MOT-004, MOT-030 … MOT-035 |

| Estado | Cantidad | Placas |
|--------|----------|--------|
| En ruta | 11 | VAN-002, TRK-006 a TRK-008, VAN-015 a VAN-019, MOT-030, CAR-025 |
| Disponible | 20 | El resto, incluidos CAR-029 (vencido) y los 12 con ruta planificada o completada |
| Mantenimiento | 2 | CAR-003, CAR-028 |
| Fuera de servicio | 2 | VAN-005, MOT-035 |

**Vehículos libres para asignar rutas nuevas** (sin ruta): TRK-001, TRK-013, TRK-014, VAN-024, MOT-004, MOT-033 y MOT-034, más CAR-029,
que está disponible pero vencido (la regla lo rechaza), CAR-028 (en taller) y MOT-035 (fuera de servicio).

### 4.3 Rutas con carga: 24 (22 nuevas)

| Estado | Vehículo → operador | Ruta | Carga (kg · m³) |
|--------|---------------------|------|-----------------|
| En progreso | TRK-006 → operador3 | Bucaramanga - Barrancabermeja | 7 700 · 9 |
| En progreso | TRK-007 → operador4 | Bogotá - Villavicencio | 5 000 · 37 |
| En progreso | TRK-008 → operador5 | Barranquilla - Santa Marta | 8 000 · 28 |
| En progreso | VAN-015 → operador6 | Cali - Palmira | 800 · 6,7 |
| En progreso | VAN-016 → operador7 | Medellín - Rionegro | 750 · 7 |
| **Con retraso (25 min)** | VAN-017 → operador8 | Pereira - Manizales | 1 020 · 7 |
| En progreso | VAN-018 → operador9 | Cúcuta - Pamplona | 1 050 · 9 |
| **Con retraso (40 min)** | VAN-019 → operador10 | Ibagué - Bogotá | 1 350 · 11 |
| En progreso | MOT-030 → operador11 | Mensajería Bucaramanga - Floridablanca | 20 · 0,25 |
| En progreso | CAR-025 → operador12 | Bucaramanga - Piedecuesta | 240 · 2,5 |
| Planificada | TRK-009 → operador3 | Bogotá - Tunja | 5 600 · 22 |
| Planificada | TRK-010 → operador4 | Medellín - Bogotá | 7 300 · 39 |
| Planificada | VAN-020 → operador5 | Cali - Pereira | 1 100 · 9 |
| Planificada | VAN-021 → operador6 | Cartagena - Barranquilla | 1 200 · 9 |
| Planificada | MOT-031 → operador7 | Mensajería Medellín - El Poblado | 10 · 0,13 |
| Planificada | CAR-026 → operador8 | Villavicencio - Bogotá | 320 · 3,2 |
| Completada | TRK-011 → operador9 | Bucaramanga - San Gil | 8 000 · 13 |
| Completada | TRK-012 → operador10 | Cúcuta - Bucaramanga | 7 800 · 18 |
| Completada | VAN-022 → operador11 | Pereira - Cali | 1 200 · 10,5 |
| Completada | VAN-023 → operador12 | Barranquilla - Cartagena | 1 200 · 9 |
| Completada | MOT-032 → operador1 | Mensajería Girón - Bucaramanga | 13 · 0,22 |
| Completada | CAR-027 → operador2 | Bogotá - Ibagué | 300 · 3,5 |

Más las dos rutas originales («Girón - Bucaramanga Centro», activa, y «Bucaramanga - Bogotá», planificada y aún **sin vehículo**).
**Resumen:** 11 activas, 7 planificadas, 6 completadas; **23 vehículos distintos con ruta y carga** (el mínimo pedido era 20).

Toda la carga cabe en su vehículo (las pruebas lo comprueban fila por fila) y varios casos están justo en el límite: TRK-011 lleva 8 000 kg de 8 000.

### 4.4 Historial de mantenimiento

| Vehículo | Tipo | Hace | Estado de la tarea |
|----------|------|------|---------------------|
| TRK-006, TRK-007, TRK-008, VAN-015, TRK-011, TRK-014, MOT-030, CAR-025 | Aceite, frenos, llantas, general… | 1 a 3 meses | Al día |
| VAN-017 | Revisión general | 5 meses | Próxima en 25 días y a 2 000 km |
| VAN-019 | Frenos | 4 meses | Próxima en 40 días y a 1 000 km |
| VAN-022 | Aceite | 5 meses | Al día, pero ya recorrió 8 600 km desde el servicio |
| **CAR-028** | Aceite | 7 meses | **Vencido** por fecha y por kilometraje (en taller) |
| **CAR-029** | Frenos | 8 meses | **Vencido** por fecha y por kilometraje (Disponible) |

Los 20 vehículos que no figuran aquí **no tienen historial**: las reglas del Interpreter los tratan como «sin dato».

---

## 5. Ejemplos

Se usa Git Bash. Primero un token de administrador:

```bash
TOKEN=$(curl -s -X POST http://localhost:5080/api/auth/login -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}' | python -c "import sys,json; print(json.load(sys.stdin)['token'])")
```

### Ejemplo 1 — Cada operador ve su propio viaje (objetivos 1 y 4)

1. Iniciar sesión como `operador5` / `Operador123!` (o elegirlo en el selector del login).
2. En `operator.html` aparece la ruta **Barranquilla - Santa Marta** (TRK-008) con su carga: alimentos perecederos (5 200 kg, frágil) y bebidas (2 800 kg).
3. Cerrar sesión y entrar como `operador8`: aparece **Pereira - Manizales** (VAN-017), con retraso.

**Resultado esperado:** cada cuenta ve la ruta de su propio conductor y no la de otro. Patrones: State (el vehículo está En ruta) y la relación entre ruta y vehículo que coordina el Mediator (8.16).

### Ejemplo 2 — Monitoreo: once viajes a la vez (objetivo 1)

```bash
curl -s http://localhost:5080/api/monitoring/telemetry -H "Authorization: Bearer $TOKEN"
```

**Resultado esperado:** una lectura por cada vehículo que ya publicó posición; con la simulación en marcha, **11 vehículos** (VAN-002 y los 10 nuevos «En ruta»).
En la UI, la pestaña «Mapa y vehículos» muestra los marcadores con un ícono por tipo y estado. Patrones: Observer, Flyweight.

### Ejemplo 3 — Filtros de monitoreo escritos como texto (objetivo 1)

```bash
curl -s -X POST http://localhost:5080/api/rules/vehicles/filter \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"rule":"status = \"Available\" AND type = \"Truck\" AND hasDriver = 0"}'
```

| Regla | Resultado esperado (al arrancar) |
|-------|----------------------------------|
| `status = "EnRoute"` | 11 vehículos |
| `status = "Available" AND type = "Truck" AND hasDriver = 0` | 7 camiones: TRK-001, TRK-009 a TRK-014 |
| `capacityKg >= 9000 AND status = "Available"` | 5 camiones: TRK-009, TRK-010, TRK-012, TRK-013, TRK-014 |
| `type = "Motorcycle" AND status != "OutOfService"` | 6 motos: MOT-004, MOT-030 a MOT-034 |

`evaluatedVehicles` debe ser 35 en todos los casos. La misma gramática y las mismas expresiones sirven para los filtros y para el mantenimiento (ejemplo 5): solo cambia el contexto.
Una regla mal escrita, como `status = `, responde **400** con la posición del error.

### Ejemplo 4 — Mantenimiento vencido por el módulo predictivo (objetivo 3)

```bash
curl -s http://localhost:5080/api/maintenance/due -H "Authorization: Bearer $TOKEN"
```

**Resultado esperado:** tres vehículos con tarea vencida: **CAR-003**, **CAR-028** y **CAR-029**. VAN-017 y VAN-019 no aparecen: faltan días y kilómetros.
En la UI, pestaña «Mantenimiento».

### Ejemplo 5 — Reglas de mantenimiento como texto (objetivo 3)

```bash
curl -s -X POST http://localhost:5080/api/rules/maintenance/evaluate \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"rule":"kmSinceLastService >= 8000 OR daysSinceLastService > 180"}'
```

| Regla | Resultado esperado |
|-------|--------------------|
| `kmSinceLastService >= 8000 OR daysSinceLastService > 180` | 5 vehículos: VAN-017 (12 000 km), VAN-019 (9 000 km), VAN-022 (8 600 km), CAR-028 y CAR-029 |
| `daysSinceLastService > 100` | 6 vehículos: los cinco anteriores más CAR-003 (hace unos 4 meses) |
| `NOT kmSinceLastService > 5` | 21: TRK-001 (0 km desde su servicio) y los 20 **sin historial** |

La tercera fila muestra la regla del dato que falta: un vehículo sin mantenimientos **no inventa un 0**, la comparación `kmSinceLastService > 5`
da falso y su negación da verdadero (quedó documentado en 8.22). VAN-022 es útil: el módulo predictivo **no** lo marca vencido (todavía le quedan 1 400 km y 25 días),
pero la regla de 8 000 km sí lo señala, así que el Interpreter **complementa** al módulo sin reemplazarlo.
El cálculo de «meses atrás» usa la fecha actual, por eso se evitan umbrales cercanos a un múltiplo de mes (por ejemplo, 90 días).

### Ejemplo 6 — Un vehículo en taller no vuelve a Disponible sin registrar su mantenimiento (objetivo 3)

1. Obtener el id de **CAR-028** (`GET /api/fleet/vehicles`).
2. Intentar pasarlo a Disponible:

```bash
curl -s -X PATCH http://localhost:5080/api/fleet/vehicles/$CAR028/status \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"status":"Available"}'
```

   **Esperado:** **400** con el motivo (primero hay que registrar el mantenimiento).
3. Registrar el mantenimiento:

```bash
curl -s -X POST http://localhost:5080/api/maintenance \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{
    "vehicleId":"'$CAR028'","type":"OilChange","performedAt":"2026-11-21T10:00:00Z",
    "nextDueDate":"2027-05-21T10:00:00Z","nextDueMileageKm":100000,
    "notes":"Cambio de aceite y filtros","mileageAtServiceKm":90500 }'
```

   **Esperado:** el vehículo sale de «Mantenimiento» y queda «Disponible»; `GET /api/maintenance/due` ya no lista CAR-028 (quedan CAR-003 y CAR-029).
   Patrones: State (única vía de salida), Bridge/Decorator (aviso y auditoría en `GET /api/audit/logs`).

### Ejemplo 7 — Mantenimiento vencido bloquea la asignación aunque el vehículo esté «Disponible» (objetivo 2)

```bash
curl -s -X POST http://localhost:5080/api/routes \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{
    "name":"Prueba CAR-029","originLat":7.8939,"originLng":-72.5078,"destinationLat":7.3750,"destinationLng":-72.6475,
    "waypoints":[],"cargoItems":[{"description":"Cajas","weightKg":50,"volumeM3":0.5,"priority":"Standard"}],
    "assignedVehicleId":"'$CAR029'","assignedDriverId":null,"scheduledDate":null }'
```

**Resultado esperado:** **400** con un mensaje que menciona el mantenimiento vencido (BrakeInspection). La ruta no se crea.
Es el patrón Decorator de reglas (8.12) actuando sobre el servicio de rutas. Después de registrar su mantenimiento (como en el ejemplo 6), la misma petición funciona.

### Ejemplo 8 — Carga que no cabe (objetivo 2)

Misma petición con **MOT-033** (35 kg, 0,35 m³) y un artículo de 500 kg y 0,1 m³:

**Resultado esperado:** **400** indicando que la carga (500 kg) supera la capacidad de MOT-033 (35 kg). Con 20 kg la ruta se crea.
En la UI: «+ Nueva ruta» muestra el aviso rojo de capacidad. Con **TRK-013** (11 000 kg, 48 m³) y 4 000 kg, la ruta se crea sin problema.

### Ejemplo 9 — Reparto de un lote entre los vehículos libres (objetivo 2)

Usar el ejemplo de `POST /api/dispatch/plan` de la sección 9 del README (cuatro entregas en el área metropolitana de Bucaramanga).

**Resultado esperado:** un plan con viajes asignados a vehículos **disponibles y aptos** (19 al arrancar), paradas ya ordenadas, totales y comparación con el reparto simple.
Deberían quedar en `excludedVehicles`, cada uno con su motivo: los 11 en viaje, CAR-003 y CAR-028 (taller), CAR-029 (mantenimiento vencido), VAN-005 y MOT-035 (fuera de servicio); el README (8.11) documenta ese comportamiento para VAN-002, CAR-003 y VAN-005.
`POST /api/dispatch/plan` **no guarda nada**; `POST /api/dispatch/apply` crearía una ruta planificada por viaje, todo o nada.
Patrón: Strategy (el optimizador y la estrategia de asignación son intercambiables).

### Ejemplo 10 — Reordenar las paradas, deshacer y rehacer (objetivo 2)

1. En «Rutas y cargas», crear una ruta con varias paradas **desordenadas** (por ejemplo, desde Bucaramanga: Piedecuesta, Lebrija, Floridablanca, Girón).
2. Pulsar «🧭 Optimizar»: la propuesta muestra el nuevo orden y los kilómetros ahorrados.
3. «Aplicar este orden», luego «↶ Deshacer» (vuelve al orden original) y «↷ Rehacer».

**Resultado esperado:** el 2-opt **nunca devuelve un recorrido más largo** que el original (8.11); si el orden ya era bueno, el ahorro es 0. Patrones: Strategy, Command, Memento.
Las rutas de demostración tienen una sola parada intermedia a propósito (no hay nada que reordenar), así que este ejemplo usa una ruta nueva.

### Ejemplo 11 — Una ruta, dos hojas (objetivos 2 y 4: Template Method)

```bash
curl -s "http://localhost:5080/api/route-sheets/$ROUTE_A1?audience=Driver"
curl -s "http://localhost:5080/api/route-sheets/$ROUTE_A1?audience=Dispatcher"
```

con `ROUTE_A1` el id de «Bucaramanga - Barrancabermeja» (TRK-006, operador3).

| Paso del esqueleto | `Driver` | `Dispatcher` |
|--------------------|----------|--------------|
| Encabezado | «Hoja de ruta del conductor», ruta, placa TRK-006 | «Hoja de despacho», además el nombre **Andrés Felipe Torres** |
| Paradas | Origen → Lebrija → destino, en orden | vacío |
| Carga | — | 2 artículos, 7 700 kg de 10 000 kg (77 %), 9 m³ de 45 m³, `exceedsCapacity: false` |
| Avance | km que faltan, próxima parada y ETA (si el viaje ya va en curso) | porcentaje, km que faltan y **0 min** de retraso |
| Navegación | enlace de navegación | — |

Con **«Pereira - Manizales»** (VAN-017) en `Dispatcher`, el retraso esperado es **25 min**; con «Ibagué - Bogotá» (VAN-019), **40 min**.
Casos de error: `audience=Pilot` → 400 con los valores válidos; la ruta original «Bucaramanga - Bogotá» (sin vehículo) → 400.
En la UI: «📄 Hoja de ruta» en cada fila de «Rutas y cargas» (selector Conductor / Despachador) y en la barra inferior del portal del operador.

### Ejemplo 12 — Navegación con caché y complicaciones del conductor (objetivo 4)

```bash
curl -s http://localhost:5080/api/navigation/route/$ROUTE_A1
curl -s http://localhost:5080/api/navigation/route/$ROUTE_A1   # segunda vez
```

**Resultado esperado:** ambos devuelven el mismo enlace de Google Maps con el origen, Lebrija y el destino; la segunda respuesta sale del **caché** del Proxy
(la prueba `Proxy` cuenta las llamadas al proveedor). Desde el portal de `operador8` (VAN-017), «🚨 Reportar complicación» → «Tráfico» o «Avería»
pasa por la cadena de responsabilidad: las graves y críticas dejan un aviso de escalamiento visible en `GET /api/audit/logs` (categoría de alertas).

---

## 6. Pruebas que respaldan el documento

| Qué se respalda | Dónde |
|-----------------|-------|
| 12 operadores con contraseña válida, 35 vehículos, 24 rutas, ≥ 20 vehículos con ruta y carga, 11 viajes activos sin vehículo ni conductor repetido, carga ≤ capacidad, solo CAR-003, CAR-028 y CAR-029 vencidos, reglas de asignación aceptan y rechazan lo esperado | `tests/FleetManagement.Application.Tests/Demo/` |
| Gramática, precedencia, errores con posición, límites, variables sin dato, contextos | `tests/…/Interpreter/` |
| Esqueleto no virtual, orden fijo de pasos, variantes y contenido de cada audiencia | `tests/…/RouteSheets/` |
| El Interpreter y el Template Method no dependen de otros patrones ni de capas externas | `tests/…/Architecture/RouteSheetAndInterpreterDesignTests.cs` |
| Cada patrón anterior | Una carpeta por patrón en `tests/FleetManagement.Application.Tests/` (State, Command, Observer, Proxy…) |

Para correrlas: `dotnet test` en la raíz de la solución. **Al prepararlas no había .NET disponible; la solución se compiló después sin errores, pero las pruebas no se han ejecutado todavía**;
si alguna falla, el dato o la prueba está mal escrito y debe corregirse antes de usar este documento como evidencia.

---

## 7. Límites

- **No es una ejecución.** Los resultados esperados vienen de leer los datos y el código; confírmelos corriendo la aplicación (sección «Cómo leer los ejemplos»).
- **El tiempo mueve los datos.** Con la simulación activa, los viajes terminan y cambian los estados. Reinicie la aplicación para volver al estado inicial (los repositorios están en memoria).
- **Los datos son de demostración.** Las distancias de las rutas (`EstimatedDistanceKm`) son aproximadas, escritas a mano (km por carretera
  aproximados), y el servidor calcula el avance y la ETA en línea recta entre puntos, por lo que ambas cifras no coinciden. El mapa del
  navegador sí dibuja la carretera real (OSRM) y coloca el vehículo sobre ella; ver la sección 11 del README.
- **La optimización de rutas es en línea recta** (vecino más cercano + 2-opt sobre distancia Haversine), no por carretera.
- **Los patrones se aplicaron donde resuelven un problema del sistema.** Esta tabla no pretende que cada patrón sea imprescindible; el README (8.x) explica en cada caso por qué se eligió y cuáles son sus límites.
