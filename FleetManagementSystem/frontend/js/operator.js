/**
 * Lógica del portal del conductor/operador. Muestra la ruta activa
 * asignada, permite reportar complicaciones durante el viaje (alertas) y
 * abrir la navegación externa (Google Maps) con la ruta ya cargada.
 */

requireAuth('Operator');
document.getElementById('userFullName').textContent = currentUserFullName();
document.getElementById('logoutBtn').addEventListener('click', logout);

const ROUTE_STATUS_LABELS = { Planned: 'Planificada', InProgress: 'En progreso', Delayed: 'Retrasada', Completed: 'Completada', Cancelled: 'Cancelada' };
const ALERT_TYPE_LABELS = { Delay: 'Retraso', Breakdown: 'Avería mecánica', TrafficJam: 'Tráfico / trancón', Accident: 'Accidente', WeatherCondition: 'Clima adverso', Other: 'Otro' };
const CARGO_PRIORITY_LABELS = { Standard: 'Estándar', High: 'Alta', Urgent: 'Urgente', Fragile: 'Frágil' };

let map;
let routeLayer;
let currentRoute = null;
let myRoutes = [];

// Avance del viaje: se consulta más seguido que la ruta (que casi no cambia) y el marcador del vehículo se desliza
// durante todo el intervalo para que el movimiento sea continuo.
const PROGRESS_POLL_MS = 5000;
const ROUTE_POLL_MS = 15000;
let vehicleMarker = null;
let tripWasActive = false;

// El ícono no depende de nada que cambie: se crea una sola vez y se reutiliza (reusa los estilos del marcador del panel).
const VEHICLE_ICON = L.divIcon({
  className: '',
  html: '<div class="vehicle-marker-pin" style="background:#4c8dff"><span>🚚</span></div>',
  iconSize: [34, 34],
  iconAnchor: [17, 32],
  popupAnchor: [0, -30]
});

