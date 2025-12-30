// Services/PedidoService.cs
using KaraokeEtniaApi.Domain.Entities;
using KaraokeEtniaApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using KaraokeEtniaApi.API.Hubs;
using KaraokeEtniaApi.Domain.Interfaces;

namespace KaraokeEtniaApi.Infrastructure.Services;

public class PedidoService : IPedidoService
{
    private readonly AppDbContext _context;
    private readonly IHubContext<PedidoHub> _hubContext;

    public PedidoService(AppDbContext context, IHubContext<PedidoHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    public async Task RegistrarPedidoAsync(int mesaId, int productoId, int cantidad)
    {
        var mesa = await _context.Mesas.FindAsync(mesaId);
        if (mesa == null)
            throw new Exception("Mesa no encontrada.");

        var producto = await _context.Productos.FindAsync(productoId);
        if (producto == null)
            throw new Exception("Producto no encontrado.");

        if (producto.Stock < cantidad)
            throw new Exception("Stock insuficiente.");

        var total = producto.Precio * cantidad;

        // Validar saldo antes de registrar
        if (mesa.Saldo < total)
            throw new Exception("Saldo insuficiente. Por favor, recargue la mesa.");

        var pedido = new PedidoActivo
        {
            MesaId = mesaId,
            
            Total = total
        };

        _context.PedidosActivos.Add(pedido);

        // Actualizar stock
        producto.Stock -= cantidad;
        // Actualizar tickets (si aplica)
        if (producto.TicketsPorUnidad > 0)
        {
            mesa.TicketsDisponibles += cantidad * producto.TicketsPorUnidad;
        }
        // Actualizar saldo (restar el total)
        mesa.Saldo -= total;

        await _context.SaveChangesAsync();

        // Enviar notificación al administrador
        var mensaje = $"Nuevo pedido en Mesa {mesaId}: {cantidad} x {producto.Nombre} (Total: {total} S/.)";
        await _hubContext.Clients.Group("Administradores").SendAsync("RecibirNotificacion", mesaId, mensaje);
    }

    public async Task<List<PedidoActivo>> ObtenerPedidosPorMesaAsync(int mesaId)
    {
        return await _context.PedidosActivos
            .Where(p => p.MesaId == mesaId)
            .Include(p => p.Detalles)
            .ThenInclude(d => d.Producto)
            .ToListAsync();
    }
}
