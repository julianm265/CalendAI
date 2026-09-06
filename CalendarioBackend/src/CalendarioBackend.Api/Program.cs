using CalendarioBackend.Core.Repositories;
using CalendarioBackend.Core.Persistence;
using CalendarioBackend.Core.Services;
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

var connectionString = builder.Configuration.GetConnectionString("Calendai")
	?? throw new InvalidOperationException("Falta la cadena de conexión 'Calendai'.");

builder.Services.AddDbContext<CalendaiDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IEquipoRepository, PostgresEquipoRepository>();
builder.Services.AddScoped<EquipoService>();
builder.Services.AddScoped<CalendarioService>();
builder.Services.AddScoped<AutenticacionService>();

var app = builder.Build();

app.UseRouting();
app.UseCors("Frontend");
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
app.MapControllers();

app.Run();
