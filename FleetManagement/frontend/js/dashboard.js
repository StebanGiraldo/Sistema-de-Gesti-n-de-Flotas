/**
 * Lógica del panel administrativo (SPA). Consume la API REST del backend
 * mediante FleetAPI (js/api.js) y renderiza el mapa Leaflet, las tablas y
 * los formularios modales de cada módulo.
 */

requireAuth('Admin');
document.getElementById('userFullName').textContent = currentUserFullName();
document.getElementById('logoutBtn').addEventListener('click', logout);

// ---------------------------------------------------------------------
// Diccionarios de traducción para la interfaz en español
// ---------------------------------------------------------------------
const VEHICLE_STATUS_LABELS = { Available: 'Disponible', EnRoute: 'En ruta', Maintenance: 'Mantenimiento', OutOfService: 'Fuera de servicio' };
const VEHICLE_TYPE_LABELS = { Truck: 'Camión', Van: 'Furgoneta', Car: 'Automóvil', Motorcycle: 'Motocicleta' };
const VEHICLE_TYPE_ICONS = { Truck: '🚛', Van: '🚐', Car: '🚗', Motorcycle: '🏍️' };
const ROUTE_STATUS_LABELS = { Planned: 'Planificada', InProgress: 'En progreso', Delayed: 'Retrasada', Completed: 'Completada', Cancelled: 'Cancelada' };
const ALERT_TYPE_LABELS = { Delay: 'Retraso', Breakdown: 'Avería', TrafficJam: 'Tráfico', Accident: 'Accidente', WeatherCondition: 'Clima', Other: 'Otro' };
const ALERT_STATUS_LABELS = { Open: 'Abierta', Acknowledged: 'Reconocida', Resolved: 'Resuelta' };
const CARGO_PRIORITY_LABELS = { Standard: 'Estándar', High: 'Alta', Urgent: 'Urgente', Fragile: 'Frágil' };
const MAINTENANCE_TYPE_LABELS = { OilChange: 'Cambio de aceite', TireRotation: 'Rotación de neumáticos', BrakeInspection: 'Inspección de frenos', GeneralInspection: 'Revisión general', Repair: 'Reparación', Other: 'Otro' };
const STATUS_COLORS = { Available: '#34d399', EnRoute: '#4c8dff', Maintenance: '#f5a524', OutOfService: '#f0546a' };

// ---------------------------------------------------------------------
// Estado en memoria
// ---------------------------------------------------------------------
let map, markers = {};
let vehiclesCache = [], routesCache = [], driversCache = [];
let selectedVehicleId = null;
let pollTimer = null;

// ---------------------------------------------------------------------
// Utilidades: formato, escape HTML, toasts y modales
// ---------------------------------------------------------------------
function escapeHtml(str) {
  if (str === null || str === undefined) return '';
  return String(str).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

function formatDate(iso) {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('es-CO', { year: 'numeric', month: 'short', day: 'numeric' });
}

function formatDateTime(iso) {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('es-CO', { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
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
function closeModal() {
  document.getElementById('modalRoot').innerHTML = '';
}

// ---------------------------------------------------------------------
// Pestañas
// ---------------------------------------------------------------------
function switchTab(tabName) {
  document.querySelectorAll('.tab-btn').forEach(btn => btn.classList.toggle('active', btn.dataset.tab === tabName));
  document.querySelectorAll('.tab-panel').forEach(panel => panel.classList.toggle('active', panel.id === `tab-${tabName}`));

  if (tabName === 'map' && map) setTimeout(() => map.invalidateSize(), 50);
  if (tabName === 'routes') refreshRoutes();
  if (tabName === 'maintenance') refreshMaintenance();
  if (tabName === 'alerts') refreshAlerts();
  if (tabName === 'audit') refreshAudit();
}
document.querySelectorAll('.tab-btn').forEach(btn => btn.addEventListener('click', () => switchTab(btn.dataset.tab)));

// ---------------------------------------------------------------------
// Mapa y vehículos (monitoreo en tiempo real)
// ---------------------------------------------------------------------
function initMap() {
  map = L.map('map', { zoomControl: true }).setView([7.1193, -73.1227], 7);
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
    maxZoom: 19
  }).addTo(map);
}

function vehicleDivIcon(vehicle) {
  const color = STATUS_COLORS[vehicle.status] || '#8b93a4';
  const emoji = VEHICLE_TYPE_ICONS[vehicle.type] || '📍';
  return L.divIcon({
    className: '',
    html: `<div class="vehicle-marker-pin" style="background:${color}"><span>${emoji}</span></div>`,
    iconSize: [34, 34],
    iconAnchor: [17, 32],
    popupAnchor: [0, -30]
  });
}

function popupHtml(v) {
  return `
    <strong>${escapeHtml(v.licensePlate)}</strong> — ${escapeHtml(v.brand)} ${escapeHtml(v.model)}<br/>
    ${v.assignedDriverName ? 'Conductor: ' + escapeHtml(v.assignedDriverName) : 'Sin conductor asignado'}<br/>
    ${Math.round(v.mileageKm).toLocaleString('es-CO')} km recorridos<br/>
    <select class="vehicle-status-select" data-id="${v.id}" style="margin-top:6px;width:100%;padding:4px;border-radius:6px;">
      <option value="Available" ${v.status === 'Available' ? 'selected' : ''}>Disponible</option>
      <option value="EnRoute" ${v.status === 'EnRoute' ? 'selected' : ''}>En ruta</option>
      <option value="Maintenance" ${v.status === 'Maintenance' ? 'selected' : ''}>Mantenimiento</option>
      <option value="OutOfService" ${v.status === 'OutOfService' ? 'selected' : ''}>Fuera de servicio</option>
    </select>
  `;
}

// Delegación de eventos: el select de estado vive dentro de un popup de Leaflet creado dinámicamente.
document.addEventListener('change', async (e) => {
  if (e.target.classList.contains('vehicle-status-select')) {
    try {
      await FleetAPI.updateVehicleStatus(e.target.dataset.id, e.target.value);
      showToast('Estado del vehículo actualizado.', 'success');
      await refreshVehicles(true);
    } catch (err) {
      showToast(err.message, 'error');
    }
  }
});

async function refreshVehicles(silent) {
  try {
    vehiclesCache = await FleetAPI.getVehicles();
    renderVehicleList();
    renderMarkers();
  } catch (err) {
    if (!silent) showToast(err.message, 'error');
  }
}

function renderMarkers() {
  const seen = new Set();
  vehiclesCache.forEach(v => {
    seen.add(v.id);
    const latlng = [v.latitude, v.longitude];
    if (markers[v.id]) {
      markers[v.id].setLatLng(latlng);
      markers[v.id].setIcon(vehicleDivIcon(v));
      markers[v.id].getPopup().setContent(popupHtml(v));
    } else {
      markers[v.id] = L.marker(latlng, { icon: vehicleDivIcon(v) }).addTo(map).bindPopup(popupHtml(v));
    }
  });
  Object.keys(markers).forEach(id => {
    if (!seen.has(id)) { map.removeLayer(markers[id]); delete markers[id]; }
  });
}

function renderVehicleList() {
  const container = document.getElementById('vehicleList');
  document.getElementById('vehicleCount').textContent = vehiclesCache.length;

  if (vehiclesCache.length === 0) {
    container.innerHTML = '<p class="text-faint text-sm" style="padding:10px 14px;">No hay vehículos registrados.</p>';
    return;
  }

  container.innerHTML = vehiclesCache.map(v => `
    <div class="vehicle-item ${v.id === selectedVehicleId ? 'selected' : ''}" data-id="${v.id}">
      <div class="vehicle-item-header">
        <span class="status-dot" style="color:${STATUS_COLORS[v.status]}"></span>
        <span class="plate">${escapeHtml(v.licensePlate)}</span>
        <span class="badge badge-neutral">${VEHICLE_TYPE_ICONS[v.type] || ''} ${VEHICLE_TYPE_LABELS[v.type] || v.type}</span>
        <button class="dynamic-list-remove clone-vehicle-btn" data-id="${v.id}" title="Clonar vehículo (Prototype)" style="margin-left:auto;">⧉</button>
      </div>
      <div class="vehicle-item-meta">
        <span>${escapeHtml(v.brand)} ${escapeHtml(v.model)}</span>
        <span>${VEHICLE_STATUS_LABELS[v.status] || v.status}</span>
      </div>
    </div>
  `).join('');

  container.querySelectorAll('.vehicle-item').forEach(el => {
    el.addEventListener('click', () => {
      const v = vehiclesCache.find(x => x.id === el.dataset.id);
      if (!v) return;
      selectedVehicleId = v.id;
      renderVehicleList();
      switchTab('map');
      map.setView([v.latitude, v.longitude], 14);
      markers[v.id] && markers[v.id].openPopup();
    });
  });

  container.querySelectorAll('.clone-vehicle-btn').forEach(btn => {
    btn.addEventListener('click', (e) => { e.stopPropagation(); openCloneVehicleModal(btn.dataset.id); });
  });
}

// --- Modal: nuevo vehículo (Factory Method + Abstract Factory) ---
document.getElementById('addVehicleBtn').addEventListener('click', () => {
  openModal(`
    <div class="modal-header"><h3>Nuevo vehículo</h3><button class="modal-close" onclick="closeModal()">✕</button></div>
    <form id="addVehicleForm">
      <div class="modal-body">
        <div class="field-row">
          <div class="field"><label>Placa</label><input required name="licensePlate" placeholder="ABC-123" /></div>
          <div class="field"><label>Tipo</label>
            <select name="type" required>
              <option value="Truck">Camión</option>
              <option value="Van">Furgoneta</option>
              <option value="Car">Automóvil</option>
              <option value="Motorcycle">Motocicleta</option>
            </select>
          </div>
        </div>
        <div class="field-row">
          <div class="field"><label>Marca</label><input required name="brand" placeholder="Volvo" /></div>
          <div class="field"><label>Modelo</label><input required name="model" placeholder="FH 460" /></div>
        </div>
        <div class="field"><label>Año</label><input required type="number" name="year" min="1990" max="2030" value="2023" /></div>
        <div class="field-row">
          <div class="field"><label>Latitud</label><input required type="number" step="0.0001" name="latitude" value="7.1193" /></div>
          <div class="field"><label>Longitud</label><input required type="number" step="0.0001" name="longitude" value="-73.1227" /></div>
        </div>
        <p class="field-hint">Referencias: Bucaramanga (7.1193, -73.1227) · Girón (7.0806, -73.1716) · Bogotá (4.7110, -74.0721) · Medellín (6.2442, -75.5812)</p>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-ghost" onclick="closeModal()">Cancelar</button>
        <button type="submit" class="btn btn-primary">Crear vehículo</button>
      </div>
    </form>
  `);

  document.getElementById('addVehicleForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const fd = new FormData(e.target);
    try {
      await FleetAPI.createVehicle({
        licensePlate: fd.get('licensePlate'), brand: fd.get('brand'), model: fd.get('model'),
        year: parseInt(fd.get('year'), 10), type: fd.get('type'),
        latitude: parseFloat(fd.get('latitude')), longitude: parseFloat(fd.get('longitude'))
      });
      closeModal();
      showToast('Vehículo creado (patrones Factory Method + Abstract Factory).', 'success');
      await refreshVehicles();
    } catch (err) {
      showToast(err.message, 'error');
    }
  });
});

// --- Modal: clonar vehículo (Prototype) ---
function openCloneVehicleModal(templateId) {
  const template = vehiclesCache.find(v => v.id === templateId);
  if (!template) return;
  openModal(`
    <div class="modal-header"><h3>Clonar vehículo</h3><button class="modal-close" onclick="closeModal()">✕</button></div>
    <form id="cloneVehicleForm">
      <div class="modal-body">
        <p class="text-sm text-muted">Se creará un nuevo vehículo copiando marca, modelo, tipo y capacidad de <strong>${escapeHtml(template.licensePlate)}</strong> (patrón Prototype). Sólo indique la nueva placa.</p>
        <div class="field mt-16"><label>Nueva placa</label><input required name="newLicensePlate" placeholder="XYZ-999" /></div>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-ghost" onclick="closeModal()">Cancelar</button>
        <button type="submit" class="btn btn-primary">Clonar vehículo</button>
      </div>
    </form>
  `);

  document.getElementById('cloneVehicleForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const fd = new FormData(e.target);
    try {
      await FleetAPI.cloneVehicle(templateId, fd.get('newLicensePlate'));
      closeModal();
      showToast('Vehículo clonado a partir de la plantilla (patrón Prototype).', 'success');
      await refreshVehicles();
    } catch (err) {
      showToast(err.message, 'error');
    }
  });
}

// ---------------------------------------------------------------------
// Rutas (Builder + Prototype + Director)
// ---------------------------------------------------------------------
async function refreshRoutes() {
  try {
    routesCache = await FleetAPI.getRoutes();
    renderRoutesTable();
  } catch (err) {
    showToast(err.message, 'error');
  }
}

function renderRoutesTable() {
  const tbody = document.getElementById('routesTableBody');
  if (routesCache.length === 0) {
    tbody.innerHTML = '<tr class="empty-row"><td colspan="8">No hay rutas registradas.</td></tr>';
    return;
  }
  tbody.innerHTML = routesCache.map(r => `
    <tr>
      <td>${escapeHtml(r.name)}</td>
      <td class="cell-muted">${r.assignedVehiclePlate ? escapeHtml(r.assignedVehiclePlate) : '—'}</td>
      <td class="cell-muted">${r.assignedDriverName ? escapeHtml(r.assignedDriverName) : '—'}</td>
      <td>
        <select class="route-status-select" data-id="${r.id}">
          ${Object.keys(ROUTE_STATUS_LABELS).map(s => `<option value="${s}" ${r.status === s ? 'selected' : ''}>${ROUTE_STATUS_LABELS[s]}</option>`).join('')}
        </select>
      </td>
      <td class="cell-muted">${r.estimatedDistanceKm} km · ${Math.round(r.estimatedDurationMinutes)} min</td>
      <td class="cell-muted">${r.scheduledDate ? formatDate(r.scheduledDate) : '—'}</td>
      <td class="cell-muted">${r.delayMinutes > 0 ? r.delayMinutes + ' min' : '—'}</td>
      <td><button class="btn btn-ghost btn-sm duplicate-route-btn" data-id="${r.id}" title="Duplicar ruta (Prototype)">⧉ Duplicar</button></td>
    </tr>
  `).join('');

  tbody.querySelectorAll('.duplicate-route-btn').forEach(btn => btn.addEventListener('click', () => duplicateRoute(btn.dataset.id)));
  tbody.querySelectorAll('.route-status-select').forEach(sel => {
    sel.addEventListener('change', async () => {
      try {
        await FleetAPI.updateRouteStatus(sel.dataset.id, sel.value);
        showToast('Estado de la ruta actualizado.', 'success');
        await refreshRoutes();
        await refreshVehicles(true);
      } catch (err) {
        showToast(err.message, 'error');
      }
    });
  });
}

