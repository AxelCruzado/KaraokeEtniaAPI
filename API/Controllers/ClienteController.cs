using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KaraokeEtniaApi.Domain.Entities;
using KaraokeEtniaApi.Infrastructure.Data;
using KaraokeEtniaApi.Application.DTOs;
using Microsoft.AspNetCore.SignalR;
using KaraokeEtniaApi.API.Hubs;

namespace KaraokeEtniaApi.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<PedidoHub> _hubContext;

        public ClientesController(AppDbContext context, IHubContext<PedidoHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpGet("mesa/{id}")]
        public async Task<ActionResult<object>> GetEstadoMesa(int id)
        {
            var mesa = await _context.Mesas
                .Include(m => m.PedidosActivos)
                .ThenInclude(pa => pa.Detalles)
                .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (mesa == null)
                return NotFound(new { Message = "Mesa no encontrada." });

            var pedidosActivos = mesa.PedidosActivos.Select(pa => new
            {
                Id = pa.Id,
                Total = pa.Total,
                Detalles = pa.Detalles.Select(d => new
                {
                    ProductoNombre = d.Producto.Nombre,
                    Cantidad = d.Cantidad,
                    Total = d.Total
                }).ToList()
            }).ToList();

            var estadoMesa = new
            {
                Saldo = mesa.Saldo,
                PedidosActivos = pedidosActivos,
                TicketsDisponibles = mesa.TicketsDisponibles
            };

            return Ok(estadoMesa);
        }

        [HttpPost("mesa/{id}/solicitar-cancion")]
        public async Task<ActionResult> SolicitarCancion(int id, [FromBody] SolicitudCancionRequestDto solicitud)
        {
            var mesa = await _context.Mesas
                .Include(m => m.PedidosActivos)
                .ThenInclude(pa => pa.Detalles)
                .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (mesa == null)
                return NotFound(new { Message = "Mesa no encontrada." });

            if (mesa.TicketsDisponibles <= 0)
                return BadRequest(new { Message = "No tienes tickets disponibles. Por favor, consume más para obtener tickets." });

            var tipoSolicitud = Enum.TryParse<TipoSolicitud>(solicitud.Tipo, true, out var parsedTipo) ? parsedTipo : TipoSolicitud.Escuchar;

            var nuevaSolicitud = new SolicitudCancion
            {
                MesaId = id,
                Titulo = solicitud.Titulo,
                Artista = solicitud.Artista,
                Tipo = tipoSolicitud,
                Estado = EstadoSolicitud.Pendiente
            };

            _context.SolicitudesCanciones.Add(nuevaSolicitud);
            mesa.TicketsDisponibles--; // Reducir un ticket
            await _context.SaveChangesAsync();

            // Notificar al interfaz de música usando el grupo "Musica"
            var mensajeMusica = $"{solicitud.Titulo} - {solicitud.Artista} (Mesa {id}, Tipo: {tipoSolicitud})";
            await _hubContext.Clients.Group("Musica").SendAsync("NuevaCancion", mensajeMusica);

            // Notificar a los clientes sobre la actualización de la mesa
            await _hubContext.Clients.All.SendAsync("MesaActualizada", id, "Tickets actualizados");

            return Ok(new { Message = "Solicitud de canción enviada exitosamente.", TicketsRestantes = mesa.TicketsDisponibles });
        }
    }
}
