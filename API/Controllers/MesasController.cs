// API/Controllers/MesasController.cs
using KaraokeNoeliasApi.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KaraokeNoeliasApi.Domain.Entities;
using Microsoft.AspNetCore.SignalR;
using KaraokeNoeliasApi.API.Hubs;
using KaraokeNoeliasApi.Domain.Interfaces;
using KaraokeNoeliasApi.Application.DTOs;

namespace KaraokeNoeliasApi.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MesasController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IPedidoService _pedidoService;
    private readonly IHubContext<PedidoHub> _hubContext;

    public MesasController(AppDbContext context, IPedidoService pedidoService, IHubContext<PedidoHub> hubContext)
    {
        _context = context;
        _pedidoService = pedidoService;
        _hubContext = hubContext;
    }
    [HttpGet]
    public async Task<IActionResult> GetMesas()
    {
        var mesas = await _context.Mesas
            .Include(m => m.PedidosActivos)
            .ThenInclude(pa => pa.Detalles)
            .ThenInclude(d => d.Producto)
            .OrderBy(m => m.Numero)
            .ToListAsync();

        var mesasDto = mesas.Select(m => new MesaDto
        {
            Id = m.Id,
            Numero = m.Numero,
            Saldo = m.Saldo,
            TicketsDisponibles = m.TicketsDisponibles,
            Estado = m.Estado.ToString(),
            PedidosActivos = m.PedidosActivos.Select(pa => new PedidoActivoDto
            {
                Id = pa.Id,
                MesaId = pa.MesaId,
                Total = pa.Total,
                RegistradoEn = pa.RegistradoEn,
                MetodoPago = pa.MetodoPago,
                PagoCliente = pa.PagoCliente,
                Detalles = pa.Detalles.Select(d => new PedidoDetalleDto
                {
                    Id = d.Id,
                    ProductoId = d.ProductoId,
                    Cantidad = d.Cantidad,
                    Total = d.Total,
                    Producto = new ProductoDto
                    {
                        Id = d.Producto.Id,
                        CategoriaId = d.Producto.CategoriaId,
                        Nombre = d.Producto.Nombre,
                        ImagenUrl = d.Producto.ImagenUrl,
                        Stock = d.Producto.Stock,
                        Precio = d.Producto.Precio,
                        TicketsPorUnidad = d.Producto.TicketsPorUnidad
                    }
                }).ToList()
            }).ToList()
        }).ToList();

        return Ok(mesasDto);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMesa(int id)
    {
        var mesa = await _context.Mesas
            .Include(m => m.PedidosActivos)
            .ThenInclude(pa => pa.Detalles)
            .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (mesa == null)
        {
            return NotFound();
        }

        var mesaDto = new MesaDto
        {
            Id = mesa.Id,
            Numero = mesa.Numero,
            Saldo = mesa.Saldo,
            TicketsDisponibles = mesa.TicketsDisponibles,
            Estado = mesa.Estado.ToString(),
            PedidosActivos = mesa.PedidosActivos.OrderBy(pa => pa.RegistradoEn).Select(pa => new PedidoActivoDto
            {
                Id = pa.Id,
                MesaId = pa.MesaId,
                Total = pa.Total,
                RegistradoEn = pa.RegistradoEn,
                MetodoPago = pa.MetodoPago,
                PagoCliente = pa.PagoCliente,
                Detalles = pa.Detalles.Select(d => new PedidoDetalleDto
                {
                    Id = d.Id,
                    ProductoId = d.ProductoId,
                    Cantidad = d.Cantidad,
                    Total = d.Total,
                    Producto = new ProductoDto
                    {
                        Id = d.Producto.Id,
                        CategoriaId = d.Producto.CategoriaId,
                        Nombre = d.Producto.Nombre,
                        ImagenUrl = d.Producto.ImagenUrl,
                        Stock = d.Producto.Stock,
                        Precio = d.Producto.Precio,
                        TicketsPorUnidad = d.Producto.TicketsPorUnidad
                    }
                }).ToList()
            }).ToList()
        };

        return Ok(mesaDto);
    }



    [HttpGet("{id}/pedidos")]
    public async Task<IActionResult> GetPedidosDeMesa(int id)
    {
        var pedidos = await _pedidoService.ObtenerPedidosPorMesaAsync(id);

        if (pedidos == null || !pedidos.Any())
        {
            return NotFound("No se encontraron pedidos para esta mesa.");
        }

        var pedidosDto = pedidos.Select(pa => new PedidoActivoDto
        {
            Id = pa.Id,
            MesaId = pa.MesaId,
            Total = pa.Total,
            RegistradoEn = pa.RegistradoEn,
            Detalles = pa.Detalles.Select(d => new PedidoDetalleDto
            {
                Id = d.Id,
                ProductoId = d.ProductoId,
                Cantidad = d.Cantidad,
                Total = d.Total,
                Producto = new ProductoDto
                {
                    Id = d.Producto.Id,
                    CategoriaId = d.Producto.CategoriaId,
                    Nombre = d.Producto.Nombre,
                    ImagenUrl = d.Producto.ImagenUrl,
                    Stock = d.Producto.Stock,
                    Precio = d.Producto.Precio,
                    TicketsPorUnidad = d.Producto.TicketsPorUnidad
                }
            }).ToList()
        }).ToList();

        return Ok(pedidosDto);
    }

    [HttpPost("{id}/agregar-tickets")]
    public async Task<IActionResult> AgregarTickets(int id, [FromBody] AgregarTicketsDto request)
    {
        try
        {
            var mesa = await _context.Mesas.FindAsync(id);
            if (mesa == null)
                return NotFound(new { Message = "Mesa no encontrada." });

            if (request.Cantidad <= 0)
                return BadRequest(new { Message = "La cantidad de tickets debe ser mayor a 0." });

            mesa.TicketsDisponibles += request.Cantidad;
            await _context.SaveChangesAsync();

            // Notificar a Administradores y Meseras
            await _hubContext.Clients.Group("Administradores").SendAsync("RecibirNotificacion", id,
                $"Se agregaron {request.Cantidad} tickets a la Mesa {id}.");
            await _hubContext.Clients.Group("Meseras").SendAsync("MesaActualizada", id, mesa.Estado.ToString());

            // Notificar a todos los clientes sobre la actualización de la mesa
            Console.WriteLine($"Enviando MesaActualizada a todos: Mesa {id}, Estado: Tickets actualizados");
            await _hubContext.Clients.All.SendAsync("MesaActualizada", id, "Tickets actualizados");

            return Ok(new { Message = $"Se agregaron {request.Cantidad} tickets a la mesa." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = $"Error al agregar tickets: {ex.Message}" });
        }
    }

    [HttpPost("{id}/pedidos")]
    public async Task<IActionResult> RegistrarPedido(int id, [FromBody] PedidoRequestDto pedidoRequest)
    {
        try
        {
            var mesa = await _context.Mesas.FindAsync(id);
            if (mesa == null)
                return NotFound(new { Message = "Mesa no encontrada." });

            if (pedidoRequest.Detalles == null || !pedidoRequest.Detalles.Any())
                return BadRequest(new { Message = "Debe incluir al menos un producto en el pedido." });

            decimal totalPedido = 0;
            var pedido = new PedidoActivo { MesaId = id };

            foreach (var detalle in pedidoRequest.Detalles)
            {
                var producto = await _context.Productos.FindAsync(detalle.ProductoId);
                if (producto == null)
                    throw new Exception($"Producto con ID {detalle.ProductoId} no encontrado.");
                if (producto.Stock < detalle.Cantidad)
                    throw new Exception($"Stock insuficiente para el producto {producto.Nombre}.");

                var totalDetalle = producto.Precio * detalle.Cantidad;
                var pedidoDetalle = new PedidoDetalle
                {
                    ProductoId = detalle.ProductoId,
                    Cantidad = detalle.Cantidad,
                    Total = totalDetalle
                };
                pedido.Detalles.Add(pedidoDetalle);
                totalPedido += totalDetalle;

                producto.Stock -= detalle.Cantidad;
                if (producto.TicketsPorUnidad > 0)
                {
                    mesa.TicketsDisponibles += detalle.Cantidad * producto.TicketsPorUnidad;
                }
            }

            decimal saldoUtilizado = Math.Min(mesa.Saldo, totalPedido);
            decimal montoRestante = totalPedido - saldoUtilizado;
            decimal montoPagadoTotal = pedidoRequest.PagoCliente;

            if (montoRestante > 0 && montoPagadoTotal < montoRestante && string.IsNullOrEmpty(pedidoRequest.MetodoPago))
                return BadRequest(new { Message = "El monto pagado no cubre el resto del pedido. Por favor, ingrese un pago mayor o use otro método." });

            if (montoRestante <= 0 || (montoPagadoTotal >= montoRestante))
            {
                decimal cambio = (mesa.Saldo + montoPagadoTotal) - totalPedido;
                if (cambio >= 0)
                {
                    mesa.Saldo = cambio;
                }
                else
                {
                    mesa.Saldo = 0;
                    montoPagadoTotal -= montoRestante;
                }
            }
            else
            {
                return BadRequest(new { Message = "El saldo y el monto pagado no son suficientes para cubrir el pedido." });
            }

            if (mesa.Estado == EstadoMesa.Disponible)
            {
                mesa.Estado = EstadoMesa.Ocupado;
            }

            pedido.Total = totalPedido;
            pedido.MetodoPago = pedidoRequest.MetodoPago;
            pedido.PagoCliente = pedidoRequest.PagoCliente;
            _context.PedidosActivos.Add(pedido);
            await _context.SaveChangesAsync();

            var mensajeAdmin = $"Nuevo pedido en Mesa {id}: Total {totalPedido} S/. con {pedidoRequest.Detalles.Count} productos. Método de pago: {pedidoRequest.MetodoPago ?? "Sin especificar"}, Pago: {pedidoRequest.PagoCliente} S/.";
            await _hubContext.Clients.Group("Administradores").SendAsync("RecibirNotificacion", id, mensajeAdmin);

            // Notificar a Meseras
            Console.WriteLine($"Enviando MesaActualizada a Meseras: Mesa {id}, Estado: {mesa.Estado}");
            await _hubContext.Clients.Group("Meseras").SendAsync("MesaActualizada", id, mesa.Estado.ToString());
            Console.WriteLine($"Enviando SolicitudActualizada a Meseras: Mesa {id}, Mensaje: {mensajeAdmin}");
            await _hubContext.Clients.Group("Meseras").SendAsync("SolicitudActualizada", id, mensajeAdmin);

            // Notificar a todos los clientes sobre la actualización de la mesa
            Console.WriteLine($"Enviando MesaActualizada a todos: Mesa {id}, Estado: Pedido registrado");
            await _hubContext.Clients.All.SendAsync("MesaActualizada", id, "Pedido registrado");

            return Ok(new { Message = "Pedido registrado exitosamente.", PedidoId = pedido.Id });
        }
        catch (Exception ex)
        {
            var errorMessage = ex.Message;
            var innerException = ex.InnerException;
            while (innerException != null)
            {
                errorMessage += $" | Inner Exception: {innerException.Message}";
                innerException = innerException.InnerException;
            }
            return BadRequest(new { Message = $"Error al procesar el pedido: {errorMessage}" });
        }
    }

    [HttpPost("{id}/liberar")]
    public async Task<IActionResult> LiberarMesa(int id)
    {
        try
        {
            var mesa = await _context.Mesas
                .Include(m => m.PedidosActivos)
                .ThenInclude(pa => pa.Detalles)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (mesa == null)
                return NotFound(new { Message = "Mesa no encontrada." });

            if (!mesa.PedidosActivos.Any())
                return BadRequest(new { Message = "La mesa no tiene pedidos activos para mover al historial." });

            // Mover pedidos activos al historial
            foreach (var pedidoActivo in mesa.PedidosActivos)
            {
                var pedidoHistorial = new PedidoHistorial
                {
                    MesaId = pedidoActivo.MesaId,
                    Total = pedidoActivo.Total,
                    RegistradoEn = pedidoActivo.RegistradoEn,
                    CerradoEn = DateTime.Now,
                    MetodoPago = pedidoActivo.MetodoPago,
                    PagoCliente = pedidoActivo.PagoCliente,
                    Detalles = pedidoActivo.Detalles.Select(d => new PedidoDetalleHistorial
                    {
                        ProductoId = d.ProductoId,
                        Cantidad = d.Cantidad,
                        Total = d.Total
                    }).ToList()
                };
                _context.PedidosHistorial.Add(pedidoHistorial);
            }

            _context.PedidosActivos.RemoveRange(mesa.PedidosActivos);

            // Actualizar las canciones de la mesa a "Completada"
            var cancionesMesa = await _context.SolicitudesCanciones
                .Where(sc => sc.MesaId == id && (sc.Estado == EstadoSolicitud.Pendiente || sc.Estado == EstadoSolicitud.EnReproduccion))
                .ToListAsync();

            foreach (var cancion in cancionesMesa)
            {
                cancion.Estado = EstadoSolicitud.Completada;
                // Notificar a la interfaz de música sobre el cambio de estado
                await _hubContext.Clients.Group("Musica").SendAsync("EstadoCancionActualizado", cancion.Id, ((int)cancion.Estado).ToString());
                // Notificar a la mesa específica
                await _hubContext.Clients.Group($"Mesa_{cancion.MesaId}").SendAsync("SolicitudActualizada", $"Solicitud {cancion.Id} completada");
                Console.WriteLine($"Canción {cancion.Id} de Mesa {cancion.MesaId} marcada como Completada.");
            }

            // Actualizar el estado de la mesa
            mesa.Estado = EstadoMesa.Disponible;
            mesa.TicketsDisponibles = 0;
            mesa.Saldo = 0;

            await _context.SaveChangesAsync();

            // Notificar a Administradores y Meseras
            var mensaje = $"Mesa {id} ha sido liberada.";
            await _hubContext.Clients.Group("Administradores").SendAsync("RecibirNotificacion", id, mensaje);
            await _hubContext.Clients.Group("Meseras").SendAsync("MesaActualizada", id, mesa.Estado.ToString());

            // Notificar a todos los clientes sobre la actualización de la mesa
            Console.WriteLine($"Enviando MesaActualizada a todos: Mesa {id}, Estado: Mesa liberada");
            await _hubContext.Clients.All.SendAsync("MesaActualizada", id, "Mesa liberada");

            return Ok(new { Message = "Mesa liberada exitosamente. Pedidos movidos al historial y canciones completadas." });
        }
        catch (Exception ex)
        {
            var errorMessage = ex.Message;
            var innerException = ex.InnerException;
            while (innerException != null)
            {
                errorMessage += $" | Inner Exception: {innerException.Message}";
                innerException = innerException.InnerException;
            }
            return BadRequest(new { Message = $"Error al liberar la mesa: {errorMessage}" });
        }
    }
}