async function duplicateRoute(routeId) {
  try {
    await FleetAPI.duplicateRoute(routeId, null);
    showToast('Ruta duplicada para el día siguiente (patrón Prototype).', 'success');
    await refreshRoutes();
  } catch (err) {
    showToast(err.message, 'error');
  }
}

function vehicleOptionsHtml() {
  return '<option value="">— Sin asignar —</option>' +
    vehiclesCache.map(v => `<option value="${v.id}">${escapeHtml(v.licensePlate)} · ${VEHICLE_TYPE_LABELS[v.type] || v.type}</option>`).join('');
}
function driverOptionsHtml() {
  return '<option value="">— Sin asignar —</option>' +
    driversCache.map(d => `<option value="${d.id}">${escapeHtml(d.fullName)}</option>`).join('');
}

// --- Modal: nueva ruta paso a paso (Builder) ---
let waypointsDraft = [];
let cargoDraft = [];

document.getElementById('createRouteBtn').addEventListener('click', async () => {
  waypointsDraft = [];
  cargoDraft = [];
  if (driversCache.length === 0) { try { driversCache = await FleetAPI.getDrivers(); } catch { /* se listará vacío */ } }

  openModal(`
    <div class="modal-header"><h3>Nueva ruta</h3><button class="modal-close" onclick="closeModal()">✕</button></div>
    <form id="createRouteForm">
      <div class="modal-body">
        <div class="field"><label>Nombre de la ruta</label><input required name="name" placeholder="Ruta Girón - Bucaramanga" /></div>
        <div class="field-row">
          <div class="field"><label>Origen (lat, lng)</label>
            <div class="field-row"><input required type="number" step="0.0001" name="originLat" placeholder="Lat" value="7.0806" /><input required type="number" step="0.0001" name="originLng" placeholder="Lng" value="-73.1716" /></div>
          </div>
        </div>
        <div class="field-row">
          <div class="field"><label>Destino (lat, lng)</label>
            <div class="field-row"><input required type="number" step="0.0001" name="destinationLat" placeholder="Lat" value="7.1193" /><input required type="number" step="0.0001" name="destinationLng" placeholder="Lng" value="-73.1227" /></div>
          </div>
        </div>

        <div class="field">
          <label>Paradas intermedias</label>
          <div id="waypointsList" class="dynamic-list"><p class="dynamic-list-empty">Sin paradas agregadas.</p></div>
          <div class="field-row">
            <input type="text" id="wpLabel" placeholder="Etiqueta" style="flex:2;" />
            <input type="number" step="0.0001" id="wpLat" placeholder="Lat" style="flex:1;" />
            <input type="number" step="0.0001" id="wpLng" placeholder="Lng" style="flex:1;" />
            <button type="button" id="addWaypointBtn" class="btn btn-secondary btn-sm">+ Agregar</button>
          </div>
        </div>

        <div class="field">
          <label>Artículos de carga</label>
          <div id="cargoList" class="dynamic-list"><p class="dynamic-list-empty">Sin artículos agregados.</p></div>
          <div class="field-row">
            <input type="text" id="cargoDesc" placeholder="Descripción" style="flex:2;" />
            <input type="number" step="0.1" id="cargoWeight" placeholder="Kg" style="flex:1;" />
            <select id="cargoPriority" style="flex:1;">
              <option value="Standard">Estándar</option><option value="High">Alta</option>
              <option value="Urgent">Urgente</option><option value="Fragile">Frágil</option>
            </select>
            <button type="button" id="addCargoBtn" class="btn btn-secondary btn-sm">+ Agregar</button>
          </div>
        </div>

        <div class="field-row">
          <div class="field"><label>Vehículo asignado</label><select name="assignedVehicleId">${vehicleOptionsHtml()}</select></div>
          <div class="field"><label>Conductor asignado</label><select name="assignedDriverId">${driverOptionsHtml()}</select></div>
        </div>
        <div class="field"><label>Fecha programada</label><input type="date" name="scheduledDate" /></div>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-ghost" onclick="closeModal()">Cancelar</button>
        <button type="submit" class="btn btn-primary">Crear ruta (Builder)</button>
      </div>
    </form>
  `);

  renderWaypointsDraft();
  renderCargoDraft();

  document.getElementById('addWaypointBtn').addEventListener('click', () => {
    const label = document.getElementById('wpLabel').value.trim();
    const lat = parseFloat(document.getElementById('wpLat').value);
    const lng = parseFloat(document.getElementById('wpLng').value);
    if (!label || Number.isNaN(lat) || Number.isNaN(lng)) { showToast('Complete etiqueta, latitud y longitud de la parada.', 'error'); return; }
    waypointsDraft.push({ label, latitude: lat, longitude: lng, order: waypointsDraft.length });
    document.getElementById('wpLabel').value = ''; document.getElementById('wpLat').value = ''; document.getElementById('wpLng').value = '';
    renderWaypointsDraft();
  });

  document.getElementById('addCargoBtn').addEventListener('click', () => {
    const description = document.getElementById('cargoDesc').value.trim();
    const weightKg = parseFloat(document.getElementById('cargoWeight').value);
    const priority = document.getElementById('cargoPriority').value;
    if (!description || Number.isNaN(weightKg)) { showToast('Complete la descripción y el peso del artículo.', 'error'); return; }
    cargoDraft.push({ description, weightKg, volumeM3: 0, priority });
    document.getElementById('cargoDesc').value = ''; document.getElementById('cargoWeight').value = '';
    renderCargoDraft();
  });

  document.getElementById('createRouteForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const fd = new FormData(e.target);
    const scheduledDateRaw = fd.get('scheduledDate');
    try {
      await FleetAPI.createRoute({
        name: fd.get('name'),
        originLat: parseFloat(fd.get('originLat')), originLng: parseFloat(fd.get('originLng')),
        destinationLat: parseFloat(fd.get('destinationLat')), destinationLng: parseFloat(fd.get('destinationLng')),
        waypoints: waypointsDraft, cargoItems: cargoDraft,
        assignedVehicleId: fd.get('assignedVehicleId') || null,
        assignedDriverId: fd.get('assignedDriverId') || null,
        scheduledDate: scheduledDateRaw ? new Date(scheduledDateRaw).toISOString() : null
      });
      closeModal();
      showToast('Ruta creada paso a paso (patrón Builder).', 'success');
      await refreshRoutes();
    } catch (err) {
      showToast(err.message, 'error');
    }
  });
});

