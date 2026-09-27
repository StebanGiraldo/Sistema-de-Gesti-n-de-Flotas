/**
 * Gestión de sesión en el navegador (sessionStorage). El token se guarda en
 * memoria de sesión, no en localStorage, para que se limpie automáticamente
 * al cerrar la pestaña.
 */

function saveSession(loginResponse) {
  sessionStorage.setItem('fleet_token', loginResponse.token);
  sessionStorage.setItem('fleet_username', loginResponse.username);
  sessionStorage.setItem('fleet_fullname', loginResponse.fullName);
  sessionStorage.setItem('fleet_role', loginResponse.role);
  if (loginResponse.driverId) {
    sessionStorage.setItem('fleet_driverid', loginResponse.driverId);
  } else {
    sessionStorage.removeItem('fleet_driverid');
  }
}

/**
 * Verifica que haya sesión activa y, opcionalmente, que el rol coincida.
 * Si no se cumple, redirige a login.html. Debe llamarse al inicio de cada
 * página protegida (dashboard.html, operator.html).
 */
function requireAuth(expectedRole) {
  const token = sessionStorage.getItem('fleet_token');
  const role = sessionStorage.getItem('fleet_role');

  if (!token) {
    window.location.href = 'login.html';
    return false;
  }
  if (expectedRole && role !== expectedRole) {
    window.location.href = 'login.html';
    return false;
  }
  return true;
}

function logout() {
  FleetAPI.logout().catch(() => { /* si el token ya expiró, no importa */ }).finally(() => {
    sessionStorage.clear();
    window.location.href = 'login.html';
  });
}

function currentUserFullName() {
  return sessionStorage.getItem('fleet_fullname') || sessionStorage.getItem('fleet_username') || 'Usuario';
}
