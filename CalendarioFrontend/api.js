// api.js
// Capa de acceso a datos. Solo se encarga de hablar HTTP/JSON con la Web API
// de CalendarioBackend. No conoce el DOM ni el estado de la aplicación: cada
// función recibe argumentos simples y devuelve una Promise con el JSON ya
// parseado (o lanza ApiError si algo falla).

export const API_BASE_URL = '/api';
const API_FALLBACK_URL = 'http://localhost:5080/api';

/** Error enriquecido con el código de estado HTTP (0 = fallo de red/CORS). */
export class ApiError extends Error {
  constructor(status, message) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

function mensajePorEstado(status) {
  switch (status) {
    case 400:
      return 'La información enviada no es válida. Revisa el formulario e inténtalo de nuevo.';
    case 401:
      return 'Usuario o contraseña incorrectos.';
    case 404:
      return 'No se encontró el recurso solicitado.';
    case 409:
      return 'La operación no se pudo completar porque entra en conflicto con datos existentes.';
    default:
      return 'Ocurrió un error inesperado al comunicarse con la API.';
  }
}

function encabezadoDeSesion() {
  try {
    const colaborador = JSON.parse(sessionStorage.getItem('calendarioBackend.colaborador') || 'null');
    return colaborador?.usuario ? { 'X-Usuario': colaborador.usuario } : {};
  } catch {
    return {};
  }
}

/** Intenta sacar un mensaje legible de un cuerpo de error JSON o de texto plano. */
function mensajeDesdeBody(body, fallback) {
  if (!body) return fallback;
  if (typeof body === 'string') return body.trim() || fallback;

  if (body.errors && typeof body.errors === 'object') {
    const mensajes = Object.values(body.errors).flat().filter(Boolean);
    if (mensajes.length) return mensajes.join(' ');
  }
  return body.title || body.message || body.error || fallback;
}

async function leerCuerpoDeError(response) {
  const texto = await response.text().catch(() => '');
  if (!texto) return null;
  try {
    return JSON.parse(texto);
  } catch {
    return texto;
  }
}

/** Envoltorio central de fetch: arma la URL, maneja errores de red, HTTP y JSON vacío. */
async function apiFetch(path, options = {}) {
  const urls = [API_BASE_URL];
  if (window.location.hostname === 'localhost' && window.location.port !== '5080') {
    urls.push(API_FALLBACK_URL);
  }

  const isFormData = options.body instanceof FormData;
  let response;
  for (const baseUrl of urls) {
    try {
      response = await fetch(`${baseUrl}${path}`, {
        credentials: 'include',
        ...options,
        headers: isFormData
          ? (options.headers || {})
          : { 'Content-Type': 'application/json', ...encabezadoDeSesion(), ...(options.headers || {}) },
      });
      break;
    } catch {
      if (baseUrl === urls[urls.length - 1]) {
        throw new ApiError(
          0,
          `No se pudo conectar con la API en ${API_BASE_URL} ni en ${API_FALLBACK_URL}. Verifica que esté corriendo y que CORS esté habilitado.`,
        );
      }
    }
  }

  if (!response.ok) {
    const body = await leerCuerpoDeError(response);
    throw new ApiError(response.status, mensajeDesdeBody(body, mensajePorEstado(response.status)));
  }

  if (response.status === 204) return null;

  const texto = await response.text().catch(() => '');
  if (!texto) return null;

  try {
    return JSON.parse(texto);
  } catch {
    // La API respondió 2xx con un cuerpo que no es JSON: se ignora en vez de romper la app.
    return null;
  }
}

/* ------------------------------- Equipos ------------------------------- */

export function listarEquipos() {
  return apiFetch('/equipos');
}

export function crearEquipo(nombreEquipo) {
  return apiFetch('/equipos', {
    method: 'POST',
    body: JSON.stringify({ nombreEquipo }),
  });
}

export function obtenerEquipo(id) {
  return apiFetch(`/equipos/${encodeURIComponent(id)}`);
}

export function registrarColaborador(equipoId, usuario, contraseña) {
  return apiFetch(`/equipos/${encodeURIComponent(equipoId)}/colaboradores`, {
    method: 'POST',
    body: JSON.stringify({ usuario, contraseña }),
  });
}

export function invitarUsuario(equipoId, usuario) {
  return apiFetch(`/equipos/${encodeURIComponent(equipoId)}/invitaciones`, {
    method: 'POST',
    body: JSON.stringify({ usuario }),
  });
}

export function listarInvitaciones() {
  return apiFetch('/equipos/invitaciones');
}

export function aceptarInvitacion(invitacionId) {
  return apiFetch(`/equipos/invitaciones/${encodeURIComponent(invitacionId)}/aceptar`, { method: 'POST' });
}

/* ---------------------------- Autenticación ------------------------------ */

export function registrarUsuario(usuario, contraseña) {
  return apiFetch('/autenticacion/registro', {
    method: 'POST',
    body: JSON.stringify({ usuario, contraseña }),
  });
}

export function iniciarSesion(usuario, contraseña) {
  return apiFetch('/autenticacion/login', {
    method: 'POST',
    body: JSON.stringify({ usuario, contraseña }),
  });
}

/* ------------------------------- Eventos -------------------------------- */

export function obtenerEventosDelDia(equipoId, fecha) {
  return apiFetch(`/equipos/${encodeURIComponent(equipoId)}/eventos?fecha=${encodeURIComponent(fecha)}`);
}

export function obtenerEventosDelMes(equipoId, año, mes) {
  return apiFetch(`/equipos/${encodeURIComponent(equipoId)}/eventos/mes?año=${año}&mes=${mes}`);
}

export function crearEvento(equipoId, payload) {
  return apiFetch(`/equipos/${encodeURIComponent(equipoId)}/eventos`, {
    method: 'POST',
    body: JSON.stringify(payload),
  });
}

export function eliminarEvento(equipoId, eventoId, fecha) {
  return apiFetch(
    `/equipos/${encodeURIComponent(equipoId)}/eventos/${encodeURIComponent(eventoId)}?fecha=${encodeURIComponent(fecha)}`,
    { method: 'DELETE' },
  );
}

export function buscarLugares(query) {
  return apiFetch(`/lugares/buscar?query=${encodeURIComponent(query)}`);
}

export function obtenerLugaresPreferidos(equipoId, limite = 8) {
  return apiFetch(`/equipos/${encodeURIComponent(equipoId)}/lugares/preferidos?limite=${limite}`);
}

/**
 * Sube un PDF/Word y devuelve { eventos, formatoFecha, formatoMixto, hayFechasAmbiguas }.
 * formatoFecha: 'auto' (se deduce del documento), 'dmy' (día/mes/año) o 'mdy' (mes/día/año).
 */
export function extraerFechasDeDocumento(equipoId, archivo, formatoFecha = 'auto') {
  const datos = new FormData();
  datos.append('file', archivo);

  return apiFetch(
    `/equipos/${encodeURIComponent(equipoId)}/documentos/fechas?formatoFecha=${encodeURIComponent(formatoFecha)}`,
    {
      method: 'POST',
      body: datos,
      headers: {},
    },
  );
}
