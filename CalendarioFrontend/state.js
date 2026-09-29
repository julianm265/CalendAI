// state.js
// Estado de la aplicación. El token de sesión que emite la API, el colaborador
// y el equipo activo se guardan en sessionStorage del navegador, así que la
// sesión desaparece al cerrar la pestaña.

const STORAGE_KEYS = {
  colaborador: 'calendarioBackend.colaborador',
  equipoActivo: 'calendarioBackend.equipoActivo',
  token: 'calendarioBackend.token',
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
  token: leerJson(STORAGE_KEYS.token), // token Bearer emitido por la API | null
  colaborador: leerJson(STORAGE_KEYS.colaborador), // { id, usuario } | null
  equipoActivo: leerJson(STORAGE_KEYS.equipoActivo), // { id, nombreEquipo } | null
  equipoActivoDetalle: null, // equipo completo con colaboradores, refrescado al entrar al calendario
  vista: { año: null, mes: null }, // mes visible actualmente en el calendario (mes: 1-12)
  diaSeleccionado: null, // 'YYYY-MM-DD'
  eventosDelMes: new Map(), // 'YYYY-MM-DD' -> cantidad de eventos (para los puntos en la cuadrícula)
};

export function setColaborador(colaborador) {
  state.colaborador = colaborador;
  escribirJson(STORAGE_KEYS.colaborador, colaborador);
}

export function setToken(token) {
  state.token = token;
  escribirJson(STORAGE_KEYS.token, token);
}

export function setEquipoActivo(equipo) {
  state.equipoActivo = equipo;
  escribirJson(STORAGE_KEYS.equipoActivo, equipo);
}

export function cerrarSesion() {
  state.token = null;
  escribirJson(STORAGE_KEYS.token, null);
  state.colaborador = null;
  state.equipoActivo = null;
  state.equipoActivoDetalle = null;
  escribirJson(STORAGE_KEYS.colaborador, null);
  escribirJson(STORAGE_KEYS.equipoActivo, null);
}
