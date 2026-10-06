// app.js
// Controlador principal: conecta api.js, state.js, calendar.js y ui.js;
// maneja los formularios y renderiza las tres pantallas de la app
// (inicio de sesión, equipos, calendario).

import * as api from './api.js';
import { ApiError } from './api.js';
import { normalizarEquipo, normalizarEvento, normalizarEventoDelMes, normalizarColaborador } from './normalize.js';
import { state, setColaborador, setEquipoActivo, cerrarSesion } from './state.js';
import {
  construirCuadriculaMes, sumarMeses, formatearFechaLarga, formatearMesAño,
  formatearHoraCorta, normalizarHoraParaApi, fechaHoyIso,
} from './calendar.js';
import {
  qs, ce, toast, setFieldError, clearFieldErrors, setFormError,
  setFieldSuccess, setStatus, setButtonLoading, updatePermissions, renderTeamMembers,
  openDrawer, closeDrawer, confirmar,
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

function renderTopbar(vista) {
  const contenedor = qs('#topbar-actions');
  contenedor.innerHTML = '';
  const controlesContexto = qs('#calendar-context-controls');
  if (controlesContexto) controlesContexto.replaceChildren();

  if (vista === 'calendar' && state.equipoActivo) {
    const esPersonal = Boolean(state.equipoActivo.esPersonal);
    controlesContexto?.append(
      ce('div', { class: 'calendar-context-tabs', role: 'group', 'aria-label': 'Seleccionar calendario' }, [
        ce('button', {
          class: `context-tab${esPersonal ? ' is-active' : ''}`,
          type: 'button',
          'aria-pressed': String(esPersonal),
          onClick: () => toggleCalendarView('personal'),
        }, 'Mi Calendario'),
        ce('button', {
          class: `context-tab${!esPersonal ? ' is-active' : ''}`,
          type: 'button',
          'aria-pressed': String(!esPersonal),
          onClick: () => toggleCalendarView('equipo'),
        }, 'Equipo'),
      ]),
    );
    if (!esPersonal) {
      const selector = ce('select', {
        class: 'team-context-select',
        'aria-label': 'Seleccionar equipo',
        onChange: (event) => seleccionarEquipoDesdeHeader(event.target.value),
      });
      for (const equipo of state.equiposDisponibles) {
        selector.appendChild(ce('option', { value: equipo.id }, equipo.nombreEquipo));
      }
      if (!state.equiposDisponibles.some((equipo) => equipo.id === state.equipoActivo.id)) {
        selector.appendChild(ce('option', { value: state.equipoActivo.id }, state.equipoActivo.nombreEquipo));
      }
      selector.value = state.equipoActivo.id;
      controlesContexto?.appendChild(selector);
    }
    contenedor.appendChild(
      ce('button', { class: 'btn btn-secondary', type: 'button', onClick: abrirColaboradores }, 'Gestión de Equipos'),
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

function esMiembroDeEquipo(equipo) {
  return equipo.colaboradores.some((colaborador) =>
    (state.colaborador.id && colaborador.id === state.colaborador.id)
    || colaborador.usuario.toLocaleLowerCase() === state.currentUser.toLocaleLowerCase());
}

async function cargarEquiposContexto() {
  const usuario = state.currentUser;
  const resumenes = await api.listarEquipos(usuario);
  const resumenesNormalizados = (Array.isArray(resumenes) ? resumenes : []).map(normalizarEquipo);
  const equipos = await Promise.all(resumenesNormalizados.map(async (equipo) =>
    normalizarEquipo(await api.obtenerEquipo(equipo.id, usuario))));
  const propios = equipos.filter(esMiembroDeEquipo);
  state.calendarioPersonal = propios.find((equipo) => equipo.esPersonal)
    || (state.equipoActivo?.esPersonal ? state.equipoActivo : state.calendarioPersonal);
  state.equiposDisponibles = propios.filter((equipo) => !equipo.esPersonal);
}

async function toggleCalendarView(contexto) {
  if (contexto === 'personal') {
    if (!state.calendarioPersonal) {
      toast('No se encontró tu calendario personal.', 'error');
      return;
    }
    await irACalendario(state.calendarioPersonal);
    return;
  }

  if (state.equipoActivo && !state.equipoActivo.esPersonal) {
    renderTopbar('calendar');
    await cargarMes();
    return;
  }
  const equipo = state.equiposDisponibles[0];
  if (!equipo) {
    toast('Crea un equipo desde Gestión de Equipos para empezar a colaborar.');
    abrirColaboradores();
    return;
  }
  await irACalendario(equipo);
}

async function seleccionarEquipoDesdeHeader(equipoId) {
  const equipo = state.equiposDisponibles.find((item) => item.id === equipoId);
  if (equipo) await irACalendario(equipo);
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

    setColaborador(colaborador);
    qs('#form-login').reset();
    toast(`Bienvenido, ${colaborador.usuario}.`, 'success');
    const equipoRespuesta = respuesta?.equipo ?? respuesta?.Equipo ?? null;
    setEquipoActivo(equipoRespuesta ? normalizarEquipo(equipoRespuesta) : null);

    // El calendario se sirve como una página HTML independiente del login.
    window.location.assign('/calendar.html');
  } catch (error) {
    qs('#login-password').value = '';
    setFormError('login-form-error', mensajeDeError(error));
  } finally {
    setButtonLoading(boton, false);
  }
}

/* =================================== EQUIPOS =================================== */

function irAEquipos() {
  mostrarApp('teams');
  cargarEquipos();
}

async function cargarEquipos() {
  setStatus('teams-status', { loading: 'Cargando equipos…' });
  qs('#teams-list').innerHTML = '';

  try {
    const bruto = await api.listarEquipos(state.currentUser);
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

  if (equipos.length === 0) {
    setStatus('teams-status', { empty: 'Todavía no hay equipos. Crea el primero arriba.' });
    return;
  }

  for (const equipo of equipos) {
    const cantidad = equipo.cantidadColaboradores;
    const meta = `${cantidad} colaborador${cantidad === 1 ? '' : 'es'}`;

    const fila = ce('li', { class: 'ledger-row' }, [
      ce('div', { class: 'ledger-row-main' }, [
        ce('span', { class: 'ledger-row-title' }, equipo.nombreEquipo),
        ce('span', { class: 'ledger-row-meta' }, meta),
      ]),
      ce('div', { class: 'ledger-row-actions' }, [
        ce('button', {
          class: 'btn btn-primary', type: 'button',
          onClick: () => manejarSeleccionarEquipo(equipo),
        }, 'Usar este equipo'),
      ]),
    ]);
    lista.appendChild(fila);
  }
}

async function manejarSubmitCrearEquipo(event) {
  event.preventDefault();
  setFieldError('nuevo-equipo-nombre', '');

  const input = qs('#nuevo-equipo-nombre');
  const nombre = input.value.trim();
  if (!state.currentUser) {
    setFieldError('nuevo-equipo-nombre', 'Inicia sesión para crear un equipo.');
    return;
  }
  if (nombre.length < 2) {
    setFieldError('nuevo-equipo-nombre', 'El nombre debe tener al menos 2 caracteres.');
    return;
  }

  const boton = qs('#crear-equipo-submit');
  setButtonLoading(boton, true, 'Creando…');

  try {
    const creado = normalizarEquipo(await api.crearEquipo(nombre, state.currentUser));
    input.value = '';
    toast(`Equipo "${creado.nombreEquipo}" creado.`, 'success');
    await manejarSeleccionarEquipo(creado, creado);
  } catch (error) {
    setFieldError('nuevo-equipo-nombre', mensajeDeError(error));
  } finally {
    setButtonLoading(boton, false);
  }
}

async function manejarSeleccionarEquipo(equipo, detalle = null) {
  setEquipoActivo({ id: equipo.id, nombreEquipo: equipo.nombreEquipo });

  if (!state.colaborador) {
    toast(`Ahora inicia sesión con un colaborador de "${equipo.nombreEquipo}".`);
    mostrarLogin();
    return;
  }

  await irACalendario({ id: equipo.id, nombreEquipo: equipo.nombreEquipo }, detalle);
}

/* ================================== CALENDARIO ================================== */

async function irACalendario(equipo, detalleInicial = null) {
  setEquipoActivo(equipo);
  state.equipoActivoDetalle = detalleInicial;
  state.eventosDelMes = new Map();
  qs('#calendar-grid')?.replaceChildren();
  qs('#day-events-list')?.replaceChildren();
  qs('#collab-list')?.replaceChildren();
  if (detalleInicial && !detalleInicial.esPersonal) {
    state.equiposDisponibles = [
      ...state.equiposDisponibles.filter((item) => item.id !== detalleInicial.id),
      detalleInicial,
    ];
    updatePermissions(rolUsuarioEnEquipo());
  }
  try {
    await cargarEquiposContexto();
  } catch (error) {
    state.equiposDisponibles = [];
    state.calendarioPersonal = equipo.esPersonal ? equipo : null;
    toast(mensajeDeError(error), 'error');
  }
  mostrarApp('calendar');

  const hoy = new Date();
  state.vista = { año: hoy.getFullYear(), mes: hoy.getMonth() + 1 };
  state.diaSeleccionado = null;

  await refrescarDetalleEquipo();
  await cargarMes();
}

async function refrescarDetalleEquipo() {
  try {
    const detalle = normalizarEquipo(await api.obtenerEquipo(state.equipoActivo.id, state.currentUser));
    state.equipoActivoDetalle = detalle;
    state.equipoActivo = {
      id: detalle.id ?? state.equipoActivo.id,
      nombreEquipo: detalle.nombreEquipo || state.equipoActivo.nombreEquipo,
      esPersonal: detalle.esPersonal,
    };
    setEquipoActivo(state.equipoActivo);
    renderTopbar('calendar');
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      toast('El equipo activo ya no existe. Elige otro equipo.', 'error');
      setEquipoActivo(null);
      window.location.replace('/index.html');
      return;
    }
    toast(mensajeDeError(error), 'error');
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
      ce('button', {
        class: 'event-delete', type: 'button', 'aria-label': `Eliminar ${evento.nombreEvento}`,
        onClick: () => manejarEliminarEvento(evento, fechaIso),
      }, '🗑'),
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
    await api.eliminarEvento(state.equipoActivo.id, evento.id, fechaIso);
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
  select.appendChild(ce('option', { value: '' }, 'Sin asignar'));
  const colaboradores = state.equipoActivoDetalle?.colaboradores || [];
  for (const colaborador of colaboradores) {
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

async function manejarSubmitDocumento(event) {
  event.preventDefault();
  const input = qs('#document-file');
  const archivo = input.files?.[0];
  if (!archivo) {
    setStatus('document-status', { error: 'Selecciona un archivo PDF o Word.' });
    return;
  }

  const formato = qs('#document-date-format')?.value || 'auto';

  setStatus('document-status', { loading: 'Leyendo el documento…' });
  qs('#document-results').hidden = true;
  const boton = qs('#submit-document');
  setButtonLoading(boton, true, 'Buscando…');

  try {
    const respuesta = await api.extraerFechasDeDocumento(state.equipoActivo.id, archivo, formato);
    renderResultadosDocumento(respuesta);
    setStatus('document-status');
  } catch (error) {
    setStatus('document-status', { error: mensajeDeError(error) });
  } finally {
    setButtonLoading(boton, false);
  }
}

let elementoQueAbrioModalDocumento = null;

function abrirModalDocumento(event) {
  const modal = qs('#document-modal');
  const backdrop = qs('#document-modal-backdrop');
  if (!modal || !backdrop) return;
  elementoQueAbrioModalDocumento = event.currentTarget;
  modal.hidden = false;
  backdrop.hidden = false;
  qs('#close-document-modal')?.focus();
  document.addEventListener('keydown', manejarTeclaModalDocumento);
}

function cerrarModalDocumento() {
  const modal = qs('#document-modal');
  const backdrop = qs('#document-modal-backdrop');
  if (!modal || !backdrop) return;
  modal.hidden = true;
  backdrop.hidden = true;
  document.removeEventListener('keydown', manejarTeclaModalDocumento);
  if (elementoQueAbrioModalDocumento instanceof HTMLElement) elementoQueAbrioModalDocumento.focus();
}

function manejarTeclaModalDocumento(event) {
  if (event.key === 'Escape' && !qs('#document-modal')?.hidden) cerrarModalDocumento();
}

function actualizarArchivoDocumento(archivo) {
  const etiqueta = qs('#document-file-name');
  const input = qs('#document-file');
  if (!etiqueta || !input) return;
  if (archivo) {
    etiqueta.textContent = archivo.name;
    qs('#document-results').hidden = true;
    setStatus('document-status');
  } else {
    etiqueta.textContent = 'No se ha seleccionado ningún archivo';
  }
}

function manejarDropDocumento(event) {
  event.preventDefault();
  const zona = qs('#document-dropzone');
  zona?.classList.remove('is-dragging');
  const archivo = event.dataTransfer?.files?.[0];
  if (!archivo) return;
  if (!/\.(pdf|docx)$/i.test(archivo.name)) {
    setStatus('document-status', { error: 'Elige un archivo PDF o Word (.docx).' });
    return;
  }
  const transferencia = new DataTransfer();
  transferencia.items.add(archivo);
  qs('#document-file').files = transferencia.files;
  actualizarArchivoDocumento(archivo);
}

function abrirChatIA() {
  qs('#ai-chat-panel').hidden = false;
  qs('#ai-chat-backdrop').hidden = false;
  qs('#open-ai-chat').setAttribute('aria-expanded', 'true');
  qs('#ai-chat-input')?.focus();
  document.addEventListener('keydown', manejarTeclaChatIA);
}

function cerrarChatIA() {
  qs('#ai-chat-panel').hidden = true;
  qs('#ai-chat-backdrop').hidden = true;
  qs('#open-ai-chat').setAttribute('aria-expanded', 'false');
  document.removeEventListener('keydown', manejarTeclaChatIA);
  qs('#open-ai-chat')?.focus();
}

function manejarTeclaChatIA(event) {
  if (event.key === 'Escape' && !qs('#ai-chat-panel')?.hidden) cerrarChatIA();
}

function manejarSubmitChatIA(event) {
  event.preventDefault();
  const input = qs('#ai-chat-input');
  if (!input.value.trim()) return;
  toast('El chat IA aún no está conectado a un servicio.', 'error');
}

/** Texto de aviso sobre cómo se interpretaron las fechas del documento. */
function avisoDeFormato(respuesta, formatoElegido) {
  const nombreFormato = respuesta.formatoFecha === 'mdy' ? 'mes/día/año' : 'día/mes/año';
  if (respuesta.formatoMixto) {
    return `Este documento mezcla formatos de fecha. Se usó ${nombreFormato} salvo cuando una fecha solo tiene sentido de otra forma (por ejemplo 25/03). Revisa las marcadas con ⚠.`;
  }
  if (respuesta.hayFechasAmbiguas && formatoElegido === 'auto') {
    return `No hay forma de saber si el documento escribe día/mes o mes/día, así que se asumió ${nombreFormato}. Revisa las fechas marcadas con ⚠ o elige el formato arriba.`;
  }
  return '';
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

  const textoAviso = avisoDeFormato(datos, qs('#document-date-format')?.value || 'auto');
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
  if (evento.fechaOriginal) notas.push(`Escrito como «${evento.fechaOriginal}»`);
  if (evento.pagina) notas.push(`Página ${evento.pagina}`);
  if (evento.anioInferido) notas.push('Año tomado del documento');

  const copia = ce('div', { class: 'document-date-copy' }, [
    ce('div', { class: 'document-date-title' }, [
      ce('strong', {}, nombre),
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

async function usarEventoDetectado(evento, fecha) {
  cerrarModalDocumento();
  abrirDrawerEvento();
  qs('#evento-fecha').value = fecha;
  qs('#evento-nombre').value = (evento.nombreEvento ?? '').slice(0, 120);
  qs('#evento-lugar').value = (evento.lugar || 'Por definir').slice(0, 120);

  const partes = ['Evento importado desde un documento.'];
  if (evento.fechaFin) partes.push(`Se extiende hasta el ${formatearFechaLarga(evento.fechaFin)}.`);
  if (evento.textoEncontrado) partes.push(`Texto original: «${evento.textoEncontrado}»`);
  qs('#evento-descripcion').value = partes.join(' ').slice(0, 500);

  qs('#evento-hora').value = evento.hora || await horaLibre(fecha);
}

/* ============================== DRAWER: colaboradores ============================== */

async function abrirColaboradores() {
  clearFieldErrors(['collab-usuario', 'nuevo-equipo-nombre']);
  setFormError('collab-form-error', '');
  setStatus('collab-status');
  qs('#form-colaborador').reset();
  openDrawer('collab-drawer', 'collab-drawer-backdrop');
  await cargarColaboradores();
}

function cerrarColaboradores() {
  closeDrawer('collab-drawer', 'collab-drawer-backdrop');
}

async function cargarColaboradores() {
  qs('#collab-current-team-name').textContent = state.equipoActivo?.nombreEquipo || '';
  const equipoPersonal = Boolean(state.equipoActivo?.esPersonal);
  updatePermissions(equipoPersonal ? null : rolUsuarioEnEquipo());
  if (equipoPersonal) {
    setStatus('collab-status', { empty: 'Selecciona o crea un equipo para administrar miembros. Tu calendario personal seguirá disponible.' });
  } else {
    setStatus('collab-status', { loading: 'Cargando miembros del equipo…' });
  }
  qs('#collab-list').innerHTML = '';

  try {
    const detalle = normalizarEquipo(await api.obtenerEquipo(
      state.equipoActivo.id,
      state.currentUser,
    ));
    state.equipoActivoDetalle = detalle;
    updatePermissions(equipoPersonal ? null : rolUsuarioEnEquipo());
    if (!equipoPersonal) setStatus('collab-status');
    renderListaColaboradores(detalle.colaboradores, !equipoPersonal);
  } catch (error) {
    setStatus('collab-status', { error: mensajeDeError(error) });
  }
}

function renderListaColaboradores(colaboradores, permitirEliminar) {
  const lista = qs('#collab-list');

  if (colaboradores.length === 0) {
    lista.replaceChildren();
    if (!state.equipoActivo?.esPersonal) setStatus('collab-status', { empty: 'Este equipo aún no tiene miembros.' });
    return;
  }
  renderTeamMembers(lista, colaboradores, {
    currentUsername: state.colaborador?.usuario,
    allowRemove: permitirEliminar && rolUsuarioEnEquipo() === 'Líder',
    onRemove: manejarEliminarMiembro,
  });
}

function rolUsuarioEnEquipo() {
  const equipo = state.equipoActivoDetalle;
  const usuario = state.currentUser.toLocaleLowerCase();
  const miembro = equipo?.colaboradores.find((colaborador) =>
    String(colaborador.usuario || '').toLocaleLowerCase() === usuario
    || (state.colaborador?.id && colaborador.id === state.colaborador.id));
  return miembro?.rol || equipo?.rolUsuario || null;
}

async function manejarSubmitColaborador(event) {
  event.preventDefault();
  clearFieldErrors(['collab-usuario']);
  setFormError('collab-form-error', '');

  const usuario = qs('#collab-usuario').value.trim();
  if (!usuario) {
    setFieldError('collab-usuario', 'Escribe el nombre de usuario de una cuenta existente.');
    return;
  }

  const boton = qs('#add-team-member');
  setButtonLoading(boton, true, 'Agregando…');

  try {
    await api.agregarMiembro(state.equipoActivo.id, usuario);
    qs('#form-colaborador').reset();
    toast(`"${usuario}" se agregó al equipo.`, 'success');
    await cargarColaboradores();
    await cargarEquiposContexto();
    renderTopbar('calendar');
  } catch (error) {
    setFieldError('collab-usuario', mensajeDeError(error));
  } finally {
    setButtonLoading(boton, false);
    updatePermissions(rolUsuarioEnEquipo());
  }
}

async function manejarEliminarMiembro(colaborador) {
  const confirmado = await confirmar({
    title: '¿Eliminar miembro?',
    message: `Se quitará a "${colaborador.usuario}" de "${state.equipoActivo.nombreEquipo}". Su cuenta y calendario personal se conservarán.`,
    confirmLabel: 'Eliminar miembro',
  });
  if (!confirmado) return;

  try {
    await api.eliminarMiembro(state.equipoActivo.id, colaborador.id);
    toast(`"${colaborador.usuario}" se quitó del equipo.`, 'success');
    await cargarColaboradores();
    await cargarEquiposContexto();
    if (!state.equiposDisponibles.some((equipo) => equipo.id === state.equipoActivo.id)) {
      await toggleCalendarView('personal');
    } else {
      renderTopbar('calendar');
    }
  } catch (error) {
    toast(mensajeDeError(error), 'error');
  }
}

async function manejarCrearEquipoDesdeCalendario(event) {
  event.preventDefault();
  setFieldError('nuevo-equipo-nombre', '');
  const input = qs('#nuevo-equipo-nombre');
  const nombre = input.value.trim();
  if (!state.currentUser) {
    setFieldError('nuevo-equipo-nombre', 'Inicia sesión para crear un equipo.');
    return;
  }
  if (nombre.length < 2) {
    setFieldError('nuevo-equipo-nombre', 'El nombre debe tener al menos 2 caracteres.');
    return;
  }

  const boton = qs('#crear-equipo-submit');
  setButtonLoading(boton, true, 'Creando…');
  try {
    const equipo = normalizarEquipo(await api.crearEquipo(nombre, state.currentUser));
    input.value = '';
    setFieldSuccess('nuevo-equipo-nombre', 'Equipo creado exitosamente.');
    await manejarSeleccionarEquipo(equipo, equipo);
    await cargarColaboradores();
  } catch (error) {
    setFieldError('nuevo-equipo-nombre', mensajeDeError(error));
  }
  finally {
    setButtonLoading(boton, false);
  }
}

/* ==================================== Utilidades ==================================== */

function mensajeDeError(error) {
  if (error instanceof ApiError) {
    if (/the (?:contrase(?:ñ|n)a|password) field is required/i.test(error.message)) {
      return 'El servidor de la API aún exige una contraseña para agregar miembros. Actualiza y reinicia la API; CalendAI no envió ninguna contraseña.';
    }
    return error.message;
  }
  return 'Ocurrió un error inesperado. Inténtalo de nuevo.';
}

/* ===================================== Arranque ===================================== */

function inicializar() {
  const esPaginaCalendario = Boolean(qs('#view-calendar') && !qs('#form-login'));

  if (qs('#toggle-auth-mode')) qs('#toggle-auth-mode').addEventListener('click', cambiarModoAuth);
  if (qs('#form-login')) qs('#form-login').addEventListener('submit', manejarSubmitLogin);
  if (qs('#form-crear-equipo')) {
    qs('#form-crear-equipo').addEventListener(
      'submit',
      qs('#view-calendar') ? manejarCrearEquipoDesdeCalendario : manejarSubmitCrearEquipo,
    );
  }
  if (qs('#prev-month')) qs('#prev-month').addEventListener('click', manejarMesAnterior);
  if (qs('#next-month')) qs('#next-month').addEventListener('click', manejarMesSiguiente);
  if (qs('#open-new-event')) qs('#open-new-event').addEventListener('click', abrirDrawerEvento);
  if (qs('#open-new-event-day')) qs('#open-new-event-day').addEventListener('click', abrirDrawerEvento);
  if (qs('#close-event-drawer')) qs('#close-event-drawer').addEventListener('click', cerrarDrawerEvento);
  if (qs('#cancel-event')) qs('#cancel-event').addEventListener('click', cerrarDrawerEvento);
  if (qs('#event-drawer-backdrop')) qs('#event-drawer-backdrop').addEventListener('click', cerrarDrawerEvento);
  if (qs('#form-evento')) qs('#form-evento').addEventListener('submit', manejarSubmitEvento);
  if (qs('#evento-lugar')) {
    qs('#evento-lugar').addEventListener('input', manejarEntradaLugar);
    qs('#evento-lugar').addEventListener('focus', () => cargarSugerenciasLugares(qs('#evento-lugar').value));
  }
  if (qs('#form-documento')) qs('#form-documento').addEventListener('submit', manejarSubmitDocumento);
  if (qs('#open-document-modal')) qs('#open-document-modal').addEventListener('click', abrirModalDocumento);
  if (qs('#open-document-modal-main')) qs('#open-document-modal-main').addEventListener('click', abrirModalDocumento);
  if (qs('#close-document-modal')) qs('#close-document-modal').addEventListener('click', cerrarModalDocumento);
  if (qs('#cancel-document-modal')) qs('#cancel-document-modal').addEventListener('click', cerrarModalDocumento);
  if (qs('#document-modal-backdrop')) qs('#document-modal-backdrop').addEventListener('click', cerrarModalDocumento);
  if (qs('#document-file')) qs('#document-file').addEventListener('change', (event) => actualizarArchivoDocumento(event.target.files?.[0]));
  if (qs('#document-dropzone')) {
    const zona = qs('#document-dropzone');
    zona.addEventListener('dragover', (event) => {
      event.preventDefault();
      zona.classList.add('is-dragging');
    });
    zona.addEventListener('dragleave', (event) => {
      if (!(event.relatedTarget instanceof Node) || !zona.contains(event.relatedTarget)) zona.classList.remove('is-dragging');
    });
    zona.addEventListener('drop', manejarDropDocumento);
  }
  if (qs('#open-ai-chat')) qs('#open-ai-chat').addEventListener('click', abrirChatIA);
  if (qs('#close-ai-chat')) qs('#close-ai-chat').addEventListener('click', cerrarChatIA);
  if (qs('#ai-chat-backdrop')) qs('#ai-chat-backdrop').addEventListener('click', cerrarChatIA);
  if (qs('#ai-chat-form')) qs('#ai-chat-form').addEventListener('submit', manejarSubmitChatIA);
  if (qs('#close-collab-drawer')) qs('#close-collab-drawer').addEventListener('click', cerrarColaboradores);
  if (qs('#collab-drawer-backdrop')) qs('#collab-drawer-backdrop').addEventListener('click', cerrarColaboradores);
  if (qs('#form-colaborador')) qs('#form-colaborador').addEventListener('submit', manejarSubmitColaborador);

  if (esPaginaCalendario) {
    if (!state.colaborador || !state.equipoActivo) {
      window.location.replace('/index.html');
      return;
    }
    irACalendario(state.equipoActivo);
    return;
  }

  if (window.location.hash === '#/calendar' && state.colaborador && state.equipoActivo) {
    window.location.replace('/calendar.html');
    return;
  }

  if (state.colaborador && state.equipoActivo) {
    window.location.replace('/calendar.html');
  } else {
    mostrarLogin({ actualizarUrl: false });
  }
}

function manejarCambioDeRuta() {
  const ruta = window.location.hash.replace(/^#\//, '');

  if (ruta === 'calendar' && state.colaborador && state.equipoActivo) {
    irACalendario(state.equipoActivo);
    return;
  }

  if (ruta === 'teams' && state.colaborador) {
    irAEquipos();
    return;
  }

  mostrarLogin({ actualizarUrl: ruta !== 'login' });
}

inicializar();
