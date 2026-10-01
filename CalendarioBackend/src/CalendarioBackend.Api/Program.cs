using CalendarioBackend.Core.Repositories;
using CalendarioBackend.Core.Persistence;
using CalendarioBackend.Core.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var cadenaDeConexion = builder.Configuration.GetConnectionString("Calendai")
    ?? builder.Configuration.GetConnectionString("Calendario")
    ?? throw new InvalidOperationException(
        "Falta la cadena de conexión 'Calendai'. Definí ConnectionStrings__Calendai (por ejemplo en .env o en los secretos de usuario).");

builder.Services.AddDbContext<CalendarioDbContext>(options => options.UseNpgsql(cadenaDeConexion));
builder.Services.AddScoped<IEquipoRepository, PostgresEquipoRepository>();
builder.Services.AddScoped<IColaboradorRepository, PostgresColaboradorRepository>();
builder.Services.AddScoped<EquipoService>();
builder.Services.AddScoped<CalendarioService>();
builder.Services.AddScoped<AutenticacionService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CalendarioDbContext>();
    db.Database.EnsureCreated();
    db.Database.ExecuteSqlRaw("ALTER TABLE IF EXISTS equipos ADD COLUMN IF NOT EXISTS es_personal boolean NOT NULL DEFAULT false");
    db.Database.ExecuteSqlRaw("ALTER TABLE IF EXISTS colaboradores ALTER COLUMN equipo_id DROP NOT NULL");
}

app.UseRouting();
app.UseCors("Frontend");

// Middleware personalizado para CORS
app.Use(async (context, next) =>
{
    if (context.Request.Headers.TryGetValue("Origin", out var origin))
    {
        context.Response.Headers["Access-Control-Allow-Origin"] = origin.ToString();
        context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,DELETE,OPTIONS";
        context.Response.Headers["Vary"] = "Origin";
    }

    if (context.Request.Method == HttpMethods.Options)
    {
        context.Response.StatusCode = StatusCodes.Status204NoContent;
        return;
    }

    await next();
});

// ============================================================
// FRONTEND
// ============================================================

// Ruta física de CalendarioFrontend cuando la API se ejecuta fuera de Docker.
var frontendPath = Path.GetFullPath(
    Path.Combine(
        builder.Environment.ContentRootPath,
        "..",
        "..",
        "..",
        "CalendarioFrontend"
    )
);

if (Directory.Exists(frontendPath))
{
    var frontendProvider = new PhysicalFileProvider(frontendPath);

    // Sirve el frontend cuando se ejecuta la API directamente en el host.
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = frontendProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = frontendProvider });
}

// ============================================================
// API
// ============================================================

app.MapControllers();

// ============================================================
// SPA FALLBACK
// ============================================================

// Si entramos a una ruta que no sea de la API,
// devolvemos el index.html.
if (Directory.Exists(frontendPath))
{
    app.MapFallback(async context =>
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(Path.Combine(frontendPath, "index.html"));
    });
}

// ============================================================

app.Run();