function renderWaypointsDraft() {
  const el = document.getElementById('waypointsList');
  if (!el) return;
  if (waypointsDraft.length === 0) { el.innerHTML = '<p class="dynamic-list-empty">Sin paradas agregadas.</p>'; return; }
  el.innerHTML = waypointsDraft.map((w, i) => `
    <div class="dynamic-list-row">
      <span class="row-text">${i + 1}. ${escapeHtml(w.label)} (${w.latitude}, ${w.longitude})</span>
      <button type="button" class="dynamic-list-remove" data-index="${i}">✕</button>
    </div>
  `).join('');
  el.querySelectorAll('.dynamic-list-remove').forEach(btn => btn.addEventListener('click', () => {
    waypointsDraft.splice(parseInt(btn.dataset.index, 10), 1);
    waypointsDraft.forEach((w, i) => w.order = i);
    renderWaypointsDraft();
  }));
}

function renderCargoDraft() {
  const el = document.getElementById('cargoList');
  if (!el) return;
  if (cargoDraft.length === 0) { el.innerHTML = '<p class="dynamic-list-empty">Sin artículos agregados.</p>'; return; }
  el.innerHTML = cargoDraft.map((c, i) => `
    <div class="dynamic-list-row">
      <span class="row-text">${escapeHtml(c.description)} — ${c.weightKg} kg · ${CARGO_PRIORITY_LABELS[c.priority] || c.priority}</span>
      <button type="button" class="dynamic-list-remove" data-index="${i}">✕</button>
    </div>
  `).join('');
  el.querySelectorAll('.dynamic-list-remove').forEach(btn => btn.addEventListener('click', () => {
    cargoDraft.splice(parseInt(btn.dataset.index, 10), 1);
    renderCargoDraft();
  }));
}

