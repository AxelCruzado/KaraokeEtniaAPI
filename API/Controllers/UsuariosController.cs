using KaraokeEtniaApi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KaraokeEtniaApi.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Administrador")]
public class UsuariosController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsuariosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsuarios()
    {
        try
        {
            var usuarios = await _context.Usuarios
                .Select(u => new
                {
                    u.Id,
                    u.NombreUsuario,
                    u.Rol,
                    u.FechaRegistro
                })
                .ToListAsync();
            return Ok(usuarios);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Error al obtener usuarios: {ex.Message}" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> EliminarUsuario(int id)
    {
        try
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null) return NotFound();

            if (usuario.NombreUsuario == "admin")
            {
                return BadRequest(new { message = "No se puede eliminar el usuario administrador principal." });
            }

            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Usuario eliminado." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Error al eliminar usuario: {ex.Message}" });
        }
    }
}
