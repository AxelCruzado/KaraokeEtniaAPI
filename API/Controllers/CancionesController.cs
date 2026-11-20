using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KaraokeEtniaApi.Infrastructure.Data;
using KaraokeEtniaApi.Domain.Entities;
using Microsoft.AspNetCore.SignalR;
using KaraokeEtniaApi.API.Hubs;

namespace KaraokeEtniaApi.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Administrador,Cajero,Mesera,Musica")]
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
                .OrderBy(sc => sc.Id)
                .Select(sc => new
                {
                    sc.Id,
                    sc.Titulo,
                    sc.Artista,
                    sc.MesaId,
                    MesaNumero = sc.Mesa.Numero,
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
                    MesaNumero = sc.Mesa.Numero,
                    sc.Tipo,
                    sc.Estado
                })
                .ToListAsync();

            return Ok(canciones);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles="Administrador,Musica")]
    [HttpPost("{id}/actualizar-estado")]
        public async Task<ActionResult> ActualizarEstado(int id, [FromBody] string estado)
        {
            var solicitud = await _context.SolicitudesCanciones.FindAsync(id);
            if (solicitud == null)
                return NotFound(new { Message = "Solicitud no encontrada." });

            Console.WriteLine($"Estado recibido: {estado}"); // Depuración

            if (!int.TryParse(estado, out int parsedEstado) || !Enum.IsDefined(typeof(EstadoSolicitud), parsedEstado))
                return BadRequest(new { Message = "Estado inválido." });

            // Publish after persistence so connected screens read the committed state.
            var anteriores = new List<SolicitudCancion>();
            if (parsedEstado == (int)EstadoSolicitud.EnReproduccion)
            {
                anteriores = await _context.SolicitudesCanciones
                    .Where(sc => sc.Estado == EstadoSolicitud.EnReproduccion && sc.Id != id)
                    .ToListAsync();
                foreach (var cancion in anteriores)
                    cancion.Estado = EstadoSolicitud.Completada;
            }

            solicitud.Estado = (EstadoSolicitud)parsedEstado;
            await _context.SaveChangesAsync();

            foreach (var cancion in anteriores)
            {
                await _hubContext.Clients.Group($"Mesa_{cancion.MesaId}").SendAsync("SolicitudActualizada", $"Solicitud {cancion.Id} completada");
                await _hubContext.Clients.Group("Musica").SendAsync("EstadoCancionActualizado", cancion.Id, ((int)cancion.Estado).ToString());
            }

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
