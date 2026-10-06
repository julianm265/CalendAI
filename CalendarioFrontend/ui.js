// ui.js
// Helpers de interfaz reutilizables: toasts, apertura/cierre de paneles
// laterales con manejo de foco, diálogo de confirmación y estados de
// carga/error/vacío/validación. No conoce la API ni las reglas de negocio.

export function qs(selector, scope = document) {
  return scope.querySelector(selector);
}

export function ce(tag, attrs = {}, children = []) {
  const el = document.createElement(tag);
  for (const [key, value] of Object.entries(attrs)) {
    if (value === undefined || value === null) continue;
    if (key === 'class') el.className = value;
    else if (key === 'text') el.textContent = value;
    else if (key.startsWith('on') && typeof value === 'function') {
      el.addEventListener(key.slice(2).toLowerCase(), value);
    } else {
      el.setAttribute(key, value);
    }
  }
  for (const child of [].concat(children)) {
    if (child === null || child === undefined) continue;
    el.appendChild(typeof child === 'string' ? document.createTextNode(child) : child);
  }
  return el;
}

/* -------------------------------- Toasts --------------------------------- */

let toastSeq = 0;

export function toast(message, type = 'info') {
  const region = qs('#toasts');
  if (!region) return;
  const id = `toast-${++toastSeq}`;
  const el = ce('div', { class: `toast toast-${type}`, id, role: 'status' }, message);
  region.appendChild(el);
  requestAnimationFrame(() => el.classList.add('is-visible'));
  setTimeout(() => {
    el.classList.remove('is-visible');
    setTimeout(() => el.remove(), 200);
  }, 4200);
}

/* --------------------------- Estados de formulario ------------------------- */

export function setFieldError(inputId, message) {
  const errorEl = qs(`#${inputId}-error`);
  const inputEl = qs(`#${inputId}`);
  if (errorEl) {
    errorEl.dataset.feedbackSequence = String(Number(errorEl.dataset.feedbackSequence || 0) + 1);
    errorEl.textContent = message || '';
    errorEl.classList.remove('is-success');
  }
  if (inputEl) inputEl.setAttribute('aria-invalid', message ? 'true' : 'false');
}

export function setFieldSuccess(inputId, message, duration = 3200) {
  const feedbackEl = qs(`#${inputId}-error`);
  const inputEl = qs(`#${inputId}`);
  if (!feedbackEl) return;

  const sequence = Number(feedbackEl.dataset.feedbackSequence || 0) + 1;
  feedbackEl.dataset.feedbackSequence = String(sequence);
  feedbackEl.textContent = message;
  feedbackEl.classList.add('is-success');
  inputEl?.setAttribute('aria-invalid', 'false');
  window.setTimeout(() => {
    if (Number(feedbackEl.dataset.feedbackSequence) !== sequence) return;
    feedbackEl.textContent = '';
    feedbackEl.classList.remove('is-success');
  }, duration);
}

export function clearFieldErrors(ids) {
  ids.forEach((id) => setFieldError(id, ''));
}

export function setFormError(formErrorId, message) {
  const el = qs(`#${formErrorId}`);
  if (el) el.textContent = message || '';
}

/** Muestra/oculta un bloque de estado (#id) para carga, error o vacío. */
export function setStatus(elId, { loading, error, empty } = {}) {
  const el = qs(`#${elId}`);
  if (!el) return;
  const mensaje = loading || error || empty;
  if (!mensaje) {
    el.hidden = true;
    el.textContent = '';
    el.className = 'status-block';
    return;
  }
  el.hidden = false;
  el.className = ['status-block', loading && 'is-loading', error && 'is-error', empty && 'is-empty']
    .filter(Boolean)
    .join(' ');
  el.textContent = mensaje;
}

export function setButtonLoading(button, isLoading, loadingLabel = 'Guardando…') {
  if (!button) return;
  if (isLoading) {
    if (!button.dataset.originalLabel) button.dataset.originalLabel = button.textContent;
    button.disabled = true;
    button.textContent = loadingLabel;
  } else {
    button.disabled = false;
    if (button.dataset.originalLabel) button.textContent = button.dataset.originalLabel;
  }
}

export function updatePermissions(currentUserRole) {
  const input = qs('#collab-usuario');
  const button = qs('#add-team-member');
  if (!input || !button) return;

  const role = String(currentUserRole || '').trim().toLocaleLowerCase();
  const isLeader = role === 'líder' || role === 'lider' || role === 'admin';
  if (isLeader) {
    input.removeAttribute('disabled');
    button.removeAttribute('disabled');
  } else {
    input.setAttribute('disabled', '');
    button.setAttribute('disabled', '');
  }
  input.placeholder = isLeader
    ? 'Buscar usuario (ej. julianm265)'
    : 'Solo el líder puede invitar miembros';
}

