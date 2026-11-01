/**
 * Optimización de rutas y asignación de cargas en el panel administrativo (requerimiento #2).
 *
 * - "Planificar reparto": el administrador indica desde dónde salen los vehículos y qué hay que entregar. La API propone qué
 *   vehículo lleva cada entrega (sin pasarse de su capacidad), en qué orden las visita, y lo compara con repartir sin
 *   optimizar. Si el plan convence, se convierte en rutas planificadas.
 * - "Optimizar" (en cada ruta de la tabla): propone el mejor orden de las paradas de esa ruta y, si la ruta sigue
 *   planificada, permite aplicarlo.
 *
 * Este archivo solo PRESENTA: los cálculos y las reglas viven en el backend (DispatchController y
 * RouteOptimizationController). Usa las utilidades de dashboard.js (modales, avisos, cachés), que se carga antes.
 */

const PLAN_COLORS = ['#2563eb', '#dc2626', '#16a34a', '#9333ea', '#ea580c', '#0891b2'];
const OPTIMIZED_COLOR = '#16a34a';
const CURRENT_COLOR = '#6b7280';
const MINUTES_PER_HOUR = 60;

// Datos de ejemplo de Santander para probar sin escribir coordenadas a mano (las coordenadas son reales, los pedidos no).
const SAMPLE_ORIGIN = { latitude: 7.1193, longitude: -73.1227 }; // Bucaramanga
const SAMPLE_DELIVERIES = [
  { description: 'Piedecuesta · repuestos', weightKg: 50, volumeM3: 0.5, priority: 'Standard', latitude: 6.9876, longitude: -73.0505 },
  { description: 'Girón · suministros', weightKg: 20, volumeM3: 0.3, priority: 'Standard', latitude: 7.0806, longitude: -73.1716 },
  { description: 'Floridablanca · documentos', weightKg: 10, volumeM3: 0.2, priority: 'Standard', latitude: 7.0622, longitude: -73.0864 },
  { description: 'Lebrija · paquetes', weightKg: 5, volumeM3: 0.1, priority: 'Standard', latitude: 7.1139, longitude: -73.2194 }
];

let dispatchDraft = [];     // entregas que el administrador va agregando
let dispatchRequest = null; // solicitud con la que se calculó el plan que se está viendo: la misma que se aplica
let planMap = null;         // minimapa del modal abierto (solo hay un modal a la vez)

// ---------------------------------------------------------------------
// Utilidades de presentación
// ---------------------------------------------------------------------
function formatNumber(value, maxDigits = 1) {
  return Number(value).toLocaleString('es-CO', { minimumFractionDigits: 0, maximumFractionDigits: maxDigits });
}

function formatMinutes(minutes) {
  if (minutes < MINUTES_PER_HOUR) return `${formatNumber(minutes, 0)} min`;
  const hours = Math.floor(minutes / MINUTES_PER_HOUR);
  const rest = Math.round(minutes - hours * MINUTES_PER_HOUR);
  return `${hours} h ${String(rest).padStart(2, '0')} min`;
}

function priorityLabel(priority) {
  return CARGO_PRIORITY_LABELS[priority] || priority;
}

function numberedIcon(label, color) {
  return L.divIcon({
    className: '',
    html: `<div class="plan-pin" style="background:${color}">${escapeHtml(label)}</div>`,
    iconSize: [24, 24],
    iconAnchor: [12, 12]
  });
}

function destroyPlanMap() {
  if (!planMap) return;
  try { planMap.remove(); } catch { /* el contenedor ya no está en el DOM */ }
  planMap = null;
}

// Crea el minimapa dentro del modal. Los modales no se cierran con un método propio de Leaflet: por eso se registra su
// limpieza para cuando el modal se cierre (ver onModalClose en dashboard.js).
function createPlanMap(containerId) {
  destroyPlanMap();
  planMap = L.map(containerId, { zoomControl: true, scrollWheelZoom: false });
  addBaseTiles(planMap);
  return planMap;
}

function fitPlanMap(points) {
  if (points.length === 1) planMap.setView(points[0], 13);
  else planMap.fitBounds(points, { padding: [30, 30], maxZoom: 14 });
  setTimeout(() => { if (planMap) planMap.invalidateSize(); }, 80);
}

