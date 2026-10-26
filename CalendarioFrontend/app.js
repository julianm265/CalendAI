// app.js
// Controlador principal: conecta api.js, state.js, calendar.js y ui.js;
// maneja los formularios y renderiza las tres pantallas de la app
// (inicio de sesión, equipos, calendario).

import * as api from './api.js';
import { ApiError } from './api.js';
import {
  normalizarEquipo, normalizarEvento, normalizarEventoDelMes, normalizarColaborador, normalizarInvitacion,
} from './normalize.js';
import { state, setToken, setColaborador, setEquipoActivo, cerrarSesion } from './state.js';
import {
  construirCuadriculaMes, sumarMeses, formatearFechaLarga, formatearMesAño,
  formatearHoraCorta, normalizarHoraParaApi, fechaHoyIso,
} from './calendar.js';
import {
  qs, ce, toast, setFieldError, clearFieldErrors, setFormError,
  setStatus, setButtonLoading, openDrawer, closeDrawer, confirmar,
} from './ui.js';

/* ============================== Navegación entre pantallas ============================== */

function cambiarRuta(ruta) {
  const hash = `#/${ruta}`;
  if (window.location.hash !== hash) {
    window.history.pushState(null, '', hash);
  }
}

function mostrarLogin({ actualizarUrl = true } = {}) {
  if (!qs('#screen-login')) {
    window.location.assign('/index.html');
    return;
  }
  qs('#screen-login').hidden = false;
  if (qs('#screen-app')) qs('#screen-app').hidden = true;
  if (actualizarUrl) cambiarRuta('login');
}

function mostrarApp(vista) {
  if (qs('#screen-login')) qs('#screen-login').hidden = true;
  if (qs('#screen-app')) qs('#screen-app').hidden = false;
  mostrarVista(vista);
  cambiarRuta(vista);
}

function mostrarVista(vista) {
  if (qs('#view-teams')) qs('#view-teams').hidden = vista !== 'teams';
  if (qs('#view-calendar')) qs('#view-calendar').hidden = vista !== 'calendar';
  renderTopbar(vista);
}

function renderTopbar() {
  const contenedor = qs('#topbar-actions');
  contenedor.innerHTML = '';

  if (state.equipoActivo && qs('#view-calendar')) {
    const activo = state.equipoActivo;
    const tipo = activo.esPersonal ? 'Personal' : (activo.rol === 'lider' ? 'Equipo · líder' : 'Equipo');
    contenedor.appendChild(ce('span', { class: 'topbar-chip' }, `${tipo}: ${activo.nombreEquipo}`));

    const pendientes = state.invitacionesPendientes || 0;
    contenedor.appendChild(
      ce('button', { class: 'btn btn-secondary', type: 'button', onClick: abrirMisCalendarios }, [
        'Mis calendarios',
        pendientes > 0 ? ce('span', { class: 'topbar-badge', 'aria-label': `${pendientes} invitaciones pendientes` }, String(pendientes)) : null,
      ]),
    );
    if (!activo.esPersonal) {
      contenedor.appendChild(
        ce('button', { class: 'btn btn-secondary', type: 'button', onClick: abrirColaboradores }, 'Integrantes'),
      );
    }
    contenedor.appendChild(
      ce('button', { class: 'btn btn-secondary', type: 'button', onClick: abrirAsistente }, '✨ Asistente'),
    );
  }

  if (state.colaborador) {
    contenedor.appendChild(ce('span', { class: 'topbar-chip' }, state.colaborador.usuario));
    contenedor.appendChild(
      ce('button', { class: 'btn btn-ghost', type: 'button', onClick: manejarCerrarSesion }, 'Cerrar sesión'),
    );
  } else {
    contenedor.appendChild(
      ce('button', { class: 'btn btn-ghost', type: 'button', onClick: mostrarLogin }, 'Iniciar sesión'),
    );
  }
}

function manejarCerrarSesion() {
  cerrarSesion();
  toast('Sesión cerrada.');
  mostrarLogin();
}

/* ==================================== LOGIN & REGISTRO ==================================== */

let isRegistering = false;

// Función para alternar visualmente entre Iniciar Sesión y Registrarse
function cambiarModoAuth(e) {
  if (e) e.preventDefault();
  isRegistering = !isRegistering;
  
  clearFieldErrors(['login-usuario', 'login-password']);
  setFormError('login-form-error', '');
  qs('#form-login').reset();

  const title = qs('#auth-title');
  const subtitle = qs('#auth-subtitle');
  const submitBtn = qs('#login-submit');
  const teamFieldWrapper = qs('#login-team-wrapper');
  const optionsBlock = qs('#login-options');
  const switchText = qs('#auth-switch-text');
  const switchBtn = qs('#toggle-auth-mode');

  if (isRegistering) {
    title.textContent = 'Crea tu cuenta';
    subtitle.textContent = 'Regístrate para empezar a organizar';
    submitBtn.textContent = 'Registrarse';
    if (teamFieldWrapper) teamFieldWrapper.style.display = 'block';
    if (optionsBlock) optionsBlock.style.display = 'none';
    switchText.textContent = '¿Ya tienes cuenta? ';
    switchBtn.textContent = 'Inicia sesión';
  } else {
    title.textContent = 'Bienvenido de vuelta';
    subtitle.textContent = 'Ingresa a tu cuenta para continuar';
    submitBtn.textContent = 'Iniciar sesión';
    if (teamFieldWrapper) teamFieldWrapper.style.display = 'none';
    if (optionsBlock) optionsBlock.style.display = 'flex';
    switchText.textContent = '¿No tienes usuario todavía? ';
    switchBtn.textContent = 'Regístrate';
  }
}

async function manejarSubmitLogin(event) {
  event.preventDefault();
  clearFieldErrors(['login-usuario', 'login-password']);
  setFormError('login-form-error', '');

  const usuario = qs('#login-usuario').value.trim();
  const contraseña = qs('#login-password').value;
  let valido = true;
  if (!usuario) { setFieldError('login-usuario', 'Escribe tu usuario.'); valido = false; }
  if (!contraseña) { setFieldError('login-password', 'Escribe tu contraseña.'); valido = false; }
  if (!valido) return;

  const boton = qs('#login-submit');
  setButtonLoading(boton, true, isRegistering ? 'Registrando…' : 'Iniciando sesión…');

  try {
    if (isRegistering) {
      // La cuenta se crea sin equipo; este se puede elegir o crear después.
      await api.registrarUsuario(usuario, contraseña);
      toast(`Cuenta "${usuario}" creada con éxito.`, 'success');
    }

    // 2. Ejecutar login normal
    const respuesta = await api.iniciarSesion(usuario, contraseña);
    const colaborador = normalizarColaborador(respuesta?.colaborador ?? respuesta);

    if (!colaborador || !colaborador.usuario) {
      setFormError('login-form-error', 'Usuario o contraseña incorrectos.');
      return;
    }

    const token = respuesta?.token ?? respuesta?.Token ?? null;
    if (!token) {
      // La API respondió el login pero sin token: es una versión anterior a la autenticación con JWT.
      setFormError(
        'login-form-error',
        'La API en ejecución es una versión anterior y no emite sesiones. Reconstruye el backend con "docker compose up -d --build backend" (o reinicia dotnet run) e inténtalo de nuevo.',
      );
      return;
    }

    setToken(token);
    setColaborador(colaborador);
    qs('#form-login').reset();
    toast(`Bienvenido, ${colaborador.usuario}.`, 'success');
    // Al iniciar sesión se abre el calendario personal; desde "Mis calendarios" se cambia a un equipo.
    const personal = normalizarEquipo(respuesta?.equipo ?? respuesta?.Equipo ?? null);
    setEquipoActivo(personal ? datosDeEquipoActivo(personal) : null);

    // El calendario se sirve como una página HTML independiente del login.
    window.location.assign('/calendar.html');
  } catch (error) {
    qs('#login-password').value = '';
    setFormError('login-form-error', mensajeDeError(error));
  } finally {
    setButtonLoading(boton, false);
  }
}

