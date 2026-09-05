using CalendarioBackend.Core.Repositories;
using CalendarioBackend.Core.Services;

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