// ---------------------------------------------------------------------
// Planificar reparto
// ---------------------------------------------------------------------
document.getElementById('planDispatchBtn').addEventListener('click', openDispatchModal);

function openDispatchModal() {
  dispatchDraft = [];
  dispatchRequest = null;

  openModal(`
    <div class="modal-header"><h3>Planificar reparto</h3><button class="modal-close" onclick="closeModal()">✕</button></div>
    <div class="modal-body">
      <p class="text-sm text-muted">Indique desde dónde salen los vehículos y qué hay que entregar. El sistema elige qué vehículo lleva cada
        entrega sin pasarse de su capacidad, en qué orden las visita, y lo compara con repartir sin optimizar. Solo salen los vehículos
        disponibles, sin mantenimiento vencido y sin otro viaje activo.</p>

      <div class="field-row mt-16">
        <div class="field"><label>Punto de salida · latitud</label><input type="number" step="0.0001" id="dispatchOriginLat" value="${SAMPLE_ORIGIN.latitude}" /></div>
        <div class="field"><label>Punto de salida · longitud</label><input type="number" step="0.0001" id="dispatchOriginLng" value="${SAMPLE_ORIGIN.longitude}" /></div>
      </div>

      <div class="field">
        <label>Entregas</label>
        <div id="dispatchList" class="dynamic-list"></div>
        <div class="dispatch-add-row">
          <input type="text" id="dlvDesc" placeholder="Descripción" />
          <input type="number" step="0.1" min="0" id="dlvKg" placeholder="Kg" />
          <input type="number" step="0.01" min="0" id="dlvM3" placeholder="m³" />
          <select id="dlvPriority">
            <option value="Standard">Estándar</option><option value="High">Alta</option>
            <option value="Urgent">Urgente</option><option value="Fragile">Frágil</option>
          </select>
          <input type="number" step="0.0001" id="dlvLat" placeholder="Lat" />
          <input type="number" step="0.0001" id="dlvLng" placeholder="Lng" />
          <button type="button" id="dlvAddBtn" class="btn btn-secondary btn-sm">+ Agregar</button>
        </div>
        <div class="mt-8"><button type="button" id="dlvSampleBtn" class="btn btn-ghost btn-sm">Cargar ejemplo (Santander)</button></div>
      </div>

      <div id="dispatchResult"></div>
    </div>
    <div class="modal-footer">
      <button type="button" class="btn btn-ghost" onclick="closeModal()">Cerrar</button>
      <button type="button" id="dispatchPlanBtn" class="btn btn-secondary">Calcular plan</button>
      <button type="button" id="dispatchApplyBtn" class="btn btn-primary" disabled>Crear rutas del plan</button>
    </div>
  `, { wide: true });
  onModalClose(destroyPlanMap);

  renderDispatchDraft();
  document.getElementById('dlvAddBtn').addEventListener('click', addDelivery);
  document.getElementById('dlvSampleBtn').addEventListener('click', loadSampleDeliveries);
  document.getElementById('dispatchPlanBtn').addEventListener('click', calculateDispatchPlan);
  document.getElementById('dispatchApplyBtn').addEventListener('click', applyDispatchPlan);
  ['dispatchOriginLat', 'dispatchOriginLng'].forEach(id => document.getElementById(id).addEventListener('input', invalidateDispatchPlan));
}

function addDelivery() {
  const description = document.getElementById('dlvDesc').value.trim();
  const weightKg = parseFloat(document.getElementById('dlvKg').value);
  const volumeM3 = parseFloat(document.getElementById('dlvM3').value) || 0;
  const latitude = parseFloat(document.getElementById('dlvLat').value);
  const longitude = parseFloat(document.getElementById('dlvLng').value);
  if (!description || !(weightKg > 0) || Number.isNaN(latitude) || Number.isNaN(longitude)) {
    showToast('Complete descripción, peso (mayor que 0) y coordenadas de la entrega.', 'error');
    return;
  }

  dispatchDraft.push({ description, weightKg, volumeM3, priority: document.getElementById('dlvPriority').value, latitude, longitude });
  ['dlvDesc', 'dlvKg', 'dlvM3', 'dlvLat', 'dlvLng'].forEach(id => { document.getElementById(id).value = ''; });
  invalidateDispatchPlan();
  renderDispatchDraft();
}

