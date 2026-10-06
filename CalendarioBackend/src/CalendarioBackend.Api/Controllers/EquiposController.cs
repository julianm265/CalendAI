using CalendarioBackend.Core.Models;
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
    public ActionResult<IReadOnlyList<EquipoResumen>> Listar([FromQuery] string? usuario)
    {
        if (string.IsNullOrWhiteSpace(usuario))
            return BadRequest(new { error = "El nombre de usuario es obligatorio para listar equipos." });

        var equipos = _equipoService.ListarEquipos(usuario)
            .Select(equipo => new EquipoResumen(
                equipo.Id,
                equipo.NombreEquipo,
                equipo.LColaboradores.Count,
                equipo.EsPersonal,
                RolUsuario(equipo, usuario)))
            .ToList();

        return Ok(equipos);
    }

    [HttpGet("{id:guid}")]
    public ActionResult<EquipoDetalle> Obtener(Guid id, [FromQuery] string? usuario)
    {
        try
        {
            var equipo = _equipoService.ObtenerEquipo(id);
            return Ok(new EquipoDetalle(
                equipo.Id,
                equipo.NombreEquipo,
                equipo.LColaboradores
                    .Select(colaborador => new ColaboradorResumen(
                        colaborador.Id,
                        colaborador.Usuario,
                        equipo.ObtenerRolColaborador(colaborador.Id)))
                    .ToList(),
                equipo.EsPersonal,
                RolUsuario(equipo, usuario)));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public ActionResult<EquipoDetalle> Crear(CreateTeamDto request)
    {
        try
        {
            var equipo = _equipoService.CrearEquipo(request.NombreEquipo, request.Usuario);
            var detalle = CrearDetalle(equipo, request.Usuario);
            return CreatedAtAction(
                nameof(Obtener),
                new { id = equipo.Id, usuario = request.Usuario },
                detalle);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
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
    public ActionResult<ColaboradorResumen> AgregarMiembro(
        Guid id,
        AddMemberDto request)
    {
        try
        {
            var colaborador = _equipoService.AgregarMiembro(id, request.Usuario);
            return Ok(new ColaboradorResumen(colaborador.Id, colaborador.Usuario, "Miembro"));
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

    [HttpDelete("{id:guid}/colaboradores/{colaboradorId:guid}")]
    public IActionResult EliminarMiembro(Guid id, Guid colaboradorId)
    {
        try
        {
            _equipoService.EliminarColaborador(id, colaboradorId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    private static string? RolUsuario(Equipo equipo, string? usuario)
    {
        var miembro = string.IsNullOrWhiteSpace(usuario) ? null : equipo.BuscarColaborador(usuario);
        return miembro is null ? null : equipo.ObtenerRolColaborador(miembro.Id);
    }

    private static EquipoDetalle CrearDetalle(Equipo equipo, string? usuario) =>
        new(
            equipo.Id,
            equipo.NombreEquipo,
            equipo.LColaboradores
                .Select(colaborador => new ColaboradorResumen(
                    colaborador.Id,
                    colaborador.Usuario,
                    equipo.ObtenerRolColaborador(colaborador.Id)))
                .ToList(),
            equipo.EsPersonal,
            RolUsuario(equipo, usuario));
}

public sealed record CreateTeamDto(string NombreEquipo, string Usuario);
public sealed record AddMemberDto(string Usuario);
public sealed record EquipoResumen(Guid Id, string Nombre, int Colaboradores, bool EsPersonal, string? RolUsuario);
public sealed record EquipoDetalle(
    Guid Id,
    string Nombre,
    IReadOnlyList<ColaboradorResumen> Colaboradores,
    bool EsPersonal,
    string? RolUsuario);
public sealed record ColaboradorResumen(Guid Id, string Usuario, string Rol = "Miembro");