/* ============================== CALENDARIOS, EQUIPOS E INVITACIONES ============================== */

/** Lo mínimo que se guarda del calendario activo (sessionStorage). */
function datosDeEquipoActivo(equipo) {
  return {
    id: equipo.id,
    nombreEquipo: equipo.nombreEquipo,
    esPersonal: Boolean(equipo.esPersonal),
    rol: equipo.rol ?? null,
  };
}

async function abrirMisCalendarios() {
  qs('#form-crear-equipo').reset();
  setFieldError('nuevo-equipo-nombre', '');
  openDrawer('teams-drawer', 'teams-drawer-backdrop');
  await Promise.all([cargarEquipos(), cargarInvitaciones()]);
}

function cerrarMisCalendarios() {
  closeDrawer('teams-drawer', 'teams-drawer-backdrop');
}

async function cargarEquipos() {
  setStatus('teams-status', { loading: 'Cargando calendarios…' });
  qs('#teams-list').innerHTML = '';

  try {
    const bruto = await api.listarEquipos();
    const equipos = (Array.isArray(bruto) ? bruto : []).map(normalizarEquipo);
    setStatus('teams-status');
    renderListaEquipos(equipos);
  } catch (error) {
    setStatus('teams-status', { error: mensajeDeError(error) });
  }
}

function renderListaEquipos(equipos) {
  const lista = qs('#teams-list');
  lista.innerHTML = '';

  for (const equipo of equipos) {
    const esActivo = equipo.id === state.equipoActivo?.id;
    const cantidad = equipo.cantidadColaboradores;
    const meta = equipo.esPersonal
      ? 'Calendario personal'
      : `${cantidad} integrante${cantidad === 1 ? '' : 's'}`;

    lista.appendChild(
      ce('li', { class: `ledger-row${esActivo ? ' is-active' : ''}` }, [
        ce('div', { class: 'ledger-row-main' }, [
          ce('span', { class: 'ledger-row-title' }, equipo.nombreEquipo),
          ce('span', { class: 'ledger-row-meta' }, [
            meta,
            equipo.esPersonal ? null : ' · ',
            equipo.esPersonal ? null : ce('span', { class: `badge-rol badge-rol-${equipo.rol}` }, equipo.rol === 'lider' ? 'Líder' : 'Miembro'),
          ]),
        ]),
        ce('div', { class: 'ledger-row-actions' }, [
          ce('button', {
            class: esActivo ? 'btn btn-ghost' : 'btn btn-primary',
            type: 'button',
            disabled: esActivo ? 'disabled' : null,
            onClick: () => cambiarDeCalendario(equipo),
          }, esActivo ? 'Abierto' : 'Abrir'),
        ]),
      ]),
    );
  }
}

async function cambiarDeCalendario(equipo) {
  cerrarMisCalendarios();
  await irACalendario(datosDeEquipoActivo(equipo));
}

async function manejarSubmitCrearEquipo(event) {
  event.preventDefault();
  setFieldError('nuevo-equipo-nombre', '');

  const input = qs('#nuevo-equipo-nombre');
  const nombre = input.value.trim();
  if (nombre.length < 2) {
    setFieldError('nuevo-equipo-nombre', 'El nombre debe tener al menos 2 caracteres.');
    return;
  }

  const boton = qs('#crear-equipo-submit');
  setButtonLoading(boton, true, 'Creando…');

  try {
    const creado = normalizarEquipo(await api.crearEquipo(nombre));
    input.value = '';
    toast(`Equipo "${creado.nombreEquipo}" creado. Eres su líder: ya puedes invitar a otros usuarios.`, 'success');
    await cambiarDeCalendario({ ...creado, rol: 'lider' });
  } catch (error) {
    setFieldError('nuevo-equipo-nombre', mensajeDeError(error));
  } finally {
    setButtonLoading(boton, false);
  }
}

/** Si el calendario activo desapareció (o ya no se pertenece a él), se vuelve al personal. */
async function volverAlCalendarioPersonal() {
  try {
    const equipos = (await api.listarEquipos() ?? []).map(normalizarEquipo);
    const personal = equipos.find((e) => e.esPersonal) ?? equipos[0];
    if (personal) {
      await irACalendario(datosDeEquipoActivo(personal));
      return;
    }
  } catch {
    // se cae al inicio de sesión
  }
  cerrarSesion();
  window.location.replace('/index.html');
}

/* ------------------------------ Invitaciones recibidas ------------------------------ */

async function cargarInvitaciones() {
  try {
    const bruto = await api.listarInvitacionesRecibidas();
    const invitaciones = (Array.isArray(bruto) ? bruto : []).map(normalizarInvitacion);
    state.invitacionesPendientes = invitaciones.length;
    renderInvitaciones(invitaciones);
    renderTopbar();
  } catch {
    // Las invitaciones son un extra: si falla la consulta no se bloquea el resto de la pantalla.
  }
}

function renderInvitaciones(invitaciones) {
  const seccion = qs('#invitations-section');
  const lista = qs('#invitations-list');
  if (!seccion || !lista) return;
  lista.innerHTML = '';
  seccion.hidden = invitaciones.length === 0;

  for (const invitacion of invitaciones) {
    lista.appendChild(
      ce('li', { class: 'ledger-row' }, [
        ce('div', { class: 'ledger-row-main' }, [
          ce('span', { class: 'ledger-row-title' }, invitacion.equipo),
          ce('span', { class: 'ledger-row-meta' }, `Te invitó ${invitacion.invitadoPor}`),
        ]),
        ce('div', { class: 'ledger-row-actions' }, [
          ce('button', {
            class: 'btn btn-primary', type: 'button',
            onClick: () => responderInvitacion(invitacion, true),
          }, 'Aceptar'),
          ce('button', {
            class: 'btn btn-ghost', type: 'button',
            onClick: () => responderInvitacion(invitacion, false),
          }, 'Rechazar'),
        ]),
      ]),
    );
  }
}