function loadSampleDeliveries() {
  document.getElementById('dispatchOriginLat').value = SAMPLE_ORIGIN.latitude;
  document.getElementById('dispatchOriginLng').value = SAMPLE_ORIGIN.longitude;
  dispatchDraft = SAMPLE_DELIVERIES.map(delivery => ({ ...delivery }));
  invalidateDispatchPlan();
  renderDispatchDraft();
}

function renderDispatchDraft() {
  const list = document.getElementById('dispatchList');
  if (!list) return;
  if (dispatchDraft.length === 0) {
    list.innerHTML = '<p class="dynamic-list-empty">Sin entregas agregadas.</p>';
    return;
  }

  list.innerHTML = dispatchDraft.map((d, i) => `
    <div class="dynamic-list-row">
      <span class="row-text">${i + 1}. ${escapeHtml(d.description)} — ${formatNumber(d.weightKg)} kg · ${formatNumber(d.volumeM3, 2)} m³ · ${priorityLabel(d.priority)} (${d.latitude}, ${d.longitude})</span>
      <button type="button" class="dynamic-list-remove" data-index="${i}">✕</button>
    </div>
  `).join('');
  list.querySelectorAll('.dynamic-list-remove').forEach(btn => btn.addEventListener('click', () => {
    dispatchDraft.splice(parseInt(btn.dataset.index, 10), 1);
    invalidateDispatchPlan();
    renderDispatchDraft();
  }));
}

// Lo que se ve debe ser lo que se aplica: si cambian los datos, el plan mostrado deja de valer y se descarta.
function invalidateDispatchPlan() {
  dispatchRequest = null;
  destroyPlanMap();
  const result = document.getElementById('dispatchResult');
  if (result) result.innerHTML = '';
  const apply = document.getElementById('dispatchApplyBtn');
  if (apply) apply.disabled = true;
}

function buildDispatchRequest() {
  return {
    originLat: parseFloat(document.getElementById('dispatchOriginLat').value),
    originLng: parseFloat(document.getElementById('dispatchOriginLng').value),
    deliveries: dispatchDraft.map(d => ({ ...d }))
  };
}

async function calculateDispatchPlan() {
  const request = buildDispatchRequest();
  if (!Number.isFinite(request.originLat) || !Number.isFinite(request.originLng)) {
    showToast('Indique la latitud y la longitud del punto de salida.', 'error');
    return;
  }
  if (request.deliveries.length === 0) {
    showToast('Agregue al menos una entrega.', 'error');
    return;
  }

  const button = document.getElementById('dispatchPlanBtn');
  button.disabled = true;
  button.textContent = 'Calculando…';
  try {
    const plan = await FleetAPI.planDispatch(request);
    dispatchRequest = request;
    renderDispatchPlan(plan);
    document.getElementById('dispatchApplyBtn').disabled = plan.trips.length === 0;
  } catch (err) {
    showToast(err.message, 'error');
  } finally {
    button.disabled = false;
    button.textContent = 'Calcular plan';
  }
}

async function applyDispatchPlan() {
  if (!dispatchRequest) return;
  const request = dispatchRequest;
  const apply = document.getElementById('dispatchApplyBtn');
  apply.disabled = true; // una sola vez: aplicar dos veces crearía las rutas por duplicado
  try {
    const result = await FleetAPI.commandApplyDispatch(request);
    dispatchRequest = null;
    destroyPlanMap();
    document.getElementById('dispatchResult').innerHTML = createdRoutesHtml(result);
    showToast(`${result.createdRoutes.length} ruta(s) planificada(s) a partir del plan.`, 'success');
    await refreshRoutes();
    await refreshCommandHistory();
  } catch (err) {
    apply.disabled = false;
    showToast(err.message, 'error');
  }
}

// ---------------------------------------------------------------------
// Presentación del plan
// ---------------------------------------------------------------------
function renderDispatchPlan(plan) {
  document.getElementById('dispatchResult').innerHTML = `
    ${comparisonHtml(plan)}
    ${plan.trips.length
      ? `<div class="plan-grid">${plan.trips.map(tripCardHtml).join('')}</div>`
      : '<p class="plan-empty">Ningún vehículo pudo llevar entregas.</p>'}
    ${unassignedHtml(plan.unassigned)}
    ${excludedHtml(plan.excludedVehicles)}
    <div id="dispatchMap" class="plan-map"></div>
  `;
  drawPlanMap(plan);
}