function escapeHtml(str) {
  if (str === null || str === undefined) return '';
  return String(str).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

function showToast(message, type = 'info') {
  const stack = document.getElementById('toastStack');
  const toast = document.createElement('div');
  toast.className = `toast ${type === 'error' ? 'toast-error' : type === 'success' ? 'toast-success' : ''}`;
  toast.textContent = message;
  stack.appendChild(toast);
  setTimeout(() => toast.remove(), 4500);
}

function openModal(innerHtml) {
  const root = document.getElementById('modalRoot');
  root.innerHTML = `<div class="modal-overlay" id="activeModalOverlay"><div class="modal-box">${innerHtml}</div></div>`;
  document.getElementById('activeModalOverlay').addEventListener('click', (e) => {
    if (e.target.id === 'activeModalOverlay') closeModal();
  });
}
function closeModal() { document.getElementById('modalRoot').innerHTML = ''; }

async function loadMyRoute() {
  const driverId = sessionStorage.getItem('fleet_driverid');
  if (!driverId) {
    showEmptyState();
    return;
  }

  try {
    myRoutes = await FleetAPI.getRoutesByDriver(driverId);
  } catch (err) {
    showToast(err.message, 'error');
    showEmptyState();
    return;
  }

  const active = myRoutes.find(r => r.status === 'InProgress' || r.status === 'Delayed')
    || myRoutes.find(r => r.status === 'Planned')
    || null;

  if (!active) {
    showEmptyState();
    return;
  }

  currentRoute = active;
  showRoute(active);
  await refreshProgress();
}

function showEmptyState() {
  document.getElementById('noRouteState').classList.remove('hidden');
  document.getElementById('routeContent').classList.add('hidden');
  document.getElementById('actionBar').style.display = 'none';
}

function showRoute(route) {
  document.getElementById('noRouteState').classList.add('hidden');
  document.getElementById('routeContent').classList.remove('hidden');
  document.getElementById('actionBar').style.display = 'flex';

  document.getElementById('routeName').textContent = route.name;
  const badge = document.getElementById('routeStatusBadge');
  badge.textContent = ROUTE_STATUS_LABELS[route.status] || route.status;
  badge.className = `badge badge-${route.status}`;

  document.getElementById('routeStatusSelect').value = route.status;
  document.getElementById('routeDistance').textContent = `${route.estimatedDistanceKm} km`;
  document.getElementById('routeDuration').textContent = `${Math.round(route.estimatedDurationMinutes)} min`;
  document.getElementById('routeScheduled').textContent = route.scheduledDate
    ? new Date(route.scheduledDate).toLocaleDateString('es-CO', { day: 'numeric', month: 'short' })
    : 'Sin fecha';

  const delayWrap = document.getElementById('routeDelayWrap');
  if (route.delayMinutes > 0) {
    delayWrap.classList.remove('hidden');
    document.getElementById('routeDelay').textContent = `${route.delayMinutes} min de retraso`;
  } else {
    delayWrap.classList.add('hidden');
  }

  renderWaypoints(route);
  renderCargo(route);
  renderMap(route);
  renderOtherRoutes(route);
}

// Origen → paradas → destino, en el orden en que se recorren y con la forma que usan la lista y el mapa.
function routeStops(route) {
  return [
    { label: 'Origen', latitude: route.originLat, longitude: route.originLng },
    ...route.waypoints.map(w => ({ label: w.label, latitude: w.latitude, longitude: w.longitude })),
    { label: 'Destino', latitude: route.destinationLat, longitude: route.destinationLng }
  ];
}

function renderWaypoints(route) {
  const list = document.getElementById('waypointList');
  list.innerHTML = routeStops(route).map((s, i) => `
    <li><span class="waypoint-index">${i + 1}</span> ${escapeHtml(s.label)}</li>
  `).join('');
}

function renderCargo(route) {
  const list = document.getElementById('cargoList');
  if (!route.cargoItems || route.cargoItems.length === 0) {
    list.innerHTML = '<li class="text-faint">Sin artículos de carga registrados.</li>';
    return;
  }
  list.innerHTML = route.cargoItems.map(c => `
    <li>📦 ${escapeHtml(c.description)} — ${c.weightKg} kg
      <span class="badge badge-neutral" style="margin-left:auto;">${CARGO_PRIORITY_LABELS[c.priority] || c.priority}</span>
    </li>
  `).join('');
}

function renderMap(route) {
  if (!map) {
    map = L.map('operatorMap', { zoomControl: false, dragging: true, scrollWheelZoom: false }).setView([route.originLat, route.originLng], 11);
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors', maxZoom: 19 }).addTo(map);
  }

  if (routeLayer) { map.removeLayer(routeLayer); }

  const stops = routeStops(route);
  const points = stops.map(s => [s.latitude, s.longitude]);
  routeLayer = TripRouteLayer.build(stops, route.status).addTo(map);

  setTimeout(() => {
    map.invalidateSize();
    map.fitBounds(points, { padding: [24, 24] });
  }, 60);
}

function renderOtherRoutes(active) {
  const container = document.getElementById('otherRoutesList');
  const others = myRoutes.filter(r => r.id !== active.id);
  if (others.length === 0) {
    container.textContent = 'No tiene más rutas programadas por ahora.';
    return;
  }
  container.innerHTML = others.map(r => `
    <div class="flex justify-between mt-8">
      <span>${escapeHtml(r.name)}</span>
      <span class="badge badge-${r.status}">${ROUTE_STATUS_LABELS[r.status] || r.status}</span>
    </div>
  `).join('');
}

// --- Avance del viaje: barra de progreso, kilómetros restantes, próxima parada, ETA y posición del vehículo ---
// El panel se crea al usarse por primera vez, justo encima del mapa, para no tener que modificar el HTML de la página.
function progressPanel() {
  let panel = document.getElementById('tripProgressPanel');
  if (!panel) {
    panel = document.createElement('div');
    panel.id = 'tripProgressPanel';
    panel.className = 'trip-progress-panel hidden';
    document.getElementById('operatorMap').before(panel);
  }
  return panel;
}

function renderTripProgress(trip) {
  const panel = progressPanel();
  if (trip === null) {
    panel.classList.add('hidden');
    removeVehicleMarker();
    return;
  }
  panel.classList.remove('hidden');
  panel.innerHTML = TripProgressUI.html(trip);
  moveVehicleMarker(trip);
}

function moveVehicleMarker(trip) {
  const position = [trip.latitude, trip.longitude];
  if (vehicleMarker) {
    MarkerMotion.glide(vehicleMarker, position, PROGRESS_POLL_MS);
    return;
  }
  vehicleMarker = L.marker(position, { icon: VEHICLE_ICON, zIndexOffset: 1000 })
    .addTo(map)
    .bindPopup(`🚚 ${escapeHtml(trip.licensePlate)}`);
}

function removeVehicleMarker() {
  if (!vehicleMarker) return;
  map.removeLayer(vehicleMarker);
  vehicleMarker = null;
}

async function refreshProgress() {
  if (!currentRoute) {
    tripWasActive = false;
    renderTripProgress(null);
    return;
  }

  let trips;
  try {
    trips = await FleetAPI.getTripProgress();
  } catch (err) {
    console.warn('No se pudo consultar el avance del viaje:', err.message);
    return;
  }

  const trip = trips.find(t => t.routeId === currentRoute.id) || null;
  const justFinished = trip === null && tripWasActive;
  tripWasActive = trip !== null;
  renderTripProgress(trip);

  // El viaje salió de la lista de activos: llegó al destino o alguien cambió el estado a mano. Se vuelve a leer la ruta
  // para mostrar su estado real sin esperar al siguiente ciclo de actualización.
  if (justFinished) await loadMyRoute();
}

// --- Cambiar estado de la ruta ---
document.getElementById('routeStatusSelect').addEventListener('change', async (e) => {
  if (!currentRoute) return;
  try {
    await FleetAPI.updateRouteStatus(currentRoute.id, e.target.value);
    showToast('Estado de la ruta actualizado.', 'success');
    await loadMyRoute();
  } catch (err) {
    showToast(err.message, 'error');
  }
});

// --- Reportar alerta/complicación ---
document.getElementById('reportAlertBtn').addEventListener('click', () => {
  if (!currentRoute) return;
  openModal(`
    <div class="modal-header"><h3>Reportar complicación</h3><button class="modal-close" onclick="closeModal()">✕</button></div>
    <form id="alertForm">
      <div class="modal-body">
        <div class="field">
          <label>Tipo de complicación</label>
          <select required name="type">
            ${Object.entries(ALERT_TYPE_LABELS).map(([k, v]) => `<option value="${k}">${v}</option>`).join('')}
          </select>
        </div>
        <div class="field"><label>Descripción</label><textarea required name="description" placeholder="Describa brevemente lo ocurrido…"></textarea></div>
        <div class="field"><label>Retraso estimado (minutos)</label><input type="number" min="0" name="delayMinutes" value="0" /></div>
        <p class="field-hint">Si indica minutos de retraso, la ruta se marcará automáticamente como "Retrasada".</p>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-ghost" onclick="closeModal()">Cancelar</button>
        <button type="submit" class="btn btn-danger">Enviar reporte</button>
      </div>
    </form>
  `);

  document.getElementById('alertForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const fd = new FormData(e.target);
    try {
      await FleetAPI.createAlert({
        routeId: currentRoute.id,
        type: fd.get('type'),
        description: fd.get('description'),
        delayMinutes: parseInt(fd.get('delayMinutes') || '0', 10)
      });
      closeModal();
      showToast('Complicación reportada. El despachador ha sido notificado.', 'success');
      await loadMyRoute();
    } catch (err) {
      showToast(err.message, 'error');
    }
  });
});

// --- Abrir navegación externa (integración con sistemas de navegación) ---
document.getElementById('openNavigationBtn').addEventListener('click', async () => {
  if (!currentRoute) return;
  try {
    const nav = await FleetAPI.getNavigationLink(currentRoute.id);
    window.open(nav.externalMapsUrl, '_blank', 'noopener');
  } catch (err) {
    showToast(err.message, 'error');
  }
});

document.getElementById('refreshRouteBtn').addEventListener('click', loadMyRoute);

// --- Arranque + actualización periódica ---
loadMyRoute();
setInterval(loadMyRoute, ROUTE_POLL_MS);
setInterval(refreshProgress, PROGRESS_POLL_MS);