async function responderInvitacion(invitacion, aceptar) {
  try {
    await api.responderInvitacion(invitacion.id, aceptar);
    toast(aceptar ? `Te uniste a "${invitacion.equipo}".` : 'Invitación rechazada.', aceptar ? 'success' : 'info');
    await Promise.all([cargarEquipos(), cargarInvitaciones()]);
  } catch (error) {
    toast(mensajeDeError(error), 'error');
    await cargarInvitaciones(); // pudo haber sido cancelada: se refresca la lista
  }
}

/* ================================== CALENDARIO ================================== */

async function irACalendario(equipo) {
  setEquipoActivo(equipo);
  mostrarApp('calendar');
  reiniciarAsistente();
  reiniciarImportacion();

  const hoy = new Date();
  state.vista = { año: hoy.getFullYear(), mes: hoy.getMonth() + 1 };
  state.diaSeleccionado = null;

  cargarInvitaciones(); // actualiza la insignia de invitaciones pendientes sin bloquear la carga
  const existe = await refrescarDetalleEquipo();
  if (existe) await cargarMes();
}

/** Devuelve false si el calendario activo ya no es accesible (y se redirige al personal). */
async function refrescarDetalleEquipo() {
  try {
    const detalle = normalizarEquipo(await api.obtenerEquipo(state.equipoActivo.id));
    state.equipoActivoDetalle = detalle;
    setEquipoActivo(datosDeEquipoActivo({
      ...detalle,
      id: detalle.id ?? state.equipoActivo.id,
      nombreEquipo: detalle.nombreEquipo || state.equipoActivo.nombreEquipo,
    }));
    renderTopbar();
    return true;
  } catch (error) {
    if (error instanceof ApiError && (error.status === 404 || error.status === 403)) {
      toast('Ese calendario ya no está disponible. Te llevamos a tu calendario personal.', 'error');
      await volverAlCalendarioPersonal();
      return false;
    }
    toast(mensajeDeError(error), 'error');
    return true;
  }
}

async function cargarMes() {
  const { año, mes } = state.vista;
  qs('#month-label').textContent = formatearMesAño(año, mes);
  setStatus('calendar-status', { loading: 'Cargando eventos del mes…' });

  try {
    const bruto = await api.obtenerEventosDelMes(state.equipoActivo.id, año, mes);
    const items = (Array.isArray(bruto) ? bruto : []).map(normalizarEventoDelMes);

    state.eventosDelMes = new Map();
    for (const { fecha } of items) {
      if (!fecha) continue;
      state.eventosDelMes.set(fecha, (state.eventosDelMes.get(fecha) || 0) + 1);
    }

    setStatus('calendar-status');
  } catch (error) {
    state.eventosDelMes = new Map();
    setStatus('calendar-status', { error: mensajeDeError(error) });
  }

  renderCuadricula();

  const hoyIso = fechaHoyIso();
  const [añoHoy, mesHoy] = hoyIso.split('-').map(Number);
  const diaPorDefecto = añoHoy === año && mesHoy === mes ? hoyIso : `${año}-${String(mes).padStart(2, '0')}-01`;
  await seleccionarDia(diaPorDefecto);
}

function renderCuadricula() {
  const { año, mes } = state.vista;
  const contenedor = qs('#calendar-grid');
  contenedor.innerHTML = '';

  const hoyIso = fechaHoyIso();
  const semanas = construirCuadriculaMes(año, mes);

  for (const semana of semanas) {
    for (const celda of semana) {
      const cantidadEventos = state.eventosDelMes.get(celda.iso) || 0;
      const clases = ['day-cell'];
      if (!celda.enMes) clases.push('is-outside');
      if (celda.iso === hoyIso) clases.push('is-today');
      if (celda.iso === state.diaSeleccionado) clases.push('is-selected');

      const hijos = [ce('span', { class: 'day-number' }, String(celda.dia))];
      if (cantidadEventos > 0) {
        hijos.push(
          ce('span', { class: 'event-dots' }, [
            ce('span', { class: 'dot' }),
            `${cantidadEventos} evento${cantidadEventos === 1 ? '' : 's'}`,
          ]),
        );
      }

      const boton = ce('button', {
        type: 'button',
        class: clases.join(' '),
        'aria-pressed': celda.iso === state.diaSeleccionado ? 'true' : 'false',
        'aria-label': celda.iso,
        onClick: () => manejarClicDia(celda),
      }, hijos);

      contenedor.appendChild(boton);
    }
  }
}

async function manejarClicDia(celda) {
  if (!celda.enMes) {
    const [añoCelda, mesCelda] = celda.iso.split('-').map(Number);
    state.vista = { año: añoCelda, mes: mesCelda };
    state.diaSeleccionado = celda.iso;
    await cargarMes();
    return;
  }
  await seleccionarDia(celda.iso);
}

async function seleccionarDia(iso) {
  state.diaSeleccionado = iso;
  renderCuadricula();

  qs('#day-panel-title').textContent = formatearFechaLarga(iso);
  qs('#open-new-event-day').hidden = false;
  setStatus('day-panel-status', { loading: 'Cargando eventos…' });
  qs('#day-events-list').innerHTML = '';

  try {
    const bruto = await api.obtenerEventosDelDia(state.equipoActivo.id, iso);
    const eventos = (Array.isArray(bruto) ? bruto : []).map(normalizarEvento).sort((a, b) => a.hora.localeCompare(b.hora));
    setStatus('day-panel-status');
    renderEventosDelDia(eventos, iso);
  } catch (error) {
    setStatus('day-panel-status', { error: mensajeDeError(error) });
  }
}

function nombreColaborador(id) {
  if (!id) return null;
  const colaborador = state.equipoActivoDetalle?.colaboradores.find((c) => c.id === id);
  return colaborador ? colaborador.usuario : null;
}

/** Pueden eliminar un evento el líder del calendario y quien lo organizó. */
function puedeEliminarEvento(evento) {
  return state.equipoActivo?.rol === 'lider'
    || (evento.colaboradorOrganizadorId && evento.colaboradorOrganizadorId === state.colaborador?.id);
}

function renderEventosDelDia(eventos, fechaIso) {
  const lista = qs('#day-events-list');
  lista.innerHTML = '';

  if (eventos.length === 0) {
    setStatus('day-panel-status', { empty: 'No hay eventos este día. Agrega el primero.' });
    return;
  }

  for (const evento of eventos) {
    const organizador = nombreColaborador(evento.colaboradorOrganizadorId);
    const metaPartes = [evento.lugar, organizador].filter(Boolean);

    const fila = ce('li', { class: 'event-row' }, [
      ce('span', { class: 'event-time' }, formatearHoraCorta(evento.hora)),
      ce('div', { class: 'event-body' }, [
        ce('span', { class: 'event-name' }, evento.nombreEvento),
        metaPartes.length ? ce('span', { class: 'event-meta' }, metaPartes.join(' · ')) : null,
        evento.descripcion ? ce('span', { class: 'event-meta' }, evento.descripcion) : null,
      ]),
      puedeEliminarEvento(evento)
        ? ce('button', {
          class: 'event-delete', type: 'button', 'aria-label': `Eliminar ${evento.nombreEvento}`,
          onClick: () => manejarEliminarEvento(evento, fechaIso),
        }, '🗑')
        : null,
    ]);
    lista.appendChild(fila);
  }
}

