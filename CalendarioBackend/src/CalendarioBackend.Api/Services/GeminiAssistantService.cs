using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Services;

namespace CalendarioBackend.Api.Services;

public sealed class GeminiAssistantService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly EquipoService _equipoService;
    private readonly CalendarioService _calendarioService;
    private readonly ILogger<GeminiAssistantService> _logger;

    public GeminiAssistantService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        EquipoService equipoService,
        CalendarioService calendarioService,
        ILogger<GeminiAssistantService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _equipoService = equipoService;
        _calendarioService = calendarioService;
        _logger = logger;
    }

    public AsistenteEstado ObtenerEstado() =>
        new(true, _configuration["GEMINI_MODEL"] ?? "gemini-3.5-flash-lite");

    public async Task<AsistenteChatResponse> ConversarAsync(
        Guid equipoId,
        string usuario,
        IReadOnlyList<AsistenteMensaje> mensajes,
        DateOnly hoy,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(usuario))
            throw new UnauthorizedAccessException("La sesión de usuario es obligatoria.");
        if (mensajes is null || mensajes.Count == 0)
            throw new ArgumentException("Debes enviar al menos un mensaje.", nameof(mensajes));

        var equipo = _equipoService.ObtenerEquipo(equipoId);
        var miembro = equipo.BuscarColaborador(usuario.Trim())
            ?? throw new UnauthorizedAccessException("No perteneces al equipo seleccionado.");
        var apiKey = _configuration["GEMINI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("La integración con Gemini no está configurada.");

        var candidatos = ConstruirCandidatos(equipo, hoy);
        var prompt = CrearPrompt(equipo, usuario, miembro.Id, mensajes, hoy, candidatos);
        var modelo = _configuration["GEMINI_MODEL"] ?? "gemini-3.5-flash-lite";
        var cliente = _httpClientFactory.CreateClient("Gemini");
        var url = $"v1beta/models/{Uri.EscapeDataString(modelo)}:generateContent?key={Uri.EscapeDataString(apiKey)}";

        using var respuesta = await cliente.PostAsJsonAsync(url, new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.1, responseMimeType = "application/json" }
        }, cancellationToken);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancellationToken);
        if (!respuesta.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini devolvió HTTP {StatusCode} usando el modelo {Modelo}.", (int)respuesta.StatusCode, modelo);
            throw new InvalidOperationException($"Gemini no pudo procesar la solicitud ({(int)respuesta.StatusCode}).");
        }

        var modeloRespuesta = ParsearSalida(ExtraerTextoGemini(cuerpo));
        var propuestas = (modeloRespuesta.Propuestas ?? Array.Empty<ModelPropuesta>())
            .Take(3)
            .Select(item => (item, candidato: EncontrarCandidato(item, candidatos)))
            .Where(item => item.candidato is not null)
            .Select(item => new AsistentePropuesta(
                item.item.Fecha,
                item.item.Hora,
                string.IsNullOrWhiteSpace(item.item.Lugar) ? null : item.item.Lugar,
                item.item.Motivo,
                item.candidato!.Libres,
                item.candidato.Total,
                item.candidato.Ocupados))
            .ToList();

        return new AsistenteChatResponse(
            modeloRespuesta.Respuesta?.Trim() ?? "No encontré una respuesta para esa solicitud.",
            propuestas,
            equipo.LColaboradores.Select(c => c.Usuario).OrderBy(x => x).ToList(),
            candidatos.Select(c => c.Lugar).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private IReadOnlyList<Candidato> ConstruirCandidatos(Equipo equipo, DateOnly hoy)
    {
        var lugares = _calendarioService.ObtenerEventosDelMes(equipo.Id, hoy.Year, (byte)hoy.Month)
            .Concat(_calendarioService.ObtenerEventosDelMes(equipo.Id, hoy.AddDays(31).Year, (byte)hoy.AddDays(31).Month))
            .Select(x => x.Evento.LugarEvento?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .Append("")
            .ToList();
        var eventos = Enumerable.Range(0, 15)
            .SelectMany(offset =>
            {
                var fecha = hoy.AddDays(offset);
                return _calendarioService.ObtenerEventosDelDia(equipo.Id, fecha)
                    .Select(e => (fecha, e));
            })
            .ToList();
        var integrantes = Math.Max(1, equipo.LColaboradores.Count);
        return Enumerable.Range(0, 15).SelectMany(offset =>
            Enumerable.Range(9, 15).SelectMany(hour =>
                new[] { 0, 30 }.Select(minute =>
                {
                    var fecha = hoy.AddDays(offset);
                    var hora = new TimeOnly(hour, minute).ToString("HH:mm", CultureInfo.InvariantCulture);
                    var ocupados = eventos.Where(x => x.fecha == fecha && x.e.HoraEvento.ToString("HH:mm") == hora)
                        .Select(x => equipo.BuscarColaborador(x.e.ColaboradorOrganizadorId ?? Guid.Empty)?.Usuario ?? "usuario")
                        .Distinct().ToList();
                    return lugares.Select(lugar => new Candidato(fecha.ToString("yyyy-MM-dd"), hora, lugar, integrantes - Math.Min(integrantes, ocupados.Count), integrantes, ocupados));
                }))).SelectMany(x => x).Where(x => x.Libres > 0).ToList();
    }

    private static Candidato? EncontrarCandidato(ModelPropuesta propuesta, IReadOnlyList<Candidato> candidatos)
    {
        var lugar = propuesta.Lugar?.Trim() ?? "";
        return candidatos.FirstOrDefault(c => c.Fecha == propuesta.Fecha && c.Hora == propuesta.Hora &&
            string.Equals(c.Lugar, lugar, StringComparison.OrdinalIgnoreCase));
    }

    private static string CrearPrompt(Equipo equipo, string usuario, Guid miembroId, IReadOnlyList<AsistenteMensaje> mensajes,
        DateOnly hoy, IReadOnlyList<Candidato> candidatos) =>
        $"""
        Eres el asistente de CalendAI. Responde en español y devuelve SOLO JSON válido.
        No inventes fechas, horas ni lugares. Selecciona propuestas únicamente de CANDIDATOS.
        Devuelve un objeto JSON con las propiedades respuesta y propuestas. Cada propuesta tiene fecha,
        hora, lugar (string o null) y motivo.
        Devuelve como máximo tres propuestas. No crees eventos.
        EQUIPO: {equipo.NombreEquipo}; USUARIO: {usuario}; HOY: {hoy:yyyy-MM-dd}
        CANDIDATOS: {JsonSerializer.Serialize(candidatos)}
        HISTORIAL: {JsonSerializer.Serialize(mensajes.TakeLast(20))}
        """;

    private static string ExtraerTextoGemini(string cuerpo)
    {
        using var documento = JsonDocument.Parse(cuerpo);
        return documento.RootElement.GetProperty("candidates")[0].GetProperty("content")
            .GetProperty("parts")[0].GetProperty("text").GetString()
            ?? throw new InvalidOperationException("Gemini devolvió una respuesta vacía.");
    }

    private static ModeloRespuesta ParsearSalida(string salida)
    {
        var texto = salida.Trim().Replace("```json", "", StringComparison.OrdinalIgnoreCase).Replace("```", "").Trim();
        try
        {
            return JsonSerializer.Deserialize<ModeloRespuesta>(texto, JsonOptions)
                ?? throw new InvalidOperationException("Gemini devolvió un formato incompleto.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Gemini devolvió una respuesta no válida.", exception);
        }
    }

    private sealed record Candidato(string Fecha, string Hora, string Lugar, int Libres, int Total, IReadOnlyList<string> Ocupados);
    private sealed record ModeloRespuesta(string? Respuesta, IReadOnlyList<ModelPropuesta> Propuestas);
    private sealed record ModelPropuesta(string Fecha, string Hora, string? Lugar, string Motivo);
}

public sealed record AsistenteEstado(bool Disponible, string Modelo);
public sealed record AsistenteMensaje(string Rol, string Texto);
public sealed record AsistenteChatRequest(IReadOnlyList<AsistenteMensaje> Mensajes, DateOnly Hoy);
public sealed record AsistentePropuesta(string Fecha, string Hora, string? Lugar, string Motivo, int Libres, int Total, IReadOnlyList<string> Ocupados);
public sealed record AsistenteChatResponse(string Respuesta, IReadOnlyList<AsistentePropuesta> Propuestas, IReadOnlyList<string> Integrantes, IReadOnlyList<string> LugaresConsiderados);
