using System.Security.Cryptography;
using System.Threading.RateLimiting;
using CalendarioBackend.Api.Services;
using CalendarioBackend.Core.Asistente;
using CalendarioBackend.Core.Persistence;
using CalendarioBackend.Core.Repositories;
using CalendarioBackend.Core.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// ============================================================
// CORS: el frontend se sirve desde el mismo origen (nginx hace de proxy), así que en producción
// no hace falta. Si se sirve desde otro origen, listarlo en Cors:Origins (Cors__Origins__0=...).
// ============================================================
var origenesPermitidos = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (origenesPermitidos.Length > 0)
            policy.WithOrigins(origenesPermitidos).AllowAnyHeader().AllowAnyMethod();
        else if (builder.Environment.IsDevelopment())
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// ============================================================
// Base de datos y servicios de dominio
// ============================================================
var cadenaDeConexion = builder.Configuration.GetConnectionString("Calendai")
    ?? builder.Configuration.GetConnectionString("Calendario")
    ?? throw new InvalidOperationException(
        "Falta la cadena de conexión 'Calendai'. Defínela con ConnectionStrings__Calendai (por ejemplo en .env o en los secretos de usuario).");

builder.Services.AddDbContext<CalendarioDbContext>(options => options.UseNpgsql(cadenaDeConexion));
builder.Services.AddScoped<IEquipoRepository, PostgresEquipoRepository>();
builder.Services.AddScoped<IColaboradorRepository, PostgresColaboradorRepository>();
builder.Services.AddScoped<IInvitacionRepository, PostgresInvitacionRepository>();
builder.Services.AddScoped<AccesoEquipoService>();
builder.Services.AddScoped<EquipoService>();
builder.Services.AddScoped<CalendarioService>();
builder.Services.AddScoped<AutenticacionService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IDocumentDateExtractionService, DocumentDateExtractionService>();
builder.Services.AddHttpClient("GooglePlaces", client =>
{
    client.Timeout = TimeSpan.FromSeconds(8);
});
builder.Services.AddScoped<IGooglePlacesService, GooglePlacesService>();

// ============================================================
// Asistente de IA (Gemini). Sin clave la app funciona igual; solo se desactivan las funciones de IA.
// ============================================================
builder.Services.AddSingleton(new GeminiOptions
{
    ApiKey = builder.Configuration["Gemini:ApiKey"] ?? string.Empty,
    Model = builder.Configuration["Gemini:Model"] is { Length: > 0 } modelo ? modelo : "gemini-3.5-flash-lite"
});
builder.Services.AddHttpClient(GeminiClient.NombreCliente, client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddSingleton<IModeloGenerativo, GeminiClient>();
builder.Services.AddScoped<PlanificadorReuniones>();
builder.Services.AddScoped<AsistenteReunionesService>();
builder.Services.AddScoped<RefinadorDeEventosService>();

// ============================================================
// Autenticación (JWT)
// ============================================================
var jwt = new JwtOptions
{
    Key = builder.Configuration["Jwt:Key"] ?? string.Empty,
    HorasDeVida = builder.Configuration.GetValue("Jwt:HorasDeVida", 8)
};

if (string.IsNullOrWhiteSpace(jwt.Key))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException(
            "Falta la clave de firma de tokens 'Jwt:Key'. Defínela con Jwt__Key (mínimo 32 caracteres aleatorios).");

    // Solo en desarrollo: clave efímera. Las sesiones se invalidan cada vez que se reinicia la API.
    jwt.Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
}
else if (jwt.Key.Length < 32)
{
    throw new InvalidOperationException("La clave 'Jwt:Key' debe tener al menos 32 caracteres.");
}

builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // conserva el claim "sub" tal cual
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = JwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = JwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwt.ClaveDeFirma(),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

// Límites de uso: freno a la fuerza bruta en login/registro (por IP) y control del gasto de IA (por usuario).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("autenticacion", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("asistente", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.User.FindFirst("sub")?.Value ?? contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// Detrás de nginx la IP real llega en X-Forwarded-For.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    DatabaseMigrator.Migrar(scope.ServiceProvider.GetRequiredService<CalendarioDbContext>());
}

app.UseForwardedHeaders();
app.UseMiddleware<ManejoDeErroresMiddleware>();
app.UseRouting();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// ============================================================
// FRONTEND
// ============================================================

// Ruta física de CalendarioFrontend cuando la API se ejecuta fuera de Docker.
var frontendPath = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, "..", "..", "..", "CalendarioFrontend"));

if (Directory.Exists(frontendPath))
{
    var frontendProvider = new PhysicalFileProvider(frontendPath);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = frontendProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = frontendProvider });
}

// ============================================================
// API
// ============================================================

app.MapControllers();

// Cualquier ruta que no sea de la API devuelve index.html; una ruta /api desconocida es un 404 real.
app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api") || !Directory.Exists(frontendPath))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new { error = "Recurso no encontrado." });
        return;
    }

    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(frontendPath, "index.html"));
});

app.Run();