function comparisonHtml(plan) {
  const optimized = plan.totals, baseline = plan.baseline, comparison = plan.comparison;
  const row = (label, before, after) => `<tr><td>${label}</td><td>${before}</td><td><strong>${after}</strong></td></tr>`;

  let verdict;
  if (!comparison.comparable) {
    verdict = `<div class="plan-savings plan-savings-warn">${escapeHtml(comparison.note)}</div>`;
  } else if (comparison.kmSaved > 0) {
    verdict = `<div class="plan-savings plan-savings-good">Ahorro: <strong>${formatNumber(comparison.kmSaved, 2)} km</strong> (${formatNumber(comparison.kmSavedPercent)} %) y ${formatMinutes(comparison.minutesSaved)} de conducción.</div>`;
  } else if (comparison.kmSaved < 0) {
    verdict = `<div class="plan-savings plan-savings-warn">Recorre ${formatNumber(-comparison.kmSaved, 2)} km más que el orden simple. ${escapeHtml(comparison.note || '')}</div>`;
  } else {
    verdict = '<div class="plan-savings">Misma distancia que el orden simple.</div>';
  }

  return `
    <div class="plan-compare">
      <table class="data-table plan-compare-table">
        <thead><tr><th></th><th>Sin optimizar</th><th>Optimizado</th></tr></thead>
        <tbody>
          ${row('Entregas asignadas', baseline.assignedCount, optimized.assignedCount)}
          ${row('Vehículos usados', baseline.vehiclesUsed, optimized.vehiclesUsed)}
          ${row('Distancia total', `${formatNumber(baseline.totalKm, 2)} km`, `${formatNumber(optimized.totalKm, 2)} km`)}
          ${row('Tiempo de conducción', formatMinutes(baseline.totalMinutes), formatMinutes(optimized.totalMinutes))}
          ${row('Todo entregado en', formatMinutes(baseline.longestTripMinutes), formatMinutes(optimized.longestTripMinutes))}
        </tbody>
      </table>
      ${verdict}
      <p class="field-hint">«Sin optimizar»: cada entrega va al primer vehículo con espacio y las paradas se visitan en el orden en que se ingresaron.
        Distancias en línea recta; el tiempo usa la velocidad del tipo de cada vehículo.</p>
    </div>`;
}

function tripCardHtml(trip, index) {
  const color = PLAN_COLORS[index % PLAN_COLORS.length];
  const icon = VEHICLE_TYPE_ICONS[trip.vehicleType] || '🚚';
  const typeLabel = VEHICLE_TYPE_LABELS[trip.vehicleType] || trip.vehicleType;
  const volume = trip.capacityM3 != null ? ` · ${formatNumber(trip.loadM3, 2)} de ${formatNumber(trip.capacityM3, 2)} m³` : '';
  const stops = trip.stops.map(stop => `
    <li>${escapeHtml(stop.description)}
      <span class="text-muted">· ${formatNumber(stop.weightKg)} kg${stop.priority !== 'Standard' ? ' · ' + priorityLabel(stop.priority) : ''}</span></li>`).join('');

  return `
    <div class="plan-trip" style="--trip-color:${color}">
      <div class="plan-trip-head"><strong>${icon} ${escapeHtml(trip.licensePlate)}</strong><span class="text-muted">${escapeHtml(typeLabel)}</span></div>
      <div class="plan-load-bar" title="Carga del vehículo"><div class="plan-load-fill" style="width:${Math.min(100, trip.loadPercent)}%"></div></div>
      <div class="text-sm text-muted">${formatNumber(trip.loadKg)} de ${formatNumber(trip.capacityKg)} kg (${formatNumber(trip.loadPercent)} %)${volume}</div>
      <ol class="plan-stops">${stops}</ol>
      <div class="text-sm text-muted">${formatNumber(trip.distanceKm, 2)} km · ${formatMinutes(trip.estimatedMinutes)} a ${formatNumber(trip.speedKmh, 0)} km/h</div>
    </div>`;
}