async function manejarEliminarEvento(evento, fechaIso) {
  const confirmado = await confirmar({
    title: '¿Eliminar evento?',
    message: `"${evento.nombreEvento}" a las ${formatearHoraCorta(evento.hora)} se eliminará. Esta acción no se puede deshacer.`,
    confirmLabel: 'Eliminar evento',
  });
  if (!confirmado) return;

  try {
    await api.eliminarEvento(state.equipoActivo.id, evento.id);
    toast('Evento eliminado.', 'success');
    await seleccionarDia(fechaIso);
    await recalcularPuntosDelMes();
  } catch (error) {
    toast(mensajeDeError(error), 'error');
  }
}

async function recalcularPuntosDelMes() {
  const { año, mes } = state.vista;
  try {
    const bruto = await api.obtenerEventosDelMes(state.equipoActivo.id, año, mes);
    const items = (Array.isArray(bruto) ? bruto : []).map(normalizarEventoDelMes);
    state.eventosDelMes = new Map();
    for (const { fecha } of items) {
      if (!fecha) continue;
      state.eventosDelMes.set(fecha, (state.eventosDelMes.get(fecha) || 0) + 1);
    }
  } catch {
    // si esto falla, la cuadrícula simplemente conserva los puntos anteriores
  }
  renderCuadricula();
}

async function manejarMesAnterior() {
  state.vista = sumarMeses(state.vista.año, state.vista.mes, -1);
  state.diaSeleccionado = null;
  await cargarMes();
}

async function manejarMesSiguiente() {
  state.vista = sumarMeses(state.vista.año, state.vista.mes, 1);
  state.diaSeleccionado = null;
  await cargarMes();
}

/* ============================== DRAWER: nuevo evento ============================== */

let temporizadorBusquedaLugar = null;
let numeroSolicitudLugar = 0;

function ocultarSugerenciasLugares() {
  const lista = qs('#evento-lugar-sugerencias');
  if (!lista) return;
  lista.hidden = true;
  lista.innerHTML = '';
  qs('#evento-lugar')?.setAttribute('aria-expanded', 'false');
}

function seleccionarLugar(lugar) {
  qs('#evento-lugar').value = lugar;
  ocultarSugerenciasLugares();
}

function renderSugerenciasLugares(lugares, mensaje = '') {
  const lista = qs('#evento-lugar-sugerencias');
  if (!lista) return;
  lista.innerHTML = '';
  for (const item of Array.isArray(lugares) ? lugares : []) {
    const nombre = item.nombre || item.lugar || '';
    const direccion = item.direccion || (item.vecesUsado ? `Usado ${item.vecesUsado} ${item.vecesUsado === 1 ? 'vez' : 'veces'}` : '');
    if (!nombre) continue;
    lista.appendChild(ce('li', { class: 'place-suggestion', role: 'option' }, [
      ce('button', { type: 'button', onClick: () => seleccionarLugar(item.direccion ? `${nombre}, ${direccion}` : nombre) }, [
        ce('span', { class: 'place-suggestion-name' }, nombre),
        ce('span', { class: 'place-suggestion-meta' }, direccion),
      ]),
    ]));
  }
  if (lista.children.length === 0 && mensaje) {
    lista.appendChild(ce('li', { class: 'place-suggestion-message' }, mensaje));
  }
  lista.hidden = lista.children.length === 0;
  qs('#evento-lugar')?.setAttribute('aria-expanded', String(!lista.hidden));
}

async function cargarSugerenciasLugares(consulta = '') {
  const solicitud = ++numeroSolicitudLugar;
  try {
    const lugares = consulta.trim().length >= 2
      ? await api.buscarLugares(consulta.trim())
      : await api.obtenerLugaresPreferidos(state.equipoActivo.id);
    if (solicitud !== numeroSolicitudLugar) return;
    renderSugerenciasLugares(
      lugares,
      consulta.trim() ? 'No encontramos resultados. Revisa el texto e inténtalo de nuevo.' : 'Todavía no hay lugares frecuentes. Escribe un lugar para buscarlo.',
    );
  } catch (error) {
    if (solicitud === numeroSolicitudLugar) {
      renderSugerenciasLugares([], mensajeDeError(error));
    }
  }
}

function manejarEntradaLugar(event) {
  clearTimeout(temporizadorBusquedaLugar);
  temporizadorBusquedaLugar = setTimeout(() => cargarSugerenciasLugares(event.target.value), 250);
}

function poblarSelectOrganizador() {
  const select = qs('#evento-organizador');
  select.innerHTML = '';
  select.appendChild(ce('option', { value: '' }, 'Yo (por defecto)'));
  const colaboradores = state.equipoActivoDetalle?.colaboradores || [];
  for (const colaborador of colaboradores) {
    if (colaborador.id === state.colaborador?.id) continue;
    select.appendChild(ce('option', { value: colaborador.id }, colaborador.usuario));
  }
}

function abrirDrawerEvento() {
  poblarSelectOrganizador();
  clearFieldErrors(['evento-fecha', 'evento-nombre', 'evento-hora', 'evento-lugar']);
  setFormError('evento-form-error', '');

  const form = qs('#form-evento');
  form.reset();
  qs('#evento-fecha').value = state.diaSeleccionado || fechaHoyIso();

  openDrawer('event-drawer', 'event-drawer-backdrop');
  cargarSugerenciasLugares();
}

function cerrarDrawerEvento() {
  closeDrawer('event-drawer', 'event-drawer-backdrop');
  ocultarSugerenciasLugares();
}

async function manejarSubmitEvento(event) {
  event.preventDefault();
  clearFieldErrors(['evento-fecha', 'evento-nombre', 'evento-hora', 'evento-lugar']);
  setFormError('evento-form-error', '');

  const fecha = qs('#evento-fecha').value;
  const nombreEvento = qs('#evento-nombre').value.trim();
  const hora = qs('#evento-hora').value;
  const lugar = qs('#evento-lugar').value.trim();
  const descripcion = qs('#evento-descripcion').value.trim();
  const colaboradorOrganizadorId = qs('#evento-organizador').value || null;

  let valido = true;
  if (!fecha) { setFieldError('evento-fecha', 'Selecciona una fecha.'); valido = false; }
  if (!nombreEvento) { setFieldError('evento-nombre', 'Escribe un nombre para el evento.'); valido = false; }
  if (!hora) { setFieldError('evento-hora', 'Selecciona una hora.'); valido = false; }
  if (!lugar) { setFieldError('evento-lugar', 'Escribe un lugar.'); valido = false; }
  if (!valido) return;

  const boton = qs('#submit-event');
  setButtonLoading(boton, true, 'Guardando…');

  try {
    await api.crearEvento(state.equipoActivo.id, {
      fecha,
      nombreEvento,
      hora: normalizarHoraParaApi(hora),
      lugar,
      descripcion: descripcion || null,
      colaboradorOrganizadorId,
    });

    toast('Evento creado.', 'success');
    cerrarDrawerEvento();

    const mismoMes = fecha.startsWith(`${state.vista.año}-${String(state.vista.mes).padStart(2, '0')}`);
    if (mismoMes) {
      await recalcularPuntosDelMes();
      if (fecha === state.diaSeleccionado) await seleccionarDia(fecha);
    } else {
      const [añoNuevo, mesNuevo] = fecha.split('-').map(Number);
      state.vista = { año: añoNuevo, mes: mesNuevo };
      state.diaSeleccionado = fecha;
      await cargarMes();
    }

  } catch (error) {
    setFormError('evento-form-error', mensajeDeError(error));
  } finally {
    setButtonLoading(boton, false);
  }
}

