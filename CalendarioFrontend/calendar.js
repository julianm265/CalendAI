// calendar.js
// Funciones puras para construir la cuadrícula mensual y formatear fechas.
// No tocan el DOM ni la API: son fáciles de razonar y de probar por separado.

export const MESES = [
  'enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
  'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre',
];

export const DIAS_LARGOS = [
  'domingo', 'lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado',
];

export function pad2(n) {
  return String(n).padStart(2, '0');
}

/** Construye una fecha ISO 'YYYY-MM-DD' a partir de año, mes (1-12) y día. */
export function toIsoDate(año, mes, dia) {
  return `${año}-${pad2(mes)}-${pad2(dia)}`;
}

/** Fecha de hoy en formato ISO 'YYYY-MM-DD', en la zona horaria local. */
export function fechaHoyIso() {
  const ahora = new Date();
  return toIsoDate(ahora.getFullYear(), ahora.getMonth() + 1, ahora.getDate());
}

/**
 * Devuelve la cuadrícula del mes como una lista de semanas (arrays de 7 días).
 * Cada día es { iso, dia, enMes } donde `enMes` indica si pertenece al mes
 * mostrado (false para los días de relleno del mes anterior/siguiente).
 * Las semanas empiezan en lunes.
 */
export function construirCuadriculaMes(año, mes) {
  const primerDiaMes = new Date(año, mes - 1, 1);
  const desplazamientoInicial = (primerDiaMes.getDay() + 6) % 7; // 0 = lunes
  const diasEnMes = new Date(año, mes, 0).getDate();
  const totalCeldas = Math.ceil((desplazamientoInicial + diasEnMes) / 7) * 7;

  const celdas = [];
  for (let i = 0; i < totalCeldas; i++) {
    const offset = i - desplazamientoInicial + 1;
    const fecha = new Date(año, mes - 1, offset);
    celdas.push({
      iso: toIsoDate(fecha.getFullYear(), fecha.getMonth() + 1, fecha.getDate()),
      dia: fecha.getDate(),
      enMes: fecha.getMonth() === mes - 1 && fecha.getFullYear() === año,
    });
  }

  const semanas = [];
  for (let i = 0; i < celdas.length; i += 7) {
    semanas.push(celdas.slice(i, i + 7));
  }
  return semanas;
}

/** Suma (o resta) meses a un año/mes dado, manejando el desborde de año. */
export function sumarMeses(año, mes, delta) {
  const fecha = new Date(año, mes - 1 + delta, 1);
  return { año: fecha.getFullYear(), mes: fecha.getMonth() + 1 };
}

/** Formatea una fecha ISO como "lunes, 3 de septiembre de 2026". */
export function formatearFechaLarga(iso) {
  const [año, mes, dia] = iso.split('-').map(Number);
  const fecha = new Date(año, mes - 1, dia);
  return `${DIAS_LARGOS[fecha.getDay()]}, ${dia} de ${MESES[mes - 1]} de ${año}`;
}

/** Formatea "septiembre de 2026" para el encabezado del calendario. */
export function formatearMesAño(año, mes) {
  return `${MESES[mes - 1]} de ${año}`;
}

/** Recorta una hora "HH:mm:ss" a "HH:mm" para mostrarla en la interfaz. */
export function formatearHoraCorta(hora) {
  if (!hora) return '';
  return hora.slice(0, 5);
}

/** Asegura que una hora tenga segundos: "09:00" -> "09:00:00". */
export function normalizarHoraParaApi(hora) {
  if (!hora) return hora;
  return hora.length === 5 ? `${hora}:00` : hora;
}
