using CalendarioBackend.Core.Repositories;
using CalendarioBackend.Core.Services;
using Microsoft.Extensions.FileProviders;

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

builder.Services.AddSingleton<IEquipoRepository, InMemoryEquipoRepository>();
builder.Services.AddSingleton<EquipoService>();
builder.Services.AddSingleton<CalendarioService>();
builder.Services.AddSingleton<AutenticacionService>();

var app = builder.Build();

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

// Ruta física de CalendarioFrontend
var frontendPath = Path.GetFullPath(
    Path.Combine(
        builder.Environment.ContentRootPath,
        "..",
        "..",
        "..",
        "CalendarioFrontend"
    )
);

var frontendProvider = new PhysicalFileProvider(frontendPath);

// Sirve index.html, CSS, JS, etc.
app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = frontendProvider
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = frontendProvider
});

// ============================================================
// API
// ============================================================

app.MapControllers();

// ============================================================
// SPA FALLBACK
// ============================================================

// Si entramos a una ruta que no sea de la API,
// devolvemos el index.html.
app.MapFallback(async context =>
{
    context.Response.ContentType = "text/html";

    var indexPath = Path.Combine(frontendPath, "index.html");

    await context.Response.SendFileAsync(indexPath);
});

// ============================================================

app.Run("http://localhost:3000");