function reiniciarImportacion() {
  if (qs('#document-results')) qs('#document-results').hidden = true;
  if (qs('#document-date-list')) qs('#document-date-list').innerHTML = '';
  if (qs('#document-status')) setStatus('document-status');
}

async function manejarSubmitDocumento(event) {
  event.preventDefault();
  const input = qs('#document-file');
  const archivo = input.files?.[0];
  if (!archivo) {
    setStatus('document-status', { error: 'Selecciona un archivo PDF o Word.' });
    return;
  }

  const formato = qs('#document-date-format')?.value || 'auto';
  const usarIA = qs('#document-use-ai')?.checked ?? true;

  setStatus('document-status', { loading: usarIA ? 'Leyendo el documento y mejorando los datos con IA…' : 'Leyendo el documento…' });
  qs('#document-results').hidden = true;
  const boton = qs('#submit-document');
  setButtonLoading(boton, true, 'Buscando…');

  try {
    const respuesta = await api.extraerFechasDeDocumento(state.equipoActivo.id, archivo, formato, usarIA);
    renderResultadosDocumento(respuesta);
    setStatus('document-status');
  } catch (error) {
    setStatus('document-status', { error: mensajeDeError(error) });
  } finally {
    setButtonLoading(boton, false);
  }
}

/** Texto de aviso sobre cómo se interpretaron las fechas y qué hizo la IA. */
function avisoDeImportacion(respuesta, formatoElegido) {
  const partes = [];
  const nombreFormato = respuesta.formatoFecha === 'mdy' ? 'mes/día/año' : 'día/mes/año';
  if (respuesta.formatoMixto) {
    partes.push(`Este documento mezcla formatos de fecha. Se usó ${nombreFormato} salvo cuando una fecha solo tiene sentido de otra forma (por ejemplo 25/03). Revisa las marcadas con ⚠.`);
  } else if (respuesta.hayFechasAmbiguas && formatoElegido === 'auto') {
    partes.push(`No hay forma de saber si el documento escribe día/mes o mes/día, así que se asumió ${nombreFormato}. Revisa las fechas marcadas con ⚠ o elige el formato arriba.`);
  }

  const mejorados = respuesta.eventosMejoradosConIA ?? 0;
  if (mejorados > 0) {
    partes.push(`✨ La IA mejoró el nombre o el lugar de ${mejorados} evento${mejorados === 1 ? '' : 's'}. Las fechas y horas las calcula el sistema, no la IA.`);
  }
  if (respuesta.avisoIA) partes.push(`IA: ${respuesta.avisoIA} Se muestran los datos sin mejorar.`);
  return partes.join(' ');
}

function renderResultadosDocumento(respuesta) {
  // Tolera tanto la respuesta nueva ({ eventos, ... }) como una lista simple de fechas.
  const datos = Array.isArray(respuesta) ? { eventos: respuesta } : (respuesta ?? {});
  const eventos = Array.isArray(datos.eventos) ? datos.eventos : [];

  const contenedor = qs('#document-results');
  const lista = qs('#document-date-list');
  const aviso = qs('#document-notice');
  lista.innerHTML = '';
  contenedor.hidden = false;

  const textoAviso = avisoDeImportacion(datos, qs('#document-date-format')?.value || 'auto');
  aviso.textContent = textoAviso;
  aviso.hidden = !textoAviso;

  if (eventos.length === 0) {
    lista.appendChild(ce('li', { class: 'document-empty' }, 'No se encontraron fechas reconocibles.'));
    return;
  }

  for (const evento of eventos) lista.appendChild(crearFilaEventoDetectado(evento));
}

function crearFilaEventoDetectado(evento) {
  const fecha = evento.fecha ?? evento.Fecha;
  const fechaFin = evento.fechaFin ?? null;
  const alternativa = evento.fechaAlternativa ?? null;
  const nombre = evento.nombreEvento ?? evento.textoEncontrado ?? '';
  const confianza = evento.confianza ?? 'media';

  // Si la fecha es ambigua se deja elegir entre las dos lecturas posibles.
  let selector = null;
  if (evento.esAmbigua && alternativa) {
    selector = ce('select', { class: 'document-date-select', 'aria-label': 'Fecha correcta' }, [
      ce('option', { value: fecha }, formatearFechaLarga(fecha)),
      ce('option', { value: alternativa }, formatearFechaLarga(alternativa)),
    ]);
  }

  const detalles = [];
  if (!selector) {
    detalles.push(fechaFin
      ? `${formatearFechaLarga(fecha)} → ${formatearFechaLarga(fechaFin)}`
      : formatearFechaLarga(fecha));
  }
  detalles.push(evento.hora ? formatearHoraCorta(evento.hora) : 'Hora por definir');
  detalles.push(evento.lugar || 'Lugar por definir');

  const notas = [];
  if (evento.mejoradoConIA) {
    if (evento.nombreOriginal) notas.push(`Antes: «${evento.nombreOriginal}»`);
    if (evento.lugarOriginal && evento.lugarOriginal !== evento.lugar) notas.push(`Lugar antes: «${evento.lugarOriginal}»`);
  }
  if (evento.fechaOriginal) notas.push(`Escrito como «${evento.fechaOriginal}»`);
  if (evento.pagina) notas.push(`Página ${evento.pagina}`);
  if (evento.anioInferido) notas.push('Año tomado del documento');

  const copia = ce('div', { class: 'document-date-copy' }, [
    ce('div', { class: 'document-date-title' }, [
      ce('strong', {}, nombre),
      evento.mejoradoConIA ? ce('span', { class: 'badge-ia' }, '✨ IA') : null,
      ce('span', { class: `badge-confianza badge-confianza-${confianza}` }, `Confianza ${confianza}`),
    ]),
    selector,
    ce('span', {}, detalles.join(' · ')),
    evento.esAmbigua
      ? ce('span', { class: 'document-warning' }, '⚠ Fecha ambigua: confirma cuál es la correcta antes de crear el evento.')
      : null,
    ce('span', { class: 'document-note' }, notas.join(' · ')),
  ]);

  return ce('li', { class: 'document-date-row' }, [
    copia,
    ce('button', {
      class: 'btn btn-primary',
      type: 'button',
      onClick: () => usarEventoDetectado(evento, selector ? selector.value : fecha),
    }, 'Crear evento'),
  ]);
}

