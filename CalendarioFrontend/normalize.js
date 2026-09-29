// normalize.js
// La consigna solo especifica los endpoints y el cuerpo de las peticiones
// POST, no la forma exacta del JSON de respuesta. Estas funciones adaptan las
// respuestas de la API a una forma interna estable, aceptando tanto
// camelCase (lo habitual en ASP.NET Core) como PascalCase (por si el
// serializador no está configurado), para que el resto de la app no dependa
// de esos detalles. Si tu API usa otros nombres de campo, ajusta solo aquí.

export function normalizarColaborador(raw) {
  if (!raw) return null;
  return {
    id: raw.id ?? raw.Id ?? null,
    usuario: raw.usuario ?? raw.Usuario ?? '',
  };
}

export function normalizarEquipo(raw) {
  if (!raw) return null;
  const colaboradoresRaw = [raw.colaboradores, raw.Colaboradores, raw.lColaboradores, raw.LColaboradores]
    .find(Array.isArray) ?? [];
  return {
    id: raw.id ?? raw.Id ?? null,
    nombreEquipo: raw.nombreEquipo ?? raw.NombreEquipo ?? raw.nombre ?? raw.Nombre ?? '',
    esPersonal: raw.esPersonal ?? raw.EsPersonal ?? false,
    colaboradores: Array.isArray(colaboradoresRaw) ? colaboradoresRaw.map(normalizarColaborador) : [],
  };
}

export function normalizarEvento(raw) {
  if (!raw) return null;
  return {
    id: raw.id ?? raw.Id ?? null,
    nombreEvento: raw.nombreEvento ?? raw.NombreEvento ?? '',
    hora: raw.hora ?? raw.Hora ?? raw.horaEvento ?? raw.HoraEvento ?? '',
    lugar: raw.lugar ?? raw.Lugar ?? raw.lugarEvento ?? raw.LugarEvento ?? '',
    descripcion: raw.descripcion ?? raw.Descripcion ?? '',
    colaboradorOrganizadorId: raw.colaboradorOrganizadorId ?? raw.ColaboradorOrganizadorId ?? null,
  };
}

/**
 * El endpoint de eventos del mes puede devolver, según cómo se serialice la
 * tupla (Fecha, Evento) del backend, un objeto anidado { fecha, evento } o
 * un objeto aplanado { fecha, nombreEvento, hora, ... }. Se aceptan ambas.
 */
export function normalizarEventoDelMes(raw) {
  if (!raw) return { fecha: null, evento: null };

  if ('evento' in raw || 'Evento' in raw) {
    return {
      fecha: raw.fecha ?? raw.Fecha ?? null,
      evento: normalizarEvento(raw.evento ?? raw.Evento),
    };
  }

  const { fecha, Fecha, ...resto } = raw;
  return {
    fecha: fecha ?? Fecha ?? null,
    evento: normalizarEvento(resto),
  };
}
