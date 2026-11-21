// Recorridos por carretera real en el mapa (panel del administrador y portal del operador).
//
// El servidor calcula el avance de cada viaje sobre una poligonal de tramos RECTOS (RoutePath): su porcentaje, sus km y su ETA
// son en línea recta y no cambian. Esta capa es solo de PRESENTACIÓN: pide a un servicio de rutas (OSRM) el trazado por
// carretera de cada tramo (parada → parada), dibuja ese trazado en lugar de la línea recta y coloca el marcador del vehículo
// en el punto de la carretera que corresponde al mismo avance. Si el servicio no responde, todo queda como antes (línea recta).
//
// Límites que conviene tener presentes:
//  - Usa el servidor público de demostración de OSRM (https://router.project-osrm.org): sin garantía de disponibilidad y para
//    uso ligero. En producción se debe apuntar OSRM_URL a un OSRM propio o a otro proveedor con contrato.
//  - Los km y la ETA que muestra el sistema siguen siendo en línea recta; aquí solo se informa, en un aviso del mapa, los km
//    por carretera de ese recorrido.
//  - Requiere internet en el navegador de quien mira el mapa.
const RoadRoutes = (() => {
  const OSRM_URL = 'https://router.project-osrm.org/route/v1/driving';
  const REQUEST_TIMEOUT_MS = 8000;
  const RETRY_AFTER_MS = 60000;   // tras un fallo no se vuelve a pedir ese tramo durante un minuto
  const MAX_PARALLEL_REQUESTS = 3; // el servidor público se comparte: se piden pocos tramos a la vez
  const METERS_PER_KM = 1000;
  const PERCENT_SCALE = 100;

  const legCache = new Map();  // clave del tramo -> Promise<Leg> (solo los que salieron bien)
  const failedAt = new Map();  // clave del tramo -> instante del último fallo
  const waiting = [];
  let running = 0;

  // ---------- Cola con límite de peticiones simultáneas ----------

  function enqueue(task) {
    return new Promise((resolve, reject) => {
      waiting.push({ task, resolve, reject });
      pump();
    });
  }

  function pump() {
    while (running < MAX_PARALLEL_REQUESTS && waiting.length > 0) {
      const { task, resolve, reject } = waiting.shift();
      running++;
      task().then(resolve, reject).finally(() => { running--; pump(); });
    }
  }

  // ---------- Un tramo entre dos puntos ----------

  const pointOf = (stop) => ({ lat: stop.latitude, lng: stop.longitude });
  const keyOf = (a, b) => [a.lat, a.lng, b.lat, b.lng].map(n => n.toFixed(5)).join(',');

  function straightLeg(a, b) {
    const km = L.latLng(a.lat, a.lng).distanceTo(L.latLng(b.lat, b.lng)) / METERS_PER_KM;
    return { points: [[a.lat, a.lng], [b.lat, b.lng]], km, road: false };
  }

  async function requestRoad(a, b) {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);
    try {
      const url = `${OSRM_URL}/${a.lng},${a.lat};${b.lng},${b.lat}?overview=simplified&geometries=geojson`;
      const response = await fetch(url, { signal: controller.signal });
      if (!response.ok) throw new Error(`OSRM respondió ${response.status}`);
      const body = await response.json();
      const route = body.routes && body.routes[0];
      if (!route) throw new Error('OSRM no devolvió ruta');
      return {
        points: route.geometry.coordinates.map(([lng, lat]) => [lat, lng]),
        km: route.distance / METERS_PER_KM,
        road: true
      };
    } finally {
      clearTimeout(timer);
    }
  }

  function recentlyFailed(key) {
    const failed = failedAt.get(key);
    return failed !== undefined && Date.now() - failed < RETRY_AFTER_MS;
  }

  // Quien llega mientras el tramo ya está pedido espera la misma petición; si esa falla, también recibe el tramo recto.
  async function awaitCached(key, a, b) {
    try {
      return await legCache.get(key);
    } catch {
      return straightLeg(a, b);
    }
  }

  async function legBetween(a, b) {
    const key = keyOf(a, b);
    if (legCache.has(key)) return awaitCached(key, a, b);
    if (recentlyFailed(key)) return straightLeg(a, b);

    const pending = enqueue(() => requestRoad(a, b));
    legCache.set(key, pending);
    try {
      return await pending;
    } catch (err) {
      legCache.delete(key);
      failedAt.set(key, Date.now());
      console.warn('Sin ruta por carretera para un tramo; se dibuja en línea recta:', err.message);
      return straightLeg(a, b);
    }
  }

  /** Los tramos por carretera de un viaje: uno por cada par de puntos consecutivos. Nunca rechaza: si falla, ese tramo es recto. */
  function legs(stops) {
    const pairs = stops.slice(1).map((stop, index) => [pointOf(stops[index]), pointOf(stop)]);
    return Promise.all(pairs.map(([a, b]) => legBetween(a, b)));
  }

  // ---------- Geometría ----------

  const lengthKm = (points) => points.slice(1).reduce(
    (sum, point, index) => sum + L.latLng(points[index]).distanceTo(L.latLng(point)) / METERS_PER_KM, 0);

  /** Punto que está a la fracción `fraction` (0..1) del largo de la poligonal. */
  function alongPolyline(points, fraction) {
    const target = lengthKm(points) * fraction;
    let covered = 0;
    for (let i = 1; i < points.length; i++) {
      const segment = L.latLng(points[i - 1]).distanceTo(L.latLng(points[i])) / METERS_PER_KM;
      if (covered + segment >= target && segment > 0) {
        const t = (target - covered) / segment;
        return [points[i - 1][0] + (points[i][0] - points[i - 1][0]) * t, points[i - 1][1] + (points[i][1] - points[i - 1][1]) * t];
      }
      covered += segment;
    }
    return points[points.length - 1];
  }

  /**
   * Dónde está el vehículo sobre la carretera. El servidor mide el avance (`percent`) sobre los tramos rectos; aquí se busca
   * en qué tramo recto va y qué fracción de él ha recorrido, y se toma esa misma fracción del trazado por carretera de ese
   * tramo. Así el vehículo pasa por cada parada a la vez que el servidor dice que llegó a ella.
   */
  function pointAt(tripLegs, stops, percent) {
    if (!tripLegs || tripLegs.length === 0 || tripLegs.length !== stops.length - 1) return null;

    const straightKm = stops.slice(1).map((stop, i) => L.latLng(stops[i].latitude, stops[i].longitude).distanceTo(L.latLng(stop.latitude, stop.longitude)) / METERS_PER_KM);
    const total = straightKm.reduce((a, b) => a + b, 0);
    if (total <= 0) return null;

    const target = Math.min(Math.max(percent, 0), PERCENT_SCALE) / PERCENT_SCALE * total;
    let start = 0;
    for (let i = 0; i < straightKm.length; i++) {
      const isLast = i === straightKm.length - 1;
      if (target <= start + straightKm[i] || isLast) {
        const fraction = straightKm[i] > 0 ? Math.min((target - start) / straightKm[i], 1) : 1;
        return alongPolyline(tripLegs[i].points, fraction);
      }
      start += straightKm[i];
    }
    return null;
  }

  // ---------- Dibujo ----------

  const summary = (tripLegs) => ({
    km: tripLegs.reduce((sum, leg) => sum + leg.km, 0),
    roadLegs: tripLegs.filter(leg => leg.road).length,
    totalLegs: tripLegs.length
  });

  function noticeText(tripLegs) {
    const { km, roadLegs, totalLegs } = summary(tripLegs);
    const kmText = `${km.toFixed(1).replace('.', ',')} km`;
    return roadLegs === totalLegs
      ? `Ruta por carretera · ${kmText}`
      : `Ruta por carretera en ${roadLegs} de ${totalLegs} tramos · ${kmText} (el resto, en línea recta)`;
  }

  /**
   * Cambia, dentro de la capa que armó TripRouteLayer, la línea recta por el trazado por carretera. Conserva el color de la
   * línea (ámbar si la ruta va con retraso). Si ningún tramo salió por carretera no cambia nada.
   */
  function replaceStraightLine(layer, tripLegs) {
    if (!tripLegs.some(leg => leg.road)) return false;

    const straight = [];
    layer.eachLayer(item => { if (item instanceof L.Polyline) straight.push(item); });
    const options = straight.length > 0 ? straight[0].options : { color: '#2dd4bf', weight: 4, opacity: 0.85 };
    straight.forEach(item => layer.removeLayer(item));

    const road = L.polyline(tripLegs.flatMap(leg => leg.points), { color: options.color, weight: options.weight, opacity: options.opacity });
    road.bindTooltip(noticeText(tripLegs), { sticky: true });
    road.addTo(layer);
    road.bringToBack();
    return true;
  }

  return { legs, pointAt, replaceStraightLine, summary };
})();
