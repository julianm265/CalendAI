// api.js
// Capa de acceso a datos. Solo se encarga de hablar HTTP/JSON con la Web API
// de CalendarioBackend. No conoce el DOM ni el estado de la aplicación: cada
// función recibe argumentos simples y devuelve una Promise con el JSON ya
// parseado (o lanza ApiError si algo falla).

export const API_BASE_URL = '/api';

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
  let response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      ...options,
      headers: { 'Content-Type': 'application/json', ...(options.headers || {}) },
    });
  } catch {
    throw new ApiError(
      0,
      `No se pudo conectar con la API en ${API_BASE_URL}. Verifica que esté corriendo y que CORS esté habilitado.`,
    );
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

/* ---------------------------- Autenticación ------------------------------ */

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
