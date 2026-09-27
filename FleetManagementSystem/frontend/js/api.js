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

  // --- Navegación externa ---
  getNavigationLink: (routeId) => apiFetch(`/navigation/route/${routeId}`),

  // --- Auditoría (Singleton) ---
  getAuditLogs: (count = 100) => apiFetch(`/audit/logs?count=${count}`),
};
