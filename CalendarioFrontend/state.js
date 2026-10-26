// state.js
// Estado de la aplicación. La sesión (token JWT, usuario y calendario activo) se guarda únicamente
// en sessionStorage del navegador: desaparece al cerrar la pestaña.

const STORAGE_KEYS = {
  token: 'calendarioBackend.token',
  colaborador: 'calendarioBackend.colaborador',
  equipoActivo: 'calendarioBackend.equipoActivo',
};

function leerJson(key) {
  try {
    const raw = sessionStorage.getItem(key);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

function escribirJson(key, value) {
  try {
    if (value === null || value === undefined) {
      sessionStorage.removeItem(key);
    } else {
      sessionStorage.setItem(key, JSON.stringify(value));
    }
  } catch {
    // sessionStorage no disponible (modo privado, cuota excedida, etc.):
    // la app sigue funcionando, solo pierde la persistencia entre recargas.
  }
}

export const state = {
  token: leerJson(STORAGE_KEYS.token), // JWT emitido por /autenticacion/login | null
  colaborador: leerJson(STORAGE_KEYS.colaborador), // { id, usuario } | null
  equipoActivo: leerJson(STORAGE_KEYS.equipoActivo), // { id, nombreEquipo, esPersonal, rol } | null
  equipoActivoDetalle: null, // equipo completo con colaboradores, refrescado al entrar al calendario
  vista: { año: null, mes: null }, // mes visible actualmente en el calendario (mes: 1-12)
  diaSeleccionado: null, // 'YYYY-MM-DD'
  eventosDelMes: new Map(), // 'YYYY-MM-DD' -> cantidad de eventos (para los puntos en la cuadrícula)
};

export function obtenerToken() {
  return state.token;
}

export function setToken(token) {
  state.token = token;
  escribirJson(STORAGE_KEYS.token, token);
}

export function setColaborador(colaborador) {
  state.colaborador = colaborador;
  escribirJson(STORAGE_KEYS.colaborador, colaborador);
}

export function setEquipoActivo(equipo) {
  state.equipoActivo = equipo;
  escribirJson(STORAGE_KEYS.equipoActivo, equipo);
}

export function cerrarSesion() {
  state.token = null;
  state.colaborador = null;
  state.equipoActivo = null;
  state.equipoActivoDetalle = null;
  escribirJson(STORAGE_KEYS.token, null);
  escribirJson(STORAGE_KEYS.colaborador, null);
  escribirJson(STORAGE_KEYS.equipoActivo, null);
}