export function renderTeamMembers(list, members, {
  currentUsername,
  allowRemove = false,
  onRemove,
} = {}) {
  const listElement = typeof list === 'string' ? qs(`#${list}`) : list;
  if (!listElement) return;
  listElement.replaceChildren();

  const current = String(currentUsername || '').toLocaleLowerCase();
  for (const member of members) {
    const isCurrentUser = String(member.usuario || '').toLocaleLowerCase() === current;
    const isLeader = String(member.rol || '').trim().toLocaleLowerCase() === 'líder'
      || String(member.rol || '').trim().toLocaleLowerCase() === 'lider';
    const row = ce('li', { class: 'team-member-row' }, [
      ce('div', { class: 'team-member-identity' }, [
        ce('span', { class: 'team-member-avatar', 'aria-hidden': 'true' }, (member.usuario || '?').slice(0, 1).toUpperCase()),
        ce('span', { class: 'team-member-name' }, member.usuario || ''),
        isCurrentUser ? ce('span', { class: 'team-member-you' }, 'Tú') : null,
      ]),
      ce('span', {
        class: `team-role-badge${isLeader ? ' badge-leader' : ''}`,
        text: isLeader ? 'Líder' : member.rol || 'Miembro',
      }),
      allowRemove && !isLeader && typeof onRemove === 'function' ? ce('button', {
        class: 'team-member-remove',
        type: 'button',
        'aria-label': `Eliminar a ${member.usuario} del equipo`,
        onClick: (event) => {
          event.preventDefault();
          onRemove(member);
        },
      }, '×') : null,
    ]);
    listElement.appendChild(row);
  }
}

/* ---------------------------------- Drawers -------------------------------- */

let ultimoElementoConFoco = null;

function manejarTeclaDrawer(event) {
  if (event.key !== 'Escape') return;
  const abierto = qs('.drawer.is-open');
  if (abierto) closeDrawer(abierto.id, abierto.dataset.backdropId);
}

export function openDrawer(drawerId, backdropId) {
  const drawer = qs(`#${drawerId}`);
  const backdrop = qs(`#${backdropId}`);
  if (!drawer || !backdrop) return;

  ultimoElementoConFoco = document.activeElement;
  drawer.dataset.backdropId = backdropId;
  backdrop.hidden = false;
  drawer.hidden = false;

  requestAnimationFrame(() => {
    backdrop.classList.add('is-visible');
    drawer.classList.add('is-open');
  });

  const focusable = drawer.querySelector('input, select, textarea, button');
  if (focusable) focusable.focus();

  document.addEventListener('keydown', manejarTeclaDrawer);
}

export function closeDrawer(drawerId, backdropId) {
  const drawer = qs(`#${drawerId}`);
  const backdrop = qs(`#${backdropId}`);
  if (!drawer || !backdrop) return;

  drawer.classList.remove('is-open');
  backdrop.classList.remove('is-visible');
  document.removeEventListener('keydown', manejarTeclaDrawer);

  setTimeout(() => {
    drawer.hidden = true;
    backdrop.hidden = true;
  }, 200);

  if (ultimoElementoConFoco instanceof HTMLElement) ultimoElementoConFoco.focus();
}

/* ------------------------------ Diálogo de confirmación --------------------- */

export function confirmar({ title, message, confirmLabel = 'Eliminar' }) {
  return new Promise((resolve) => {
    const backdrop = qs('#confirm-backdrop');
    const dialog = qs('#confirm-dialog');
    const titleEl = qs('#confirm-title');
    const messageEl = qs('#confirm-message');
    const acceptBtn = qs('#confirm-accept');
    const cancelBtn = qs('#confirm-cancel');
    if (!backdrop || !dialog) {
      resolve(false);
      return;
    }

    titleEl.textContent = title;
    messageEl.textContent = message;
    acceptBtn.textContent = confirmLabel;

    const focoPrevio = document.activeElement;

    const limpiar = (resultado) => {
      backdrop.hidden = true;
      dialog.hidden = true;
      acceptBtn.removeEventListener('click', alAceptar);
      cancelBtn.removeEventListener('click', alCancelar);
      document.removeEventListener('keydown', alTeclear);
      if (focoPrevio instanceof HTMLElement) focoPrevio.focus();
      resolve(resultado);
    };
    const alAceptar = () => limpiar(true);
    const alCancelar = () => limpiar(false);
    const alTeclear = (event) => {
      if (event.key === 'Escape') limpiar(false);
    };

    acceptBtn.addEventListener('click', alAceptar);
    cancelBtn.addEventListener('click', alCancelar);
    document.addEventListener('keydown', alTeclear);

    backdrop.hidden = false;
    dialog.hidden = false;
    acceptBtn.focus();
  });
}
