using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KaraokeNoeliasApi.Infrastructure.Data;
using KaraokeNoeliasApi.Domain.Entities;
using Microsoft.AspNetCore.SignalR;
using KaraokeNoeliasApi.API.Hubs;

namespace KaraokeNoeliasApi.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CancionesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<PedidoHub> _hubContext;

        public CancionesController(AppDbContext context, IHubContext<PedidoHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpGet("pendientes")]
        public async Task<ActionResult<IEnumerable<object>>> GetCancionesPendientes()
        {
            var canciones = await _context.SolicitudesCanciones
                .Where(sc => sc.Estado == EstadoSolicitud.Pendiente || sc.Estado == EstadoSolicitud.EnReproduccion)
                .Select(sc => new
                {
                    sc.Id,
                    sc.Titulo,
                    sc.Artista,
                    sc.MesaId,
                    sc.Tipo,
                    sc.Estado
                })
                .ToListAsync();

            return Ok(canciones);
        }

        [HttpGet("mesa/{mesaId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetCancionesPorMesa(int mesaId)
        {
            var canciones = await _context.SolicitudesCanciones
                .Where(sc => sc.MesaId == mesaId)
                .Select(sc => new
                {
                    sc.Id,
                    sc.Titulo,
                    sc.Artista,
                    sc.MesaId,
                    sc.Tipo,
                    sc.Estado
                })
                .ToListAsync();

            return Ok(canciones);
        }

        [HttpPost("{id}/actualizar-estado")]
        public async Task<ActionResult> ActualizarEstado(int id, [FromBody] string estado)
        {
            var solicitud = await _context.SolicitudesCanciones.FindAsync(id);
            if (solicitud == null)
                return NotFound(new { Message = "Solicitud no encontrada." });

            Console.WriteLine($"Estado recibido: {estado}"); // Depuración

            if (!int.TryParse(estado, out int parsedEstado) || !Enum.IsDefined(typeof(EstadoSolicitud), parsedEstado))
                return BadRequest(new { Message = "Estado inválido." });

            // Si el nuevo estado es 1 (EnReproduccion), marcamos todas las canciones en 1 (EnReproduccion) como 2 (Completada)
            if (parsedEstado == (int)EstadoSolicitud.EnReproduccion)
            {
                var cancionesEnReproduccion = await _context.SolicitudesCanciones
                    .Where(sc => sc.Estado == EstadoSolicitud.EnReproduccion)
                    .ToListAsync();

                foreach (var cancion in cancionesEnReproduccion)
                {
                    cancion.Estado = EstadoSolicitud.Completada;
                    // Enviar notificación a la mesa afectada por la canción completada
                    await _hubContext.Clients.Group($"Mesa_{cancion.MesaId}").SendAsync("SolicitudActualizada", $"Solicitud {cancion.Id} completada");
                    await _hubContext.Clients.Group("Musica").SendAsync("EstadoCancionActualizado", cancion.Id, ((int)cancion.Estado).ToString());
                    Console.WriteLine($"Notificando completada para canción {cancion.Id}, Mesa {cancion.MesaId}");
                }
            }

            solicitud.Estado = (EstadoSolicitud)parsedEstado;
            await _context.SaveChangesAsync();

            // Notificar al grupo "Musica" sobre el cambio de estado
            await _hubContext.Clients.Group("Musica").SendAsync("EstadoCancionActualizado", id, parsedEstado.ToString());
            Console.WriteLine($"Notificando EnReproduccion para canción {id}");

            // Notificar a la mesa cuya canción está en reproducción
            await _hubContext.Clients.Group($"Mesa_{solicitud.MesaId}").SendAsync("SolicitudActualizada", $"Solicitud {id} actualizada a {parsedEstado}");
            Console.WriteLine($"Notificando mesa {solicitud.MesaId} sobre actualización");

            return Ok(new { Message = "Estado de la canción actualizado." });
        }
    }
}