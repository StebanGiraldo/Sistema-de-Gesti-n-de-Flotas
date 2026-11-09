/**
 * Deshacer / Rehacer (patrón Command). Esta página no sabe qué hace cada operación: el backend guarda, por usuario, las
 * órdenes que ejecutó (crear una ruta —por el formulario, express o duplicando otra—, aplicar el orden optimizado, aplicar un plan de reparto) y entrega el texto de la
 * siguiente que se puede deshacer o rehacer. Aquí solo se muestran los dos botones y se pide deshacer o rehacer.
 *
 * Se carga después de dashboard.js y dispatch.js: usa showToast y refreshRoutes de dashboard.js.
 */

const undoCommandBtn = document.getElementById('undoCommandBtn');
const redoCommandBtn = document.getElementById('redoCommandBtn');

/** Pone un botón según haya o no una siguiente operación: activo y con su descripción en el texto de ayuda, o apagado. */
function setCommandButton(button, verb, nextEntry, emptyHint) {
  button.disabled = !nextEntry;
  button.title = nextEntry ? `${verb}: ${nextEntry.description}` : emptyHint;
}

/**
 * Muestra el historial en los dos botones. Si no se pudo leer (`readError`), los botones quedan apagados —sin historial no hay
 * qué deshacer— pero el texto de ayuda lo dice, para no confundirlo con "todavía no hay nada que deshacer".
 */
function renderCommandHistory(history, readError) {
  const unreadable = readError
    ? `No se pudo leer el historial (${readError.message}). Recarga la página con Ctrl+F5 y confirma que la API se reinició con la última versión.`
    : null;
  setCommandButton(undoCommandBtn, 'Deshacer', history && history.undoable[0],
    unreadable ?? 'No hay nada que deshacer todavía. Se pueden deshacer: crear una ruta (formulario, express o duplicada), aplicar el orden optimizado y aplicar el plan de reparto.');
  setCommandButton(redoCommandBtn, 'Rehacer', history && history.redoable[0],
    unreadable ?? 'No hay nada que rehacer: aquí aparece lo último que deshagas.');
}

/** Vuelve a leer el historial del usuario. Si no se puede leer, los botones quedan apagados y se deja el motivo en la consola. */
async function refreshCommandHistory() {
  try {
    renderCommandHistory(await FleetAPI.getCommandHistory());
  } catch (err) {
    console.warn('Deshacer/Rehacer: no se pudo leer el historial.', err);
    renderCommandHistory(null, err);
  }
}

async function runCommandStep(request, doneText) {
  undoCommandBtn.disabled = true; // un solo paso a la vez: dos clics seguidos no deben pedir dos operaciones
  redoCommandBtn.disabled = true;
  try {
    const step = await request();
    showToast(`${doneText}: ${step.description}.`, 'success');
    renderCommandHistory(step.history);
  } catch (err) {
    showToast(err.message, 'error');
    await refreshCommandHistory(); // si ya no se podía deshacer, el backend la quitó del historial: los botones deben reflejarlo
  }
  await refreshRoutes();
}

undoCommandBtn.addEventListener('click', () => runCommandStep(FleetAPI.undoCommand, 'Deshecho'));
redoCommandBtn.addEventListener('click', () => runCommandStep(FleetAPI.redoCommand, 'Rehecho'));

refreshCommandHistory();
