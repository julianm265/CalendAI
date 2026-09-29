using CalendarioBackend.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace CalendarioBackend.Api.Controllers;

[ApiController]
[Route("api/equipos")]
public class EquiposController : ControllerBase
{
    private readonly EquipoService _equipoService;

    public EquiposController(EquipoService equipoService)
    {
        _equipoService = equipoService;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<EquipoResumen>> Listar()
    {
        var equipos = _equipoService.ListarEquipos()
            .Select(equipo => new EquipoResumen(
                equipo.Id,
                equipo.NombreEquipo,
                equipo.LColaboradores.Count,
                equipo.EsPersonal))
            .ToList();

        return Ok(equipos);
    }

    [HttpGet("{id:guid}")]
    public ActionResult<EquipoDetalle> Obtener(Guid id)
    {
        try
        {
            var equipo = _equipoService.ObtenerEquipo(id);
            return Ok(new EquipoDetalle(
                equipo.Id,
                equipo.NombreEquipo,
                equipo.LColaboradores
                    .Select(colaborador => new ColaboradorResumen(colaborador.Id, colaborador.Usuario))
                    .ToList(),
                equipo.EsPersonal));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public ActionResult<EquipoResumen> Crear(CrearEquipoRequest request)
    {
        try
        {
            var equipo = _equipoService.CrearEquipo(request.NombreEquipo);
            var resumen = new EquipoResumen(equipo.Id, equipo.NombreEquipo, 0, false);
            return CreatedAtAction(nameof(Obtener), new { id = equipo.Id }, resumen);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    [HttpPost("{id:guid}/colaboradores")]
    public ActionResult<ColaboradorResumen> RegistrarColaborador(
        Guid id,
        RegistrarColaboradorRequest request)
    {
        try
        {
            var colaborador = _equipoService.RegistrarColaborador(id, request.Usuario, request.Contraseña);
            return Ok(new ColaboradorResumen(colaborador.Id, colaborador.Usuario));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }
}

public sealed record CrearEquipoRequest(string NombreEquipo);
public sealed record RegistrarColaboradorRequest(string Usuario, string Contraseña);
public sealed record EquipoResumen(Guid Id, string Nombre, int Colaboradores, bool EsPersonal);
public sealed record EquipoDetalle(Guid Id, string Nombre, IReadOnlyList<ColaboradorResumen> Colaboradores, bool EsPersonal);
public sealed record ColaboradorResumen(Guid Id, string Usuario);