// --- Modal: ruta express (Builder + Director) ---
document.getElementById('createExpressRouteBtn').addEventListener('click', async () => {
  if (driversCache.length === 0) { try { driversCache = await FleetAPI.getDrivers(); } catch { /* vacío */ } }
  if (vehiclesCache.length === 0) { showToast('No hay vehículos registrados para asignar.', 'error'); return; }

  openModal(`
    <div class="modal-header"><h3>Ruta express</h3><button class="modal-close" onclick="closeModal()">✕</button></div>
    <form id="expressRouteForm">
      <div class="modal-body">
        <p class="text-sm text-muted">Crea una ruta con carga urgente y salida inmediata usando el Director del patrón Builder (DeliveryRouteDirector).</p>
        <div class="field mt-16"><label>Nombre</label><input required name="name" placeholder="Entrega urgente centro" /></div>
        <div class="field-row">
          <div class="field"><label>Origen lat</label><input required type="number" step="0.0001" name="originLat" value="7.1193" /></div>
          <div class="field"><label>Origen lng</label><input required type="number" step="0.0001" name="originLng" value="-73.1227" /></div>
        </div>
        <div class="field-row">
          <div class="field"><label>Destino lat</label><input required type="number" step="0.0001" name="destinationLat" value="7.0806" /></div>
          <div class="field"><label>Destino lng</label><input required type="number" step="0.0001" name="destinationLng" value="-73.1716" /></div>
        </div>
        <div class="field-row">
          <div class="field"><label>Vehículo</label><select required name="assignedVehicleId">${vehiclesCache.map(v => `<option value="${v.id}">${escapeHtml(v.licensePlate)}</option>`).join('')}</select></div>
          <div class="field"><label>Conductor</label><select required name="assignedDriverId">${driversCache.map(d => `<option value="${d.id}">${escapeHtml(d.fullName)}</option>`).join('')}</select></div>
        </div>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-ghost" onclick="closeModal()">Cancelar</button>
        <button type="submit" class="btn btn-primary">Crear ruta express</button>
      </div>
    </form>
  `);

  document.getElementById('expressRouteForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const fd = new FormData(e.target);
    try {
      await FleetAPI.createExpressRoute({
        name: fd.get('name'),
        originLat: parseFloat(fd.get('originLat')), originLng: parseFloat(fd.get('originLng')),
        destinationLat: parseFloat(fd.get('destinationLat')), destinationLng: parseFloat(fd.get('destinationLng')),
        assignedVehicleId: fd.get('assignedVehicleId'), assignedDriverId: fd.get('assignedDriverId')
      });
      closeModal();
      showToast('Ruta express creada (Builder + Director).', 'success');
      await refreshRoutes();
    } catch (err) {
      showToast(err.message, 'error');
    }
  });
});

