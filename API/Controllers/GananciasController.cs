using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KaraokeEtniaApi.Infrastructure.Data;

namespace KaraokeEtniaApi.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GananciasController : ControllerBase
    {
        private readonly AppDbContext _context;

        public GananciasController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("dia")]
        public async Task<ActionResult<object>> GetGananciaDia([FromQuery] int year, [FromQuery] int month, [FromQuery] int day)
        {
            try
            {
                var fechaInicio = new DateTime(year, month, day, 20, 0, 0); // 20:00 del día
                var fechaFin = fechaInicio.AddDays(1).Date.AddHours(7); // 07:00 del día siguiente

                var totalVentas = await _context.PedidosHistorial
                    .Where(p => p.CerradoEn >= fechaInicio && p.CerradoEn < fechaFin)
                    .SumAsync(p => (decimal?)p.Total) ?? 0;

                var gastos = await _context.Gastos
                    .Where(g => g.Fecha.Date == new DateTime(year, month, day).Date)
                    .Select(g => new { g.Descripcion, g.Monto })
                    .ToListAsync();

                var totalGastos = gastos.Sum(g => g.Monto);
                var gananciaTotal = totalVentas - totalGastos;

                var data = new
                {
                    ventaTotal = totalVentas,
                    gastos = gastos,
                    totalGastos = totalGastos,
                    gananciaTotal = gananciaTotal
                };

                return Ok(data);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = $"Error al obtener ganancias del día: {ex.Message}" });
            }
        }

        [HttpGet("mes")]
        public async Task<ActionResult<object>> GetGananciaMes([FromQuery] int year, [FromQuery] int month)
        {
            try
            {
                var fechaInicio = new DateTime(year, month, 1, 20, 0, 0);
                var fechaFin = fechaInicio.AddMonths(1).Date.AddHours(7);

                var totalVentas = await _context.PedidosHistorial
                    .Where(p => p.CerradoEn >= fechaInicio && p.CerradoEn < fechaFin)
                    .SumAsync(p => (decimal?)p.Total) ?? 0;

                var gastos = await _context.Gastos
                    .Where(g => g.Fecha >= fechaInicio && g.Fecha < fechaFin)
                    .Select(g => new { g.Descripcion, g.Monto })
                    .ToListAsync();

                var totalGastos = gastos.Sum(g => g.Monto);
                var gananciaTotal = totalVentas - totalGastos;

                var data = new
                {
                    ventaTotalMes = totalVentas,
                    gastos = gastos,
                    totalGastos = totalGastos,
                    gananciaTotal = gananciaTotal
                };

                return Ok(data);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = $"Error al obtener ganancias del mes: {ex.Message}" });
            }
        }
    }
}
