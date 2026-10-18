/**
 * Cliente REST centralizado para el Sistema de Gestión de Flotas.
 * Ajuste API_BASE_URL si el backend corre en otro host/puerto.
 * Por defecto, el perfil de lanzamiento del backend usa http://localhost:5080.
 */
const API_BASE_URL = 'http://localhost:5080/api';

function getToken() {
  return sessionStorage.getItem('fleet_token');
}

async function apiFetch(path, options = {}) {
  const headers = { 'Content-Type': 'application/json', ...(options.headers || {}) };
  const token = getToken();
  if (token) headers['Authorization'] = `Bearer ${token}`;

  let response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, { ...options, headers });
  } catch (networkError) {
    throw new Error(`No se pudo contactar al backend en ${API_BASE_URL}. ¿Está corriendo "dotnet run"?`);
  }

  if (response.status === 401) {
    sessionStorage.clear();
    if (!location.pathname.endsWith('login.html')) {
      window.location.href = 'login.html';
    }
    throw new Error('Sesión expirada. Inicie sesión nuevamente.');
  }

  if (!response.ok) {
    let message = `Error ${response.status}`;
    try {
      const errorBody = await response.json();
      if (errorBody && errorBody.message) message = errorBody.message;
    } catch { /* respuesta sin cuerpo JSON */ }
    throw new Error(message);
  }

  if (response.status === 204) return null;

  const text = await response.text();
  return text ? JSON.parse(text) : null;
}

const FleetAPI = {
  // --- Autenticación ---
  login: (username, password) => apiFetch('/auth/login', { method: 'POST', body: JSON.stringify({ username, password }) }),
  logout: () => apiFetch('/auth/logout', { method: 'POST' }),

  // --- Vehículos / Flota (monitoreo en tiempo real) ---
  getVehicles: () => apiFetch('/fleet/vehicles'),
  getVehicle: (id) => apiFetch(`/fleet/vehicles/${id}`),
  createVehicle: (data) => apiFetch('/fleet/vehicles', { method: 'POST', body: JSON.stringify(data) }),
  cloneVehicle: (id, newLicensePlate) => apiFetch(`/fleet/vehicles/${id}/clone`, { method: 'POST', body: JSON.stringify({ newLicensePlate }) }),
  updateVehicleStatus: (id, status) => apiFetch(`/fleet/vehicles/${id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }),

  // --- Conductores ---
  getDrivers: () => apiFetch('/drivers'),

  // --- Rutas (optimización y asignación de cargas) ---
  getRoutes: () => apiFetch('/routes'),
  getRoute: (id) => apiFetch(`/routes/${id}`),
  getRoutesByDriver: (driverId) => apiFetch(`/routes/driver/${driverId}`),
  createRoute: (data) => apiFetch('/routes', { method: 'POST', body: JSON.stringify(data) }),
  createExpressRoute: (data) => apiFetch('/routes/express', { method: 'POST', body: JSON.stringify(data) }),
  duplicateRoute: (id, newScheduledDate) => apiFetch(`/routes/${id}/duplicate`, { method: 'POST', body: JSON.stringify({ newScheduledDate }) }),
  updateRouteStatus: (id, status) => apiFetch(`/routes/${id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }),

  // --- Mantenimiento predictivo ---
  getMaintenance: () => apiFetch('/maintenance'),
  getMaintenanceByVehicle: (vehicleId) => apiFetch(`/maintenance/vehicle/${vehicleId}`),
  getMaintenanceDue: () => apiFetch('/maintenance/due'),
  createMaintenance: (data) => apiFetch('/maintenance', { method: 'POST', body: JSON.stringify(data) }),

  // --- Alertas de viaje ---
  getAlerts: () => apiFetch('/alerts'),
  getAlertsByRoute: (routeId) => apiFetch(`/alerts/route/${routeId}`),
  createAlert: (data) => apiFetch('/alerts', { method: 'POST', body: JSON.stringify(data) }),
  resolveAlert: (id) => apiFetch(`/alerts/${id}/resolve`, { method: 'PATCH' }),

  // --- Avance de los viajes en curso: UNA sola consulta devuelve todos los viajes activos (sin N+1) ---
  getTripProgress: () => apiFetch('/progress'),

  // --- Navegación externa ---
  getNavigationLink: (routeId) => apiFetch(`/navigation/route/${routeId}`),

  // --- Auditoría (Singleton) ---
  getAuditLogs: (count = 100) => apiFetch(`/audit/logs?count=${count}`),
};