function unassignedHtml(unassigned) {
  if (unassigned.length === 0) return '';
  return `
    <div class="plan-unassigned">
      <strong>Sin asignar (${unassigned.length})</strong>
      <ul>${unassigned.map(u => `<li><strong>${escapeHtml(u.delivery.description)}</strong> · ${formatNumber(u.delivery.weightKg)} kg — ${escapeHtml(u.reason)}</li>`).join('')}</ul>
    </div>`;
}

function excludedHtml(excluded) {
  if (excluded.length === 0) return '';
  return `
    <details class="plan-excluded">
      <summary>Vehículos que no salen (${excluded.length})</summary>
      <ul>${excluded.map(e => `<li><strong>${escapeHtml(e.licensePlate)}</strong> — ${escapeHtml(e.reason)}</li>`).join('')}</ul>
    </details>`;
}

function createdRoutesHtml(result) {
  const routes = result.createdRoutes.map(route => `
    <li><strong>${escapeHtml(route.name)}</strong> — ${formatNumber(route.estimatedDistanceKm, 2)} km · ${formatMinutes(route.estimatedDurationMinutes)}</li>`).join('');
  const left = result.unassigned.length
    ? `<p class="plan-savings plan-savings-warn">${result.unassigned.length} entrega(s) quedaron sin asignar y no tienen ruta.</p>` : '';
  return `
    <div class="plan-created">
      <strong>Se crearon ${result.createdRoutes.length} ruta(s) planificada(s)</strong>
      <ul>${routes}</ul>
      ${left}
      <p class="field-hint">Quedaron en estado «Planificada»: inícielas desde la tabla de rutas cuando el vehículo salga.</p>
    </div>`;
}

function drawPlanMap(plan) {
  createPlanMap('dispatchMap');
  const depot = [plan.originLat, plan.originLng];
  const points = [depot];
  L.marker(depot, { icon: numberedIcon('S', '#111827') }).addTo(planMap).bindTooltip('Punto de salida');

  plan.trips.forEach((trip, index) => {
    const color = PLAN_COLORS[index % PLAN_COLORS.length];
    const path = [depot, ...trip.stops.map(stop => [stop.latitude, stop.longitude])];
    L.polyline(path, { color, weight: 4, opacity: 0.85 }).addTo(planMap);
    trip.stops.forEach((stop, order) => {
      L.marker([stop.latitude, stop.longitude], { icon: numberedIcon(order + 1, color) })
        .addTo(planMap).bindTooltip(`${trip.licensePlate} · ${order + 1}. ${stop.description}`);
      points.push([stop.latitude, stop.longitude]);
    });
  });

  plan.unassigned.forEach(({ delivery }) => {
    L.marker([delivery.latitude, delivery.longitude], { icon: numberedIcon('✕', CURRENT_COLOR) })
      .addTo(planMap).bindTooltip(`Sin asignar: ${delivery.description}`);
    points.push([delivery.latitude, delivery.longitude]);
  });
  fitPlanMap(points);
}

// ---------------------------------------------------------------------
// Optimizar las paradas de una ruta
// ---------------------------------------------------------------------
async function openRouteOptimizeModal(routeId) {
  let result, route;
  try {
    result = await FleetAPI.getRouteOptimization(routeId);
    route = routesCache.find(r => r.id === routeId) || await FleetAPI.getRoute(routeId);
  } catch (err) {
    showToast(err.message, 'error');
    return;
  }

  const canApply = result.improves && result.status === 'Planned';
  openModal(`
    <div class="modal-header"><h3>Optimizar paradas · ${escapeHtml(result.routeName)}</h3><button class="modal-close" onclick="closeModal()">✕</button></div>
    <div class="modal-body">
      <p class="text-sm text-muted">El origen y el destino no cambian: solo se reordenan las paradas intermedias para recorrer lo menos posible
        (vecino más cercano mejorado con 2-opt; distancias en línea recta).</p>
      ${optimizationVerdictHtml(result)}
      <div class="plan-compare-lists">
        <div><h4>Orden actual</h4>${sequenceHtml(result.current)}</div>
        <div><h4>Orden propuesto</h4>${sequenceHtml(result.proposed)}</div>
      </div>
      <div id="optimizeMap" class="plan-map"></div>
    </div>
    <div class="modal-footer">
      <button type="button" class="btn btn-ghost" onclick="closeModal()">Cerrar</button>
      ${canApply ? '<button type="button" id="optimizeApplyBtn" class="btn btn-primary">Aplicar este orden</button>' : ''}
    </div>
  `, { wide: true });
  onModalClose(destroyPlanMap);
  drawOptimizeMap(route, result);

  if (canApply) {
    document.getElementById('optimizeApplyBtn').addEventListener('click', async (e) => {
      e.target.disabled = true;
      try {
        const applied = await FleetAPI.commandApplyRouteOptimization(routeId);
        closeModal();
        showToast(`Paradas reordenadas: ${formatNumber(applied.distanceSavedKm, 2)} km menos.`, 'success');
        await refreshRoutes();
        await refreshCommandHistory();
      } catch (err) {
        e.target.disabled = false;
        showToast(err.message, 'error');
      }
    });
  }
}