// ---------------------------------------------------------------------
// Mantenimiento predictivo
// ---------------------------------------------------------------------
async function refreshMaintenance() {
  try {
    const [records, due] = await Promise.all([FleetAPI.getMaintenance(), FleetAPI.getMaintenanceDue()]);
    renderMaintenanceTable(records);
    renderMaintenanceDueBanner(due);
  } catch (err) {
    showToast(err.message, 'error');
  }
}

function renderMaintenanceDueBanner(due) {
  const banner = document.getElementById('maintenanceDueBanner');
  if (!due || due.length === 0) { banner.classList.remove('visible'); return; }
  banner.classList.add('visible');
  banner.innerHTML = `<strong>⚠ ${due.length} tarea(s) de mantenimiento vencida(s)</strong>` +
    due.map(d => `${escapeHtml(d.vehiclePlate)}: ${MAINTENANCE_TYPE_LABELS[d.taskName] || d.taskName}${d.dueDate ? ' (venció ' + formatDate(d.dueDate) + ')' : ''}`).join(' · ');
}

function renderMaintenanceTable(records) {
  const tbody = document.getElementById('maintenanceTableBody');
  if (records.length === 0) {
    tbody.innerHTML = '<tr class="empty-row"><td colspan="6">No hay registros de mantenimiento.</td></tr>';
    return;
  }
  tbody.innerHTML = records.map(r => `
    <tr>
      <td>${escapeHtml(r.vehiclePlate)}</td>
      <td>${MAINTENANCE_TYPE_LABELS[r.type] || r.type}</td>
      <td class="cell-muted">${formatDate(r.performedAt)}</td>
      <td class="cell-muted">${r.nextDueDate ? formatDate(r.nextDueDate) : '—'}</td>
      <td class="cell-muted">${Math.round(r.mileageAtServiceKm).toLocaleString('es-CO')} km</td>
      <td class="cell-muted">${escapeHtml(r.notes)}</td>
    </tr>
  `).join('');
}

document.getElementById('registerMaintenanceBtn').addEventListener('click', () => {
  if (vehiclesCache.length === 0) { showToast('No hay vehículos registrados.', 'error'); return; }
  openModal(`
    <div class="modal-header"><h3>Registrar mantenimiento</h3><button class="modal-close" onclick="closeModal()">✕</button></div>
    <form id="maintenanceForm">
      <div class="modal-body">
        <div class="field"><label>Vehículo</label><select required name="vehicleId">${vehiclesCache.map(v => `<option value="${v.id}">${escapeHtml(v.licensePlate)} — ${escapeHtml(v.brand)} ${escapeHtml(v.model)}</option>`).join('')}</select></div>
        <div class="field"><label>Tipo de mantenimiento</label>
          <select required name="type">
            ${Object.entries(MAINTENANCE_TYPE_LABELS).map(([k, v]) => `<option value="${k}">${v}</option>`).join('')}
          </select>
        </div>
        <div class="field-row">
          <div class="field"><label>Fecha realizado</label><input required type="date" name="performedAt" value="${new Date().toISOString().slice(0,10)}" /></div>
          <div class="field"><label>Próximo vencimiento</label><input type="date" name="nextDueDate" /></div>
        </div>
        <div class="field-row">
          <div class="field"><label>Km al momento del servicio</label><input required type="number" step="1" name="mileageAtServiceKm" value="0" /></div>
          <div class="field"><label>Próximo vencimiento (km)</label><input type="number" step="1" name="nextDueMileageKm" /></div>
        </div>
        <div class="field"><label>Notas</label><textarea name="notes" placeholder="Detalles del servicio realizado…"></textarea></div>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-ghost" onclick="closeModal()">Cancelar</button>
        <button type="submit" class="btn btn-primary">Registrar</button>
      </div>
    </form>
  `);

  document.getElementById('maintenanceForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const fd = new FormData(e.target);
    try {
      await FleetAPI.createMaintenance({
        vehicleId: fd.get('vehicleId'), type: fd.get('type'),
        performedAt: new Date(fd.get('performedAt')).toISOString(),
        nextDueDate: fd.get('nextDueDate') ? new Date(fd.get('nextDueDate')).toISOString() : null,
        nextDueMileageKm: fd.get('nextDueMileageKm') ? parseFloat(fd.get('nextDueMileageKm')) : null,
        notes: fd.get('notes') || '',
        mileageAtServiceKm: parseFloat(fd.get('mileageAtServiceKm'))
      });
      closeModal();
      showToast('Mantenimiento registrado correctamente.', 'success');
      await refreshMaintenance();
      await refreshVehicles(true);
    } catch (err) {
      showToast(err.message, 'error');
    }
  });
});