// ---------------------------------------------------------------------
// Avance de los viajes (presentación compartida por el panel administrativo y el portal del operador)
// ---------------------------------------------------------------------
// Ambas páginas cargan este archivo, así que el bloque de progreso, sus estilos y el movimiento suave de los marcadores
// se escriben UNA vez aquí en lugar de duplicarse en dashboard.js y operator.js. Los estilos se inyectan al usarse por
// primera vez: de ese modo no hace falta modificar las hojas de estilo ni el HTML existentes.

const TripProgressUI = (() => {
  const STYLE_ELEMENT_ID = 'trip-progress-styles';
  const MINUTES_PER_HOUR = 60;
  const METERS_PER_KM = 1000;
  const FRACTION_DIGITS = 1;

  const STYLES = `
    .trip-progress { margin-top: 8px; font-size: 12px; }
    .trip-progress-head { display: flex; justify-content: space-between; align-items: baseline; margin-bottom: 4px; }
    .trip-progress-bar { height: 6px; border-radius: 3px; background: var(--color-border, #2a3241); overflow: hidden; }
    .trip-progress-fill { height: 100%; border-radius: 3px; background: var(--status-enroute, #4c8dff); transition: width 1s linear; }
    .trip-progress[data-status="Delayed"] .trip-progress-fill { background: var(--status-maintenance, #f5a524); }
    .trip-progress-facts { display: flex; flex-wrap: wrap; gap: 4px 12px; margin-top: 5px; color: var(--color-text-muted, #8b93a4); }
    .trip-progress-compact { margin-top: 6px; font-size: 11px; }
    .trip-progress-panel { margin-top: 14px; padding: 10px 12px; border-radius: var(--radius-sm, 6px); background: var(--color-bg, #10141b); border: 1px solid var(--color-border-soft, #212836); }
  `;

  const escape = (text) => String(text).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  function ensureStyles() {
    if (document.getElementById(STYLE_ELEMENT_ID)) return;
    const style = document.createElement('style');
    style.id = STYLE_ELEMENT_ID;
    style.textContent = STYLES;
    document.head.appendChild(style);
  }

  function formatPercent(percent) {
    return `${percent.toLocaleString('es-CO', { maximumFractionDigits: FRACTION_DIGITS })} %`;
  }

  function formatDistance(km) {
    if (km < 1) return `${Math.round(km * METERS_PER_KM)} m`;
    return `${km.toLocaleString('es-CO', { minimumFractionDigits: FRACTION_DIGITS, maximumFractionDigits: FRACTION_DIGITS })} km`;
  }

  // La ETA llega en minutos de reloj real (ya incluye retraso y aceleración de la simulación).
  function formatEta(minutes) {
    if (minutes < 1) return 'menos de 1 min';
    const total = Math.round(minutes);
    const hours = Math.floor(total / MINUTES_PER_HOUR);
    return hours > 0 ? `${hours} h ${String(total % MINUTES_PER_HOUR).padStart(2, '0')} min` : `${total} min`;
  }

  function barHtml(trip) {
    return `
      <div class="trip-progress-bar" role="progressbar" aria-label="Avance del viaje" aria-valuemin="0" aria-valuemax="100" aria-valuenow="${trip.percent}">
        <div class="trip-progress-fill" style="width:${trip.percent}%"></div>
      </div>`;
  }

  /** Bloque completo (popup del mapa y portal del operador): porcentaje, barra, km restantes, próxima parada y ETA. */
  function html(trip) {
    ensureStyles();
    return `
      <div class="trip-progress" data-status="${escape(trip.routeStatus)}">
        <div class="trip-progress-head"><span>Avance del viaje</span><strong>${formatPercent(trip.percent)}</strong></div>
        ${barHtml(trip)}
        <div class="trip-progress-facts">
          <span>📏 ${formatDistance(trip.remainingKm)} restantes</span>
          <span>📍 Próxima: ${escape(trip.nextStopLabel)}</span>
          <span>⏱️ Llegada en ${formatEta(trip.etaMinutes)}</span>
        </div>
      </div>`;
  }

  /** Versión compacta para la lista de vehículos: la misma información en una barra y una línea de datos. */
  function compactHtml(trip) {
    ensureStyles();
    return `
      <div class="trip-progress trip-progress-compact" data-status="${escape(trip.routeStatus)}">
        ${barHtml(trip)}
        <div class="trip-progress-facts">
          <span>${formatPercent(trip.percent)}</span>
          <span>${formatDistance(trip.remainingKm)} restantes</span>
          <span>→ ${escape(trip.nextStopLabel)}</span>
          <span>${formatEta(trip.etaMinutes)}</span>
        </div>
      </div>`;
  }

  return { html, compactHtml, ensureStyles };
})();