/** Primera hora libre (desde las 09:00) para ese día, ya que no se permiten dos eventos a la misma hora. */
async function horaLibre(fecha) {
  try {
    const existentes = await api.obtenerEventosDelDia(state.equipoActivo.id, fecha);
    const ocupadas = new Set((existentes ?? []).map((e) => formatearHoraCorta(normalizarEvento(e).hora)));
    for (let h = 9; h <= 22; h++) {
      const candidata = `${String(h).padStart(2, '0')}:00`;
      if (!ocupadas.has(candidata)) return candidata;
    }
  } catch {
    // Si no se puede consultar, se propone la hora por defecto y la API avisará si choca.
  }
  return '09:00';
}

/** Abre el formulario de evento con los datos ya rellenados (desde un documento o desde el asistente). */
async function prepararEventoEnFormulario({ fecha, nombre, hora, lugar, descripcion }) {
  abrirDrawerEvento();
  qs('#evento-fecha').value = fecha;
  qs('#evento-nombre').value = (nombre ?? '').slice(0, 120);
  qs('#evento-lugar').value = (lugar || 'Por definir').slice(0, 120);
  qs('#evento-descripcion').value = (descripcion ?? '').slice(0, 500);
  qs('#evento-hora').value = hora || await horaLibre(fecha);
}

async function usarEventoDetectado(evento, fecha) {
  const partes = ['Evento importado desde un documento.'];
  if (evento.fechaFin) partes.push(`Se extiende hasta el ${formatearFechaLarga(evento.fechaFin)}.`);
  if (evento.textoEncontrado) partes.push(`Texto original: «${evento.textoEncontrado}»`);

  await prepararEventoEnFormulario({
    fecha,
    nombre: evento.nombreEvento,
    hora: evento.hora,
    lugar: evento.lugar,
    descripcion: partes.join(' '),
  });
}

/* ============================== DRAWER: integrantes del equipo ============================== */

async function abrirColaboradores() {
  clearFieldErrors(['collab-usuario']);
  setFormError('collab-form-error', '');
  qs('#form-colaborador').reset();
  openDrawer('collab-drawer', 'collab-drawer-backdrop');
  await cargarColaboradores();
}

function cerrarColaboradores() {
  closeDrawer('collab-drawer', 'collab-drawer-backdrop');
}

async function cargarColaboradores() {
  setStatus('collab-status', { loading: 'Cargando integrantes…' });
  qs('#collab-list').innerHTML = '';

  try {
    const detalle = normalizarEquipo(await api.obtenerEquipo(state.equipoActivo.id));
    state.equipoActivoDetalle = detalle;
    const eslider = detalle.rol === 'lider';
    if (state.equipoActivo.rol !== detalle.rol) setEquipoActivo(datosDeEquipoActivo({ ...detalle, nombreEquipo: detalle.nombreEquipo }));

    setStatus('collab-status');
    renderListaColaboradores(detalle, eslider);

    const puedeAdministrar = eslider && !detalle.esPersonal;
    qs('#form-colaborador').hidden = !puedeAdministrar;
    qs('#team-admin-section').hidden = !puedeAdministrar;
    qs('#team-leave-section').hidden = eslider || detalle.esPersonal;
    if (puedeAdministrar) await cargarInvitacionesDelEquipo();
    else qs('#collab-pending-section').hidden = true;
    renderTopbar();
  } catch (error) {
    setStatus('collab-status', { error: mensajeDeError(error) });
  }
}

function renderListaColaboradores(detalle, eslider) {
  const lista = qs('#collab-list');
  lista.innerHTML = '';

  for (const colaborador of detalle.colaboradores) {
    const soyYo = colaborador.id === state.colaborador?.id;
    const acciones = [];
    if (eslider && !soyYo && !detalle.esPersonal) {
      acciones.push(
        ce('button', {
          class: 'btn btn-secondary', type: 'button',
          onClick: () => manejarTransferirLiderazgo(colaborador),
        }, 'Hacer líder'),
        ce('button', {
          class: 'btn btn-ghost', type: 'button',
          onClick: () => manejarQuitarMiembro(colaborador),
        }, 'Quitar'),
      );
    }

    lista.appendChild(
      ce('li', { class: 'ledger-row' }, [
        ce('div', { class: 'ledger-row-main' }, [
          ce('span', { class: 'ledger-row-title' }, soyYo ? `${colaborador.usuario} (tú)` : colaborador.usuario),
          ce('span', { class: 'ledger-row-meta' }, [
            ce('span', { class: `badge-rol badge-rol-${colaborador.rol}` }, colaborador.rol === 'lider' ? 'Líder' : 'Miembro'),
          ]),
        ]),
        acciones.length ? ce('div', { class: 'ledger-row-actions' }, acciones) : null,
      ]),
    );
  }
}

async function cargarInvitacionesDelEquipo() {
  const seccion = qs('#collab-pending-section');
  const lista = qs('#collab-pending-list');
  lista.innerHTML = '';

  try {
    const bruto = await api.listarInvitacionesDelEquipo(state.equipoActivo.id);
    const invitaciones = (Array.isArray(bruto) ? bruto : []).map(normalizarInvitacion);
    seccion.hidden = invitaciones.length === 0;

    for (const invitacion of invitaciones) {
      lista.appendChild(
        ce('li', { class: 'ledger-row' }, [
          ce('div', { class: 'ledger-row-main' }, [
            ce('span', { class: 'ledger-row-title' }, invitacion.invitado),
            ce('span', { class: 'ledger-row-meta' }, `Invitado por ${invitacion.invitadoPor}`),
          ]),
          ce('div', { class: 'ledger-row-actions' }, [
            ce('button', {
              class: 'btn btn-ghost', type: 'button',
              onClick: () => manejarCancelarInvitacion(invitacion),
            }, 'Cancelar'),
          ]),
        ]),
      );
    }
  } catch {
    seccion.hidden = true;
  }
}

async function manejarSubmitColaborador(event) {
  event.preventDefault();
  clearFieldErrors(['collab-usuario']);
  setFormError('collab-form-error', '');

  const usuario = qs('#collab-usuario').value.trim();
  if (!usuario) {
    setFieldError('collab-usuario', 'Escribe el usuario que quieres invitar.');
    return;
  }

  const boton = qs('#invite-submit');
  setButtonLoading(boton, true, 'Enviando…');

  try {
    await api.invitarAEquipo(state.equipoActivo.id, usuario);
    qs('#form-colaborador').reset();
    toast(`Invitación enviada a "${usuario}". Se unirá cuando la acepte.`, 'success');
    await cargarInvitacionesDelEquipo();
  } catch (error) {
    setFormError('collab-form-error', mensajeDeError(error));
  } finally {
    setButtonLoading(boton, false);
  }
}