// ---------------------------------------------------------------------
// Alertas de viaje
// ---------------------------------------------------------------------
async function refreshAlerts() {
  try {
    const alerts = await FleetAPI.getAlerts();
    renderAlertsTable(alerts);
  } catch (err) {
    showToast(err.message, 'error');
  }
}

function renderAlertsTable(alerts) {
  const tbody = document.getElementById('alertsTableBody');
  if (alerts.length === 0) {
    tbody.innerHTML = '<tr class="empty-row"><td colspan="7">No hay alertas reportadas.</td></tr>';
    return;
  }
  tbody.innerHTML = alerts.map(a => `
    <tr>
      <td>${escapeHtml(a.routeName)}</td>
      <td>${ALERT_TYPE_LABELS[a.type] || a.type}</td>
      <td class="cell-muted">${escapeHtml(a.description)}</td>
      <td class="cell-muted">${a.delayMinutes > 0 ? a.delayMinutes + ' min' : '—'}</td>
      <td><span class="badge ${a.status === 'Resolved' ? 'badge-Completed' : 'badge-Delayed'}">${ALERT_STATUS_LABELS[a.status] || a.status}</span></td>
      <td class="cell-muted">${formatDateTime(a.createdAt)}</td>
      <td>${a.status !== 'Resolved' ? `<button class="btn btn-secondary btn-sm resolve-alert-btn" data-id="${a.id}">Resolver</button>` : ''}</td>
    </tr>
  `).join('');

  tbody.querySelectorAll('.resolve-alert-btn').forEach(btn => btn.addEventListener('click', async () => {
    try {
      await FleetAPI.resolveAlert(btn.dataset.id);
      showToast('Alerta marcada como resuelta.', 'success');
      await refreshAlerts();
    } catch (err) {
      showToast(err.message, 'error');
    }
  }));
}

// ---------------------------------------------------------------------
// Auditoría (patrón Singleton)
// ---------------------------------------------------------------------
async function refreshAudit() {
  try {
    const logs = await FleetAPI.getAuditLogs(150);
    renderAuditTable(logs);
  } catch (err) {
    showToast(err.message, 'error');
  }
}

function renderAuditTable(logs) {
  const tbody = document.getElementById('auditTableBody');
  if (logs.length === 0) {
    tbody.innerHTML = '<tr class="empty-row"><td colspan="4">Aún no hay eventos registrados.</td></tr>';
    return;
  }
  tbody.innerHTML = logs.map(l => `
    <tr>
      <td class="cell-muted">${formatDateTime(l.timestamp)}</td>
      <td><span class="badge badge-accent">${escapeHtml(l.category)}</span></td>
      <td>${escapeHtml(l.message)}</td>
      <td class="cell-muted">${l.username ? escapeHtml(l.username) : '—'}</td>
    </tr>
  `).join('');
}

document.getElementById('refreshAuditBtn').addEventListener('click', refreshAudit);

// ---------------------------------------------------------------------
// Arranque
// ---------------------------------------------------------------------
(async function init() {
  initMap();
  await refreshVehicles();
  try { driversCache = await FleetAPI.getDrivers(); } catch { /* se listará vacío si falla */ }

  // Monitoreo en tiempo real simulado: se vuelve a consultar la API periódicamente.
  pollTimer = setInterval(() => refreshVehicles(true), 5000);
})();