// Movimiento suave de marcadores: el backend entrega una posición nueva en cada consulta (cada pocos segundos) y, sin
// esto, el marcador saltaría de un punto a otro. Se desliza hasta la nueva posición durante todo el intervalo de consulta,
// de modo que el movimiento sea continuo.
const MarkerMotion = (() => {
  const runningFrames = new WeakMap();

  function glide(marker, target, durationMs) {
    cancel(marker);
    const from = marker.getLatLng();
    const to = L.latLng(target);
    if (from.equals(to)) return;

    const startedAt = performance.now();
    const step = (now) => {
      const progress = Math.min((now - startedAt) / durationMs, 1);
      marker.setLatLng([from.lat + (to.lat - from.lat) * progress, from.lng + (to.lng - from.lng) * progress]);
      if (progress < 1) runningFrames.set(marker, requestAnimationFrame(step));
      else runningFrames.delete(marker);
    };
    runningFrames.set(marker, requestAnimationFrame(step));
  }

  // Si llega una posición nueva a mitad de un deslizamiento, el anterior se cancela y el nuevo parte de donde está el marcador.
  function cancel(marker) {
    const frame = runningFrames.get(marker);
    if (frame !== undefined) cancelAnimationFrame(frame);
    runningFrames.delete(marker);
  }

  return { glide };
})();

// Capa de mapa con el recorrido de un viaje: la poligonal Origen → paradas → Destino y un punto por parada. Es la MISMA
// poligonal que sigue el simulador en el servidor (Leaflet une los puntos con tramos rectos, igual que RoutePath), por
// eso el marcador del vehículo nunca se sale de la línea dibujada. La usan el panel y el portal del operador.
const TripRouteLayer = (() => {
  const COLORS = { onTime: '#2dd4bf', delayed: '#f5a524', origin: '#34d399', destination: '#f0546a', waypoint: '#4c8dff' };
  const LINE_WEIGHT = 4;
  const LINE_OPACITY = 0.85;
  const ENDPOINT_RADIUS = 7;
  const WAYPOINT_RADIUS = 5;

  const toLatLng = (stop) => [stop.latitude, stop.longitude];

  // Un elemento con textContent evita tener que escapar el nombre de la parada a mano dentro del popup.
  function labelElement(label) {
    const element = document.createElement('span');
    element.textContent = label;
    return element;
  }

  function stopStyle(index, count) {
    const isOrigin = index === 0;
    const isDestination = index === count - 1;
    const color = isOrigin ? COLORS.origin : isDestination ? COLORS.destination : COLORS.waypoint;
    return { radius: isOrigin || isDestination ? ENDPOINT_RADIUS : WAYPOINT_RADIUS, color, fillColor: color, fillOpacity: 1 };
  }

  /**
   * @param stops [{ label, latitude, longitude }] en el orden en que se recorren
   * @param routeStatus 'Delayed' pinta la línea de ámbar; cualquier otro estado, del color normal
   */
  function build(stops, routeStatus) {
    const layer = L.layerGroup();
    const lineColor = routeStatus === 'Delayed' ? COLORS.delayed : COLORS.onTime;
    L.polyline(stops.map(toLatLng), { color: lineColor, weight: LINE_WEIGHT, opacity: LINE_OPACITY }).addTo(layer);
    stops.forEach((stop, index) => {
      L.circleMarker(toLatLng(stop), stopStyle(index, stops.length)).bindPopup(labelElement(stop.label)).addTo(layer);
    });
    return layer;
  }

  // Dos viajes con la misma firma se dibujan idénticos: permite no reconstruir las capas en cada refresco del mapa.
  function signature(stops, routeStatus) {
    return `${routeStatus}|${stops.map(stop => `${stop.latitude},${stop.longitude}`).join(';')}`;
  }

  return { build, signature };
})();
