/**
 * Hoja de ruta (patrón Template Method, backend: Application/RouteSheets). Presentación compartida por el panel administrativo y
 * el portal del operador: ambas páginas cargan este archivo después de api.js y de su propio script, y usan sus utilidades de
 * modal y avisos (openModal, closeModal, showToast), que existen en las dos.
 *
 * Qué muestra y para qué: la API arma SIEMPRE la hoja con el mismo esqueleto (cargar y validar → encabezado → paradas → carga →
 * avance → navegación → ensamblar) y cada audiencia aporta contenido distinto en cada paso. Aquí se dibujan los cinco pasos en el
 * mismo orden para cualquier audiencia; el que una audiencia no aporta se ve atenuado. Así, al cambiar de «Conductor» a
 * «Despachador», lo que cambia es el contenido de los pasos y nunca su orden.
 *
 * Este archivo solo PRESENTA: no calcula nada ni conoce las reglas (capacidad, viaje activo, enlace de navegación): todo viene
 * calculado de GET /api/route-sheets/{id}?audience=... Los estilos se inyectan al usarse por primera vez, como en api.js, para no
 * tocar las hojas de estilo existentes.
 */
const RouteSheetUI = (() => {
  const STYLE_ELEMENT_ID = 'route-sheet-styles';
  const SHEET_BODY_ID = 'routeSheetBody';
  const SHEET_BUTTON_CLASS = 'route-sheet-btn';
  const INVALID_AUDIENCE_EXAMPLE = 'Pilot';
  const FRACTION_DIGITS = 1;

  // Las audiencias que ofrece el selector. Si se registra una plantilla nueva en el backend, aquí es solo una fila más.
  const AUDIENCES = [
    { value: 'Driver', label: 'Conductor', hint: 'Qué recorrer y cómo seguirlo' },
    { value: 'Dispatcher', label: 'Despachador', hint: 'Cómo va la operación' }
  ];

  // Los pasos del esqueleto que varían, en el orden en que la plantilla los ejecuta. `kind` dice cómo los declara la clase base:
  // abstracto (toda audiencia lo decide) o hook (puede no aportar nada y entonces se queda en su valor por defecto).
  const STEPS = [
    { key: 'header', number: 2, title: 'Encabezado', kind: 'abstracto', method: 'BuildHeader' },
    { key: 'stops', number: 3, title: 'Paradas', kind: 'hook', method: 'BuildStops' },
    { key: 'cargo', number: 4, title: 'Carga', kind: 'hook', method: 'BuildCargo' },
    { key: 'progress', number: 5, title: 'Avance', kind: 'abstracto', method: 'BuildProgress' },
    { key: 'navigation', number: 6, title: 'Navegación', kind: 'hook', method: 'BuildNavigationAsync' }
  ];

  const STYLES = `
    .sheet-skeleton { display: flex; flex-wrap: wrap; gap: 4px; align-items: center; font-size: 11px; color: var(--color-text-muted, #8b93a4); }
    .sheet-skeleton span { padding: 2px 8px; border-radius: 10px; background: var(--color-surface-raised, #1f2632); border: 1px solid var(--color-border-soft, #212836); }
    .sheet-skeleton .fixed { color: var(--color-accent, #2dd4bf); border-color: var(--color-accent-soft, rgba(45, 212, 191, 0.14)); }
    .sheet-audience { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; margin: 12px 0; }
    .sheet-audience .btn.active { color: var(--color-accent, #2dd4bf); border-color: var(--color-accent, #2dd4bf); background: var(--color-accent-soft, rgba(45, 212, 191, 0.14)); }
    .sheet-step { margin-top: 10px; padding: 10px 12px; border-radius: var(--radius-sm, 6px); background: var(--color-bg, #10141b); border: 1px solid var(--color-border-soft, #212836); }
    .sheet-step.is-empty { opacity: 0.5; border-style: dashed; }
    .sheet-step-head { display: flex; justify-content: space-between; gap: 8px; align-items: baseline; margin-bottom: 6px; font-size: 12.5px; font-weight: 600; }
    .sheet-step-tag { font-size: 10.5px; font-weight: 500; color: var(--color-text-faint, #5b6577); white-space: nowrap; }
    .sheet-step-empty { font-size: 12px; color: var(--color-text-muted, #8b93a4); }
    .sheet-facts { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 6px 14px; font-size: 12.5px; }
    .sheet-facts dt { font-size: 10.5px; color: var(--color-text-faint, #5b6577); text-transform: uppercase; letter-spacing: 0.04em; }
    .sheet-facts dd { margin: 0 0 2px; }
    .sheet-stops { margin: 0; padding-left: 20px; font-size: 12.5px; }
    .sheet-meter { height: 6px; border-radius: 3px; background: var(--color-border, #2a3241); overflow: hidden; margin: 4px 0; }
    .sheet-meter-fill { height: 100%; background: var(--status-available, #34d399); }
    .sheet-meter-fill.over { background: var(--status-outofservice, #f0546a); }
    .sheet-warning { color: var(--status-outofservice, #f0546a); font-weight: 600; }
    .sheet-error { margin-top: 10px; padding: 10px 12px; border-radius: var(--radius-sm, 6px); background: var(--status-outofservice-soft, rgba(240, 84, 106, 0.14)); color: var(--status-outofservice, #f0546a); font-size: 12.5px; }
  `;

  const escape = (text) => (typeof escapeHtml === 'function' ? escapeHtml(text) : String(text));
  const present = (value) => value !== null && value !== undefined;

  function ensureStyles() {
    if (document.getElementById(STYLE_ELEMENT_ID)) return;
    const style = document.createElement('style');
    style.id = STYLE_ELEMENT_ID;
    style.textContent = STYLES;
    document.head.appendChild(style);
  }

  function number(value, digits = FRACTION_DIGITS) {
    return Number(value).toLocaleString('es-CO', { minimumFractionDigits: 0, maximumFractionDigits: digits });
  }

  // ROUTE_STATUS_LABELS lo define la página que carga este archivo (panel y portal tienen el mismo diccionario).
  function statusLabel(status) {
    return typeof ROUTE_STATUS_LABELS !== 'undefined' && ROUTE_STATUS_LABELS[status] ? ROUTE_STATUS_LABELS[status] : status;
  }

  function fact(label, value) {
    return `<div><dt>${escape(label)}</dt><dd>${value}</dd></div>`;
  }

  // El enlace solo se escribe si es http(s): nunca se pone en un href un texto que venga de fuera sin comprobar su esquema.
  function isSafeLink(url) {
    return typeof url === 'string' && /^https?:\/\//i.test(url);
  }

  // ---------- Contenido de cada paso (cada función devuelve null si la audiencia no aportó nada en ese paso) ----------

  function headerContent(sheet) {
    const header = sheet.header;
    return `<dl class="sheet-facts">
      ${fact('Hoja', escape(header.title))}
      ${fact('Ruta', escape(header.routeName))}
      ${fact('Estado', `<span class="badge badge-${escape(header.status)}">${escape(statusLabel(header.status))}</span>`)}
      ${fact('Vehículo', escape(header.licensePlate))}
      ${present(header.scheduledDate) ? fact('Programada', escape(new Date(header.scheduledDate).toLocaleDateString('es-CO'))) : ''}
      ${present(header.driverName) ? fact('Conductor', escape(header.driverName)) : ''}
    </dl>`;
  }

  function stopsContent(sheet) {
    if (!sheet.stops || sheet.stops.length === 0) return null;
    return `<ol class="sheet-stops">${sheet.stops.map(stop => `<li>${escape(stop.label)}</li>`).join('')}</ol>`;
  }

  function cargoContent(sheet) {
    const cargo = sheet.cargo;
    if (!present(cargo)) return null;
    const used = Math.min(cargo.weightUsagePercent, 100);
    return `
      <dl class="sheet-facts">
        ${fact('Artículos', number(cargo.items, 0))}
        ${fact('Peso', `${number(cargo.totalWeightKg)} de ${number(cargo.capacityKg)} kg`)}
        ${fact('Volumen', present(cargo.capacityM3) ? `${number(cargo.totalVolumeM3)} de ${number(cargo.capacityM3)} m³` : `${number(cargo.totalVolumeM3)} m³ (el vehículo no declara capacidad)`)}
      </dl>
      <div class="sheet-meter"><div class="sheet-meter-fill ${cargo.exceedsCapacity ? 'over' : ''}" style="width:${used}%"></div></div>
      <div class="sheet-step-empty">${number(cargo.weightUsagePercent)} % de la capacidad de peso${cargo.exceedsCapacity ? ' · <span class="sheet-warning">excede la capacidad del vehículo</span>' : ''}</div>`;
  }

  function progressContent(sheet) {
    const progress = sheet.progress;
    if (!present(progress)) return null;
    const facts = [
      present(progress.percent) ? fact('Recorrido', `${number(progress.percent)} %`) : '',
      present(progress.remainingKm) ? fact('Faltan', `${number(progress.remainingKm, 2)} km`) : '',
      present(progress.nextStop) ? fact('Próxima parada', escape(progress.nextStop)) : '',
      present(progress.etaMinutes) ? fact('Llegada estimada', `en ${number(progress.etaMinutes, 0)} min`) : '',
      present(progress.delayMinutes) ? fact('Retraso', progress.delayMinutes > 0 ? `${number(progress.delayMinutes, 0)} min` : 'ninguno') : ''
    ].join('');
    return `<dl class="sheet-facts">${facts}</dl>`;
  }

  function navigationContent(sheet) {
    if (!present(sheet.navigationUrl)) return null;
    return isSafeLink(sheet.navigationUrl)
      ? `<a href="${escape(sheet.navigationUrl)}" target="_blank" rel="noopener">🧭 Abrir la ruta en el navegador externo</a>`
      : '<span class="sheet-step-empty">El enlace recibido no es una dirección web válida.</span>';
  }

  const CONTENT = {
    header: headerContent, stops: stopsContent, cargo: cargoContent, progress: progressContent, navigation: navigationContent
  };

  // Mensaje cuando el paso queda vacío. En el avance, vacío significa «no hay un viaje en curso»; en el resto, «esta audiencia no lo pide».
  const EMPTY_MESSAGE = {
    stops: 'Esta audiencia no aporta este paso: el hook queda en su valor por defecto (sin paradas).',
    cargo: 'Esta audiencia no aporta este paso: el hook queda en su valor por defecto (sin resumen de carga).',
    progress: 'Sin datos de avance: la ruta no tiene un viaje en curso con este vehículo.',
    navigation: 'Esta audiencia no aporta este paso: el hook queda en su valor por defecto (sin enlace de navegación).'
  };

  function renderStep(step, sheet) {
    const content = CONTENT[step.key](sheet);
    const body = content ?? `<div class="sheet-step-empty">${EMPTY_MESSAGE[step.key]}</div>`;
    return `<section class="sheet-step ${content === null ? 'is-empty' : ''}" data-step="${step.key}">
      <div class="sheet-step-head"><span>${step.number}. ${step.title}</span><span class="sheet-step-tag">${step.kind} · ${step.method}</span></div>
      ${body}
    </section>`;
  }

  /** El esqueleto completo, para que se vea qué pasos son fijos de la clase base y cuáles completa cada audiencia. */
  function renderSkeleton() {
    const pieces = [
      '<span class="fixed">1. Cargar y validar (fijo)</span>',
      ...STEPS.map(step => `<span>${step.number}. ${step.title}</span>`),
      '<span class="fixed">7. Ensamblar la hoja (fijo)</span>'
    ];
    return `<div class="sheet-skeleton" title="BuildAsync no es virtual: el orden es el mismo para todas las audiencias">${pieces.join(' → ')}</div>`;
  }

  /** HTML de una hoja ya recibida de la API. Es una función pura: no toca la página, así que se puede probar sola. */
  function renderSheet(sheet) {
    return `${renderSkeleton()}${STEPS.map(step => renderStep(step, sheet)).join('')}`;
  }

  function renderError(message) {
    return `<div class="sheet-error">${escape(message)}</div>`;
  }

  // ---------- Ventana ----------

  function audienceButtons(selected) {
    return AUDIENCES.map(audience =>
      `<button type="button" class="btn btn-ghost btn-sm ${audience.value === selected ? 'active' : ''}" data-audience="${audience.value}" title="${escape(audience.hint)}">${audience.label}</button>`
    ).join('');
  }

  function modalHtml(routeName, selector) {
    return `
      <div class="modal-header"><h3>Hoja de ruta · ${escape(routeName)}</h3><button class="modal-close" onclick="closeModal()">✕</button></div>
      <div class="modal-body">
        ${selector}
        <div id="${SHEET_BODY_ID}"><p class="text-faint text-sm">Cargando…</p></div>
      </div>`;
  }

  async function showSheet(routeId, audience) {
    const target = document.getElementById(SHEET_BODY_ID);
    if (!target) return;
    const ticket = String(Date.now() + Math.random());
    target.dataset.ticket = ticket;   // si el usuario cambia de audiencia mientras se carga, la respuesta vieja se descarta
    try {
      const sheet = await FleetAPI.getRouteSheet(routeId, audience);
      if (target.dataset.ticket === ticket) target.innerHTML = renderSheet(sheet);
    } catch (err) {
      if (target.dataset.ticket === ticket) target.innerHTML = renderError(err.message);
    }
  }

  /** Panel administrativo: la hoja de una ruta con el selector de audiencia (y un ejemplo de audiencia inválida). */
  function openForAdmin(routeId, routeName) {
    ensureStyles();
    const selector = `<div class="sheet-audience" id="sheetAudience">
        ${audienceButtons(AUDIENCES[0].value)}
        <button type="button" class="btn btn-ghost btn-sm" data-audience="${INVALID_AUDIENCE_EXAMPLE}" title="Pide una audiencia que no existe: la API responde 400 con los valores válidos">Probar audiencia inválida</button>
      </div>`;
    openModal(modalHtml(routeName, selector), { medium: true });

    document.getElementById('sheetAudience').addEventListener('click', (event) => {
      const button = event.target.closest('button[data-audience]');
      if (!button) return;
      document.querySelectorAll('#sheetAudience button').forEach(each => each.classList.toggle('active', each === button));
      showSheet(routeId, button.dataset.audience);
    });
    showSheet(routeId, AUDIENCES[0].value);
  }

  /** Portal del operador: solo la hoja del conductor, sin selector (el conductor no elige su audiencia). */
  function openForDriver(routeId, routeName) {
    ensureStyles();
    openModal(modalHtml(routeName, ''), { medium: true });
    showSheet(routeId, 'Driver');
  }

  // ---------- Enganche en cada página ----------

  function routeIdOf(row) {
    const anyButton = row.querySelector('[data-id]');
    return anyButton ? anyButton.dataset.id : null;
  }

  function addAdminButton(row) {
    const actions = row.querySelector('td:last-child .flex');
    const routeId = routeIdOf(row);
    if (!actions || !routeId || actions.querySelector(`.${SHEET_BUTTON_CLASS}`)) return;

    const button = document.createElement('button');
    button.className = `btn btn-ghost btn-sm ${SHEET_BUTTON_CLASS}`;
    button.title = 'Hoja de ruta por audiencia (Template Method)';
    button.textContent = '📄 Hoja de ruta';
    button.addEventListener('click', () => openForAdmin(routeId, row.querySelector('td').textContent.trim()));
    actions.prepend(button);
  }

  // La tabla de rutas se vuelve a dibujar completa cada vez que cambia algo: se observa para poner el botón en cada fila nueva.
  function hookAdminTable(tbody) {
    const decorate = () => tbody.querySelectorAll('tr').forEach(addAdminButton);
    new MutationObserver(decorate).observe(tbody, { childList: true });
    decorate();
  }

  function hookOperatorBar(bar) {
    const button = document.createElement('button');
    button.id = 'openRouteSheetBtn';
    button.className = 'btn btn-secondary';
    button.textContent = '📄 Hoja de ruta';
    button.addEventListener('click', () => {
      if (typeof currentRoute !== 'undefined' && currentRoute) openForDriver(currentRoute.id, currentRoute.name);
    });
    bar.appendChild(button);
  }

  function init() {
    const routesBody = document.getElementById('routesTableBody');
    const actionBar = document.getElementById('actionBar');
    if (routesBody) hookAdminTable(routesBody);
    if (actionBar) hookOperatorBar(actionBar);
  }

  init();

  return { renderSheet, renderError, openForAdmin, openForDriver };
})();
