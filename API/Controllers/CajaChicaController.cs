using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KaraokeEtniaApi.Infrastructure.Data;
using KaraokeEtniaApi.Application.DTOs;
using KaraokeEtniaApi.Domain.Entities;

namespace KaraokeEtniaApi.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CajaChicaController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CajaChicaController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("resumen")]
        public async Task<ActionResult<object>> GetResumenCajaChica([FromQuery] int year, [FromQuery] int month)
        {
            try
            {
                var inicioMes = new DateTime(year, month, 1, 20, 0, 0);
                var finMes = inicioMes.AddMonths(1).Date.AddHours(7);

                var totalVentas = await _context.PedidosHistorial
                    .Where(p => p.CerradoEn >= inicioMes && p.CerradoEn < finMes)
                    .SumAsync(p => (decimal?)p.Total) ?? 0;

                var gastos = await _context.Gastos
                    .Where(g => g.Fecha >= inicioMes.Date && g.Fecha < finMes.Date)
                    .Select(g => new { Total = g.Monto, Descripcion = g.Descripcion, Fecha = g.Fecha })
                    .ToListAsync();
                
                var totalGastos = gastos.Sum(g => g.Total);

                var saldoInicial = 0m; 
                var saldoFinal = saldoInicial + totalVentas - totalGastos;

                var resumen = new
                {
                    SaldoInicial = saldoInicial,
                    Ingresos = totalVentas,
                    Gastos = gastos,
                    TotalGastos = totalGastos,
                    SaldoFinal = saldoFinal
                };

                return Ok(resumen);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = $"Error al obtener resumen de caja chica: {ex.Message}" });
            }
        }

        [HttpPost("registrar-gasto")]
        public async Task<ActionResult> RegistrarGasto([FromBody] GastoRequestDto gasto)
        {
            var nuevoGasto = new Gasto
            {
                Monto = gasto.Monto,
                Descripcion = gasto.Descripcion,
                Fecha = DateTime.SpecifyKind(gasto.Fecha, DateTimeKind.Utc), // Usar la fecha proporcionada
                Tipo = gasto.Tipo
            };

            _context.Gastos.Add(nuevoGasto);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Gasto registrado exitosamente." });
        }
    }
}
