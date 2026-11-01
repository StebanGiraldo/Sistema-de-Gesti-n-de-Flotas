/**
 * Deshacer / Rehacer (patrón Command). Esta página no sabe qué hace cada operación: el backend guarda, por usuario, las
 * órdenes que ejecutó (crear una ruta, aplicar el orden optimizado, aplicar un plan de reparto) y entrega el texto de la
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

function renderCommandHistory(history) {
  setCommandButton(undoCommandBtn, 'Deshacer', history && history.undoable[0], 'No hay nada que deshacer');
  setCommandButton(redoCommandBtn, 'Rehacer', history && history.redoable[0], 'No hay nada que rehacer');
}

/** Vuelve a leer el historial del usuario. Si no se puede leer, los botones quedan apagados: sin historial no hay qué deshacer. */
async function refreshCommandHistory() {
  try {
    renderCommandHistory(await FleetAPI.getCommandHistory());
  } catch {
    renderCommandHistory(null);
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
