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

function renderWaypoints(route) {
  const list = document.getElementById('waypointList');
  const stops = [
    { label: 'Origen', lat: route.originLat, lng: route.originLng },
    ...route.waypoints.map(w => ({ label: w.label, lat: w.latitude, lng: w.longitude })),
    { label: 'Destino', lat: route.destinationLat, lng: route.destinationLng }
  ];
  list.innerHTML = stops.map((s, i) => `
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
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '&copy; OpenStreetMap' }).addTo(map);
  }

  if (routeLayer) { map.removeLayer(routeLayer); }

  const points = [
    [route.originLat, route.originLng],
    ...route.waypoints.map(w => [w.latitude, w.longitude]),
    [route.destinationLat, route.destinationLng]
  ];

  routeLayer = L.layerGroup().addTo(map);
  L.polyline(points, { color: '#2dd4bf', weight: 4, opacity: 0.85 }).addTo(routeLayer);
  L.circleMarker(points[0], { radius: 7, color: '#34d399', fillColor: '#34d399', fillOpacity: 1 }).addTo(routeLayer).bindPopup('Origen');
  L.circleMarker(points[points.length - 1], { radius: 7, color: '#f0546a', fillColor: '#f0546a', fillOpacity: 1 }).addTo(routeLayer).bindPopup('Destino');
  route.waypoints.forEach(w => {
    L.circleMarker([w.latitude, w.longitude], { radius: 5, color: '#4c8dff', fillColor: '#4c8dff', fillOpacity: 1 }).addTo(routeLayer).bindPopup(escapeHtml(w.label));
  });

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
setInterval(loadMyRoute, 15000);