function optimizationVerdictHtml(result) {
  const distances = `${formatNumber(result.current.distanceKm, 2)} km → ${formatNumber(result.proposed.distanceKm, 2)} km`;
  if (!result.improves) {
    return `<div class="plan-savings">Las paradas ya están en el mejor orden que se encontró (${formatNumber(result.current.distanceKm, 2)} km · ${formatMinutes(result.current.durationMinutes)}).</div>`;
  }

  const saving = `<div class="plan-savings plan-savings-good">Ahorro: <strong>${formatNumber(result.distanceSavedKm, 2)} km</strong> (${formatNumber(result.distanceSavedPercent)} %) y ${formatMinutes(result.minutesSaved)} · ${distances}</div>`;
  return result.status === 'Planned'
    ? saving
    : `${saving}<div class="plan-savings plan-savings-warn">La ruta está ${(ROUTE_STATUS_LABELS[result.status] || result.status).toLowerCase()}: solo se puede reordenar una ruta planificada, así que esto es solo una vista previa.</div>`;
}

function sequenceHtml(sequence) {
  if (sequence.waypoints.length === 0) return '<p class="dynamic-list-empty">Sin paradas.</p>';
  return `
    <ol class="plan-stops">${sequence.waypoints.map(w => `<li>${escapeHtml(w.label)}</li>`).join('')}</ol>
    <div class="text-sm text-muted">${formatNumber(sequence.distanceKm, 2)} km · ${formatMinutes(sequence.durationMinutes)}</div>`;
}

function drawOptimizeMap(route, result) {
  createPlanMap('optimizeMap');
  const origin = [route.originLat, route.originLng];
  const destination = [route.destinationLat, route.destinationLng];
  const pathOf = (sequence) => [origin, ...sequence.waypoints.map(w => [w.latitude, w.longitude]), destination];
  const points = [origin, destination, ...result.proposed.waypoints.map(w => [w.latitude, w.longitude])];

  if (result.improves) L.polyline(pathOf(result.current), { color: CURRENT_COLOR, weight: 3, dashArray: '6 8', opacity: 0.9 }).addTo(planMap);
  L.polyline(pathOf(result.proposed), { color: OPTIMIZED_COLOR, weight: 4, opacity: 0.9 }).addTo(planMap);

  result.proposed.waypoints.forEach((w, i) => {
    L.marker([w.latitude, w.longitude], { icon: numberedIcon(i + 1, OPTIMIZED_COLOR) }).addTo(planMap).bindTooltip(`${i + 1}. ${w.label}`);
  });
  // En un recorrido de ida y vuelta (mismo origen y destino) un solo marcador: dos encima se taparían.
  const roundTrip = Math.abs(origin[0] - destination[0]) < 1e-6 && Math.abs(origin[1] - destination[1]) < 1e-6;
  if (roundTrip) {
    L.marker(origin, { icon: numberedIcon('S', '#111827') }).addTo(planMap).bindTooltip('Salida y regreso');
  } else {
    L.marker(origin, { icon: numberedIcon('A', '#111827') }).addTo(planMap).bindTooltip('Origen');
    L.marker(destination, { icon: numberedIcon('B', '#111827') }).addTo(planMap).bindTooltip('Destino');
  }
  fitPlanMap(points);
}
