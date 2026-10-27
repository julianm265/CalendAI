using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Services;

namespace CalendarioBackend.Api.Services;

public sealed class GeminiAssistantService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly EquipoService _equipoService;
    private readonly CalendarioService _calendarioService;

    public GeminiAssistantService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        EquipoService equipoService,
        CalendarioService calendarioService)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _equipoService = equipoService;
        _calendarioService = calendarioService;
    }

    public async Task<AiChatResponse> ConversarAsync(
        Guid equipoId,
        string usuario,
        string mensaje,
        IReadOnlyList<AiChatMessage>? historial,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(usuario))
            throw new UnauthorizedAccessException("La sesión de usuario es obligatoria.");
        if (string.IsNullOrWhiteSpace(mensaje))
            throw new ArgumentException("El mensaje no puede estar vacío.", nameof(mensaje));

        var equipo = _equipoService.ObtenerEquipo(equipoId);
        var miembro = equipo.BuscarColaborador(usuario.Trim())
            ?? throw new UnauthorizedAccessException("No perteneces al equipo seleccionado.");

        var apiKey = _configuration["GEMINI_API_KEY"] ?? _configuration["GEMINI_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("La integración con Gemini no está configurada.");

        var modelo = _configuration["GEMINI_MODEL"] ?? "gemini-2.0-flash";
        var contexto = CrearContexto(equipo, usuario, miembro.Id);
        var contenido = CrearPrompt(mensaje, historial, contexto);
        var cliente = _httpClientFactory.CreateClient("Gemini");
        var url = $"v1beta/models/{Uri.EscapeDataString(modelo)}:generateContent?key={Uri.EscapeDataString(apiKey)}";

        using var respuesta = await cliente.PostAsJsonAsync(url, new
        {
            contents = new[] { new { parts = new[] { new { text = contenido } } } },
            generationConfig = new
            {
                temperature = 0.2,
                responseMimeType = "application/json"
            }
        }, cancellationToken);

        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancellationToken);
        if (!respuesta.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Gemini no pudo procesar la solicitud ({(int)respuesta.StatusCode}).");

        var salida = ExtraerTextoGemini(cuerpo);
        var resultado = ParsearSalida(salida);
        var debeAgendar = EsSolicitudDeAgendamiento(mensaje);
        var accionSolicitada = debeAgendar &&
            (resultado.Action is null ||
             !string.Equals(resultado.Action.Type, "create_event", StringComparison.OrdinalIgnoreCase))
            ? IntentarExtraerAgendamiento(mensaje)
            : resultado.Action;
        var accion = EjecutarAccion(accionSolicitada, equipo, miembro.Id);
        if (debeAgendar && accion is null)
        {
            return new AiChatResponse(
                "Puedo agendarlo, pero necesito el nombre, la fecha en formato AAAA-MM-DD y la hora en formato HH:mm.",
                null);
        }

        return new AiChatResponse(resultado.Reply, accion);
    }

    private string CrearContexto(Equipo equipo, string usuario, Guid miembroId)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var eventos = Enumerable.Range(0, 3)
            .SelectMany(offset =>
            {
                var fecha = hoy.AddMonths(offset);
                return _calendarioService.ObtenerEventosDelMes(equipo.Id, fecha.Year, (byte)fecha.Month)
                    .Select(item => new ContextoEvento(item.Fecha, item.Evento));
            })
            .GroupBy(item => item.Evento.Id)
            .Select(grupo => grupo.First())
            .OrderBy(item => item.Fecha)
            .ThenBy(item => item.Evento.HoraEvento)
            .ToList();

        var lugares = eventos
            .Select(item => item.Evento.LugarEvento?.Trim())
            .Where(lugar => !string.IsNullOrWhiteSpace(lugar))
            .GroupBy(lugar => lugar!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(grupo => grupo.Count())
            .ThenBy(grupo => grupo.Key, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .Select(grupo => new { lugar = grupo.First(), usos = grupo.Count() });

        var tendencia = eventos
            .GroupBy(item => item.Fecha.ToString("yyyy-MM", CultureInfo.InvariantCulture))
            .OrderBy(grupo => grupo.Key)
            .Select(grupo => new { mes = grupo.Key, eventos = grupo.Count() });

        return JsonSerializer.Serialize(new
        {
            equipo = equipo.NombreEquipo,
            usuario,
            rol = equipo.ObtenerRolColaborador(miembroId),
            rango = new { desde = hoy, hasta = hoy.AddMonths(3).AddDays(-1) },
            eventos = eventos.Select(item => new
            {
                fecha = item.Fecha,
                hora = item.Evento.HoraEvento.ToString("HH:mm", CultureInfo.InvariantCulture),
                nombre = item.Evento.NombreEvento,
                lugar = item.Evento.LugarEvento,
                descripcion = item.Evento.Descripcion
            }),
            lugaresFrecuentes = lugares,
            tendenciaMensual = tendencia
        });
    }

    private static string CrearPrompt(
        string mensaje,
        IReadOnlyList<AiChatMessage>? historial,
        string contexto)
    {
        var conversaciones = historial is null
            ? ""
            : string.Join('\n', historial.TakeLast(12).Select(item => $"{item.Role}: {item.Content}"));

        return $"""
            Eres el asistente conversacional de CalendAI. Responde en español, de forma clara y breve.
            Trabajas únicamente con el equipo y los eventos incluidos en CONTEXTO. No inventes disponibilidad,
            eventos, usuarios ni lugares. Puedes explicar lugares frecuentes, tendencias y proponer horarios
            libres. Para agendar un evento necesitas fecha ISO (YYYY-MM-DD), hora de 24 horas y nombre.
            Si falta alguno, pregunta antes de crear. Nunca crees eventos solo por una sugerencia: crea solo
            cuando el usuario lo pida expresamente. Verifica mentalmente los conflictos del CONTEXTO.

            Devuelve SOLO JSON válido con las propiedades reply y action.
            action debe ser null o un objeto create_event con fecha, hora, nombre,
            lugar y descripcion.
            No uses Markdown dentro del JSON.

            CONTEXTO:
            {contexto}

            HISTORIAL:
            {conversaciones}

            MENSAJE ACTUAL:
            {mensaje}
            """;
    }

    private static string ExtraerTextoGemini(string cuerpo)
    {
        using var documento = JsonDocument.Parse(cuerpo);
        return documento.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? throw new InvalidOperationException("Gemini devolvió una respuesta vacía.");
    }

    private static AiModelResult ParsearSalida(string salida)
    {
        var texto = salida.Trim();
        if (texto.StartsWith("```", StringComparison.Ordinal))
        {
            texto = texto.Replace("```json", "", StringComparison.OrdinalIgnoreCase)
                .Replace("```", "", StringComparison.Ordinal).Trim();
        }

        try
        {
            var resultado = JsonSerializer.Deserialize<AiModelResult>(texto, JsonOptions);
            return resultado is null || string.IsNullOrWhiteSpace(resultado.Reply)
                ? throw new InvalidOperationException("Gemini devolvió un formato incompleto.")
                : resultado;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Gemini devolvió una respuesta no válida.", exception);
        }
    }

    private AiActionResult? EjecutarAccion(AiAction? action, Equipo equipo, Guid miembroId)
    {
        if (action is null || !string.Equals(action.Type, "create_event", StringComparison.OrdinalIgnoreCase))
            return null;
        if (!DateOnly.TryParseExact(action.Fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha) ||
            !TimeOnly.TryParseExact(action.Hora, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora) ||
            string.IsNullOrWhiteSpace(action.Nombre))
            throw new InvalidOperationException("No se pudo validar la fecha, hora o nombre del evento.");

        var evento = _calendarioService.AgregarEvento(
            equipo.Id,
            fecha,
            action.Nombre.Trim(),
            hora,
            string.IsNullOrWhiteSpace(action.Lugar) ? null : action.Lugar.Trim(),
            action.Descripcion,
            miembroId);
        return new AiActionResult("event_created", evento.Id, fecha);
    }

    private static bool EsSolicitudDeAgendamiento(string mensaje) =>
        Regex.IsMatch(
            mensaje,
            @"\b(agenda|agendar|agendame|crear|crea|programa|programar|reserva|reservar)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static AiAction? IntentarExtraerAgendamiento(string mensaje)
    {
        if (!EsSolicitudDeAgendamiento(mensaje))
            return null;

        var coincidencia = Regex.Match(
            mensaje,
            @"\b(?:agenda|agendar|agendame|crear|crea|programa|programar|reserva|reservar)\b\s+(?:un[ao]?\s+)?(?<nombre>.+?)\s+(?:el|para)\s+(?<fecha>\d{4}-\d{2}-\d{2})\s+(?:a las|a la|a)\s+(?<hora>\d{1,2}:\d{2})(?<resto>.*)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!coincidencia.Success)
            return null;

        var nombre = coincidencia.Groups["nombre"].Value.Trim(' ', '.', ',', ':');
        var hora = coincidencia.Groups["hora"].Value;
        if (nombre.Length == 0)
            return null;

        var resto = coincidencia.Groups["resto"].Value;
        var lugar = Regex.Match(
            resto,
            @"\ben\s+(?<lugar>.+?)(?:\s+con\b|\s+para\b|\s+sin\b|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return new AiAction(
            "create_event",
            coincidencia.Groups["fecha"].Value,
            hora,
            nombre,
            lugar.Success ? lugar.Groups["lugar"].Value.Trim(' ', '.', ',') : null,
            null);
    }

    private sealed record ContextoEvento(DateOnly Fecha, Evento Evento);
    private sealed record AiModelResult(string Reply, AiAction? Action);
    private sealed record AiAction(string Type, string Fecha, string Hora, string Nombre, string? Lugar, string? Descripcion);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public sealed record AiChatMessage(string Role, string Content);
public sealed record AiChatRequest(Guid EquipoId, string Mensaje, IReadOnlyList<AiChatMessage>? Historial);
public sealed record AiChatResponse(string Reply, AiActionResult? Action);
public sealed record AiActionResult(string Type, Guid EventId, DateOnly Fecha);