async function manejarCancelarInvitacion(invitacion) {
  try {
    await api.cancelarInvitacion(state.equipoActivo.id, invitacion.id);
    toast('Invitación cancelada.', 'success');
  } catch (error) {
    toast(mensajeDeError(error), 'error');
  }
  await cargarInvitacionesDelEquipo();
}

async function manejarQuitarMiembro(colaborador) {
  const confirmado = await confirmar({
    title: '¿Quitar del equipo?',
    message: `"${colaborador.usuario}" dejará de ver y editar el calendario de "${state.equipoActivo.nombreEquipo}".`,
    confirmLabel: 'Quitar',
  });
  if (!confirmado) return;

  try {
    await api.quitarMiembro(state.equipoActivo.id, colaborador.id);
    toast(`"${colaborador.usuario}" ya no es parte del equipo.`, 'success');
    await cargarColaboradores();
  } catch (error) {
    toast(mensajeDeError(error), 'error');
  }
}

async function manejarTransferirLiderazgo(colaborador) {
  const confirmado = await confirmar({
    title: '¿Ceder el liderazgo?',
    message: `"${colaborador.usuario}" será el nuevo líder del equipo y tú pasarás a ser miembro. Solo el líder puede invitar y quitar integrantes.`,
    confirmLabel: 'Ceder liderazgo',
  });
  if (!confirmado) return;

  try {
    await api.transferirLiderazgo(state.equipoActivo.id, colaborador.id);
    toast(`"${colaborador.usuario}" es ahora el líder.`, 'success');
    await refrescarDetalleEquipo();
    await cargarColaboradores();
    if (state.vista) await seleccionarDia(state.diaSeleccionado ?? fechaHoyIso());
  } catch (error) {
    toast(mensajeDeError(error), 'error');
  }
}

async function manejarSalirDelEquipo() {
  const confirmado = await confirmar({
    title: '¿Salir del equipo?',
    message: `Dejarás de ver el calendario de "${state.equipoActivo.nombreEquipo}". Para volver tendrán que invitarte otra vez.`,
    confirmLabel: 'Salir del equipo',
  });
  if (!confirmado) return;

  try {
    await api.quitarMiembro(state.equipoActivo.id, state.colaborador.id);
    toast('Saliste del equipo.', 'success');
    cerrarColaboradores();
    await volverAlCalendarioPersonal();
  } catch (error) {
    toast(mensajeDeError(error), 'error');
  }
}

async function manejarRenombrarEquipo() {
  const actual = state.equipoActivo.nombreEquipo;
  const nuevo = window.prompt('Nuevo nombre del equipo:', actual)?.trim();
  if (!nuevo || nuevo === actual) return;

  try {
    await api.renombrarEquipo(state.equipoActivo.id, nuevo);
    toast('Nombre actualizado.', 'success');
    await refrescarDetalleEquipo();
  } catch (error) {
    toast(mensajeDeError(error), 'error');
  }
}

async function manejarEliminarEquipo() {
  const confirmado = await confirmar({
    title: '¿Eliminar equipo?',
    message: `Se eliminará "${state.equipoActivo.nombreEquipo}" con todos sus eventos, para todos los integrantes. Esta acción no se puede deshacer.`,
    confirmLabel: 'Eliminar equipo',
  });
  if (!confirmado) return;

  try {
    await api.eliminarEquipo(state.equipoActivo.id);
    toast('Equipo eliminado.', 'success');
    cerrarColaboradores();
    await volverAlCalendarioPersonal();
  } catch (error) {
    toast(mensajeDeError(error), 'error');
  }
}

/* ============================== DRAWER: asistente de reuniones (Gemini) ============================== */

const SUGERENCIAS_ASISTENTE = [
  'Proponme fechas para reunirnos esta semana',
  '¿Dónde conviene reunirnos?',
  'Busca el mejor horario para todos',
];

const asistente = { mensajes: [], disponible: null, equipoId: null, enviando: false };

function reiniciarAsistente() {
  asistente.mensajes = [];
  asistente.disponible = null;
  asistente.equipoId = null;
  asistente.enviando = false;
  if (qs('#assistant-messages')) qs('#assistant-messages').innerHTML = '';
}

async function abrirAsistente() {
  openDrawer('assistant-drawer', 'assistant-drawer-backdrop');

  if (asistente.disponible === null || asistente.equipoId !== state.equipoActivo.id) {
    reiniciarAsistente();
    asistente.equipoId = state.equipoActivo.id;
    try {
      asistente.disponible = Boolean((await api.estadoDelAsistente(state.equipoActivo.id))?.disponible);
    } catch (error) {
      asistente.disponible = false;
      toast(mensajeDeError(error), 'error');
    }
  }

  qs('#assistant-unavailable').hidden = asistente.disponible;
  qs('#assistant-form').hidden = !asistente.disponible;
  qs('#assistant-suggestions').hidden = !asistente.disponible;

  if (asistente.disponible && asistente.mensajes.length === 0) {
    const alcance = state.equipoActivo.esPersonal
      ? 'Solo tengo en cuenta tu calendario personal.'
      : 'Tengo en cuenta los horarios libres y los lugares que más usan los integrantes.';
    agregarBurbuja('asistente', `Hola, soy el asistente de reuniones. ${alcance} ¿Qué necesitas organizar?`);
    renderSugerenciasAsistente();
  }
}

function cerrarAsistente() {
  closeDrawer('assistant-drawer', 'assistant-drawer-backdrop');
}

function renderSugerenciasAsistente() {
  const contenedor = qs('#assistant-suggestions');
  contenedor.innerHTML = '';
  for (const texto of SUGERENCIAS_ASISTENTE) {
    contenedor.appendChild(
      ce('button', { class: 'assistant-chip', type: 'button', onClick: () => enviarAlAsistente(texto) }, texto),
    );
  }
}

function agregarBurbuja(rol, texto, extras = []) {
  const contenedor = qs('#assistant-messages');
  const burbuja = ce('div', { class: `assistant-msg assistant-msg-${rol}` }, [texto, ...extras]);
  contenedor.appendChild(burbuja);
  contenedor.scrollTop = contenedor.scrollHeight;
  return burbuja;
}

function manejarSubmitAsistente(event) {
  event.preventDefault();
  const input = qs('#assistant-input');
  const texto = input.value.trim();
  if (!texto) return;
  enviarAlAsistente(texto);
}

