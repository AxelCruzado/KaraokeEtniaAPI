using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KaraokeNoeliasApi.Infrastructure.Data;
using KaraokeNoeliasApi.Application.DTOs;

namespace KaraokeNoeliasApi.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReportesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/reportes/mensual?year=2025&month=5
        [HttpGet("mensual")]
        public async Task<ActionResult<ReporteMensualDto>> GetReporteMensual([FromQuery] int year, [FromQuery] int month)
        {
            var inicioMes = new DateTime(year, month, 1, 20, 0, 0); // 20:00 del primer día
            var finMes = new DateTime(year, month + 1, 1, 7, 0, 0); // 07:00 del primer día del siguiente mes

            var totalMes = await _context.PedidosHistorial
                .Where(p => p.CerradoEn >= inicioMes && p.CerradoEn < finMes)
                .SumAsync(p => (decimal?)p.Total) ?? 0;

            var reporte = new ReporteMensualDto
            {
                TotalMes = totalMes
            };

            return Ok(reporte);
        }

        // GET: api/reportes/diario?year=2025&month=5&day=10
        [HttpGet("diario")]
        public async Task<ActionResult<ReporteDiarioDto>> GetReporteDiario([FromQuery] int year, [FromQuery] int month, [FromQuery] int day)
        {
            var fechaDia = new DateTime(year, month, day, 20, 0, 0); // 20:00 del día seleccionado
            var inicioDia = fechaDia;
            var finDia = fechaDia.AddDays(1).AddHours(-13); // 07:00 del día siguiente

            // Ventas del día (usando CerradoEn para pedidos históricos)
            var pedidosDelDia = await _context.PedidosHistorial
                .Where(p => p.CerradoEn >= inicioDia && p.CerradoEn < finDia)
                .ToListAsync();

            var ventaTotal = pedidosDelDia.Sum(p => (decimal?)p.Total) ?? 0;
            var efectivo = pedidosDelDia.Where(p => p.MetodoPago == "Efectivo").Sum(p => (decimal?)p.Total) ?? 0;
            var yape = pedidosDelDia.Where(p => p.MetodoPago == "Yape").Sum(p => (decimal?)p.Total) ?? 0;

            // Productos vendidos
            var productosVendidos = await _context.PedidoDetallesHistorial
                .Include(pd => pd.Producto)
                .Where(pd => pd.PedidoHistorial.CerradoEn >= inicioDia && pd.PedidoHistorial.CerradoEn < finDia)
                .GroupBy(pd => new { pd.ProductoId, pd.Producto.Nombre })
                .Select(g => new ProductoVendidoDto
                {
                    NombreProducto = g.Key.Nombre,
                    Cantidad = g.Sum(pd => pd.Cantidad),
                    Total = g.Sum(pd => pd.Total)
                })
                .ToListAsync();

            var reporte = new ReporteDiarioDto
            {
                VentaTotal = ventaTotal,
                Efectivo = efectivo,
                Yape = yape,
                ProductosVendidos = productosVendidos
            };

            return Ok(reporte);
        }
    }
}