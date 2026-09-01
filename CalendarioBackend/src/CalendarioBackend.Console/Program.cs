using CalendarioBackend.Core.Repositories;
using CalendarioBackend.Core.Services;

// --- Composición de dependencias (a mano; en un backend real esto lo haría el contenedor de DI) ---
IEquipoRepository equipoRepository = new InMemoryEquipoRepository();
var equipoService = new EquipoService(equipoRepository);
var calendarioService = new CalendarioService(equipoRepository);
var autenticacionService = new AutenticacionService(equipoRepository);

Console.WriteLine("=== Backend de Calendario ===\n");

// 1. Crear un equipo (crea automáticamente su Calendario)
var equipo = equipoService.CrearEquipo("Equipo de Desarrollo");
Console.WriteLine($"Equipo creado: {equipo.NombreEquipo} (Id: {equipo.Id})");

// 2. Registrar colaboradores
var ana = equipoService.RegistrarColaborador(equipo.Id, "ana.perez", "clave123");
var luis = equipoService.RegistrarColaborador(equipo.Id, "luis.gomez", "otraClave456");
Console.WriteLine($"Colaboradores registrados: {ana.Usuario}, {luis.Usuario}\n");

// 3. Autenticación
var loginOk = autenticacionService.Autenticar("ana.perez", "clave123");
var loginMal = autenticacionService.Autenticar("ana.perez", "incorrecta");
Console.WriteLine(loginOk is not null ? $"Login correcto para {loginOk.Usuario}" : "Login fallido");
Console.WriteLine(loginMal is not null ? $"Login correcto para {loginMal.Usuario}" : "Login fallido (esperado)\n");

// 4. Agregar eventos a una fecha específica (genera Año/Mes/Semana/Dia bajo demanda)
var fecha = new DateOnly(2026, 9, 3);
calendarioService.AgregarEvento(
    equipo.Id, fecha, "Reunión de Sprint", new TimeOnly(9, 0), "Sala 3",
    descripcion: "Planificación del siguiente sprint", colaboradorOrganizadorId: ana.Id);

calendarioService.AgregarEvento(
    equipo.Id, fecha, "Revisión de código", new TimeOnly(15, 30), "Virtual - Meet",
    colaboradorOrganizadorId: luis.Id);

Console.WriteLine($"Eventos del {fecha:dd/MM/yyyy}:");
foreach (var evento in calendarioService.ObtenerEventosDelDia(equipo.Id, fecha))
    Console.WriteLine($"  - {evento}");

// 5. Consultar todos los eventos del mes
Console.WriteLine($"\nEventos de {fecha:MMMM yyyy}:");
foreach (var (f, evento) in calendarioService.ObtenerEventosDelMes(equipo.Id, fecha.Year, (byte)fecha.Month))
    Console.WriteLine($"  {f:dd/MM/yyyy} -> {evento}");

// 6. Metadatos generados automáticamente por el modelo (Año/Mes)
var añoObj = calendarioService.ObtenerAño(equipo.Id, fecha.Year);
var mesObj = añoObj.ObtenerMes((byte)fecha.Month);
Console.WriteLine($"\n¿{fecha.Year} es bisiesto? {añoObj.EsBisiesto}");
Console.WriteLine($"{mesObj.NombreMes} tiene 31 días. Se organiza en {mesObj.LSemana.Count} semanas.");
