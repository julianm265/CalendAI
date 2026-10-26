using CalendarioBackend.Core.Repositories;
using CalendarioBackend.Core.Services;

// --- Composición de dependencias (a mano; en el backend real lo hace el contenedor de DI) ---
var repositorio = new InMemoryRepositorio();
var acceso = new AccesoEquipoService(repositorio);
var autenticacion = new AutenticacionService(repositorio, repositorio);
var equipos = new EquipoService(repositorio, repositorio, repositorio, acceso);
var calendario = new CalendarioService(repositorio, acceso);

Console.WriteLine("=== Backend de Calendario ===\n");

// 1. Registro: cada usuario recibe su calendario personal
var ana = autenticacion.RegistrarUsuario("ana.perez", "clave123");
var luis = autenticacion.RegistrarUsuario("luis.gomez", "otraClave456");
Console.WriteLine($"Usuarios registrados: {ana.Usuario}, {luis.Usuario}");
Console.WriteLine($"Calendario personal de Ana: {autenticacion.ObtenerOCrearCalendarioPersonal(ana).NombreEquipo}\n");

// 2. Ana crea un equipo (queda como líder) e invita a Luis
var equipo = equipos.CrearEquipo(ana.Id, "Equipo de Desarrollo");
var invitacion = equipos.Invitar(equipo.Id, ana.Id, "luis.gomez");
Console.WriteLine($"Equipo creado: {equipo.NombreEquipo}. Invitación enviada a {invitacion.Invitado}.");

// 3. Luis acepta
equipos.ResponderInvitacion(invitacion.Id, luis.Id, aceptar: true);
foreach (var miembro in equipos.ObtenerDetalle(equipo.Id, ana.Id).Miembros)
    Console.WriteLine($"  - {miembro.Usuario} ({miembro.Rol})");

// 4. Eventos del calendario del equipo
var fecha = new DateOnly(2026, 9, 3);
calendario.AgregarEvento(equipo.Id, ana.Id, fecha, "Reunión de Sprint", new TimeOnly(9, 0), "Sala 3",
    descripcion: "Planificación del siguiente sprint");
calendario.AgregarEvento(equipo.Id, luis.Id, fecha, "Revisión de código", new TimeOnly(15, 30), "Virtual - Meet");

Console.WriteLine($"\nEventos del {fecha:dd/MM/yyyy}:");
foreach (var evento in calendario.ObtenerEventosDelDia(equipo.Id, ana.Id, fecha))
    Console.WriteLine($"  - {evento}");

Console.WriteLine($"\nEventos de {fecha:MMMM yyyy}:");
foreach (var (f, evento) in calendario.ObtenerEventosDelMes(equipo.Id, luis.Id, fecha.Year, fecha.Month))
    Console.WriteLine($"  {f:dd/MM/yyyy} -> {evento}");