async function enviarAlAsistente(texto) {
  if (asistente.enviando) return;
  asistente.enviando = true;

  const input = qs('#assistant-input');
  const boton = qs('#assistant-send');
  input.value = '';
  qs('#assistant-suggestions').innerHTML = '';
  setButtonLoading(boton, true, 'Pensando…');

  asistente.mensajes.push({ rol: 'usuario', texto });
  agregarBurbuja('usuario', texto);
  const pensando = agregarBurbuja('pensando', 'Analizando horarios y lugares…');

  try {
    const respuesta = await api.conversarConElAsistente(
      state.equipoActivo.id,
      asistente.mensajes.map(({ rol, texto: contenido }) => ({ rol, texto: contenido })),
      fechaHoyIso(),
    );

    pensando.remove();
    asistente.mensajes.push({ rol: 'asistente', texto: respuesta.respuesta });
    agregarBurbuja('asistente', respuesta.respuesta, [
      renderPropuestasAsistente(respuesta.propuestas ?? []),
      renderDatosUsadosPorElAsistente(respuesta),
    ]);
  } catch (error) {
    pensando.remove();
    // La pregunta no se guarda en el historial: así el siguiente intento no queda con dos turnos de usuario seguidos.
    asistente.mensajes.pop();
    agregarBurbuja('error', mensajeDeError(error));
    input.value = texto;
  } finally {
    asistente.enviando = false;
    setButtonLoading(boton, false);
  }
}

function renderPropuestasAsistente(propuestas) {
  if (propuestas.length === 0) return null;

  return ce('div', { class: 'assistant-proposals' }, propuestas.map((propuesta) => {
    const meta = [];
    meta.push(`${propuesta.libres} de ${propuesta.total} libres`);
    if (propuesta.ocupados?.length) meta.push(`Ocupados: ${propuesta.ocupados.join(', ')}`);

    return ce('div', { class: 'assistant-proposal' }, [
      ce('div', { class: 'assistant-proposal-copy' }, [
        ce('strong', {}, `${formatearFechaLarga(propuesta.fecha)} · ${formatearHoraCorta(propuesta.hora)}`),
        ce('span', {}, propuesta.lugar ? `📍 ${propuesta.lugar}` : 'Lugar por definir'),
        propuesta.motivo ? ce('span', { class: 'assistant-proposal-meta' }, propuesta.motivo) : null,
        ce('span', { class: 'assistant-proposal-meta' }, meta.join(' · ')),
      ]),
      ce('button', {
        class: 'btn btn-primary', type: 'button',
        onClick: () => crearEventoDesdePropuesta(propuesta),
      }, 'Crear evento'),
    ]);
  }));
}

/** Transparencia: muestra qué datos del equipo usó el asistente para razonar. */
function renderDatosUsadosPorElAsistente(respuesta) {
  const lugares = respuesta.lugaresConsiderados ?? [];
  if (lugares.length === 0 && !(respuesta.integrantes ?? []).length) return null;

  const items = lugares.map((lugar) => {
    const quienes = lugar.integrantes?.length ? ` (${lugar.integrantes.join(', ')})` : '';
    return ce('li', {}, `${lugar.lugar}: ${lugar.usos} uso${lugar.usos === 1 ? '' : 's'}${quienes}`);
  });

  return ce('details', { class: 'assistant-data' }, [
    ce('summary', {}, 'Datos que usó el asistente'),
    ce('p', {}, `Integrantes: ${(respuesta.integrantes ?? []).join(', ')}. Considera los horarios de sus calendarios y los lugares de los eventos que organizaron en este calendario y en el suyo personal.`),
    items.length ? ce('ul', {}, items) : null,
  ]);
}

async function crearEventoDesdePropuesta(propuesta) {
  cerrarAsistente();
  await prepararEventoEnFormulario({
    fecha: propuesta.fecha,
    nombre: state.equipoActivo.esPersonal ? 'Reunión' : 'Reunión de equipo',
    hora: propuesta.hora,
    lugar: propuesta.lugar,
    descripcion: propuesta.motivo ? `Propuesto por el asistente: ${propuesta.motivo}` : 'Propuesto por el asistente.',
  });
}

/* ==================================== Utilidades ==================================== */

function mensajeDeError(error) {
  if (error instanceof ApiError) return error.message;
  return 'Ocurrió un error inesperado. Inténtalo de nuevo.';
}

/* ===================================== Arranque ===================================== */

function inicializar() {
  const esPaginaCalendario = Boolean(qs('#view-calendar') && !qs('#form-login'));
  const enlazar = (selector, evento, manejador) => {
    const el = qs(selector);
    if (el) el.addEventListener(evento, manejador);
  };

  enlazar('#toggle-auth-mode', 'click', cambiarModoAuth);
  enlazar('#form-login', 'submit', manejarSubmitLogin);
  enlazar('#form-crear-equipo', 'submit', manejarSubmitCrearEquipo);
  enlazar('#close-teams-drawer', 'click', cerrarMisCalendarios);
  enlazar('#teams-drawer-backdrop', 'click', cerrarMisCalendarios);
  enlazar('#prev-month', 'click', manejarMesAnterior);
  enlazar('#next-month', 'click', manejarMesSiguiente);
  enlazar('#open-new-event', 'click', abrirDrawerEvento);
  enlazar('#open-new-event-day', 'click', abrirDrawerEvento);
  enlazar('#close-event-drawer', 'click', cerrarDrawerEvento);
  enlazar('#cancel-event', 'click', cerrarDrawerEvento);
  enlazar('#event-drawer-backdrop', 'click', cerrarDrawerEvento);
  enlazar('#form-evento', 'submit', manejarSubmitEvento);
  if (qs('#evento-lugar')) {
    qs('#evento-lugar').addEventListener('input', manejarEntradaLugar);
    qs('#evento-lugar').addEventListener('focus', () => cargarSugerenciasLugares(qs('#evento-lugar').value));
  }
  enlazar('#form-documento', 'submit', manejarSubmitDocumento);
  enlazar('#close-collab-drawer', 'click', cerrarColaboradores);
  enlazar('#collab-drawer-backdrop', 'click', cerrarColaboradores);
  enlazar('#form-colaborador', 'submit', manejarSubmitColaborador);
  enlazar('#rename-team', 'click', manejarRenombrarEquipo);
  enlazar('#delete-team', 'click', manejarEliminarEquipo);
  enlazar('#leave-team', 'click', manejarSalirDelEquipo);
  enlazar('#close-assistant-drawer', 'click', cerrarAsistente);
  enlazar('#assistant-drawer-backdrop', 'click', cerrarAsistente);
  enlazar('#assistant-form', 'submit', manejarSubmitAsistente);

  const haySesion = Boolean(state.token && state.colaborador && state.equipoActivo);

  if (esPaginaCalendario) {
    if (!haySesion) {
      cerrarSesion();
      window.location.replace('/index.html');
      return;
    }
    irACalendario(state.equipoActivo);
    return;
  }

  if (haySesion) {
    window.location.replace('/calendar.html');
  } else {
    cerrarSesion(); // sesión incompleta (por ejemplo, de una versión anterior sin token)
    mostrarLogin({ actualizarUrl: false });
  }
}

function manejarCambioDeRuta() {
  const ruta = window.location.hash.replace(/^#\//, '');

  if (ruta === 'calendar' && state.token && state.colaborador && state.equipoActivo) {
    irACalendario(state.equipoActivo);
    return;
  }

  mostrarLogin({ actualizarUrl: ruta !== 'login' });
}

inicializar();
