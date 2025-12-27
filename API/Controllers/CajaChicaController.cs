using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KaraokeNoeliasApi.Infrastructure.Data;
using KaraokeNoeliasApi.Application.DTOs;
using KaraokeNoeliasApi.Domain.Entities;

namespace KaraokeNoeliasApi.API.Controllers
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
            var inicioMes = new DateTime(year, month, 1, 20, 0, 0);
            var finMes = new DateTime(year, month + 1, 1, 7, 0, 0);

            var totalVentas = await _context.PedidosHistorial
                .Where(p => p.CerradoEn >= inicioMes && p.CerradoEn < finMes)
                .SumAsync(p => (decimal?)p.Total) ?? 0;

            var gastos = await _context.Gastos
                .Where(g => g.Fecha.Date >= inicioMes.Date && g.Fecha.Date < finMes.Date)
                .Select(g => new { Total = g.Monto, Descripcion = g.Descripcion, Fecha = g.Fecha })
                .ToListAsync();
            var totalGastos = gastos.Sum(g => g.Total);

            var saldoInicial = 0m; // Ajusta según tu lógica
            var saldoFinal = saldoInicial + totalVentas - totalGastos;

            var resumen = new
            {
                SaldoInicial = saldoInicial,
                Ingresos = totalVentas,
                Gastos = gastos,
                SaldoFinal = saldoFinal
            };

            return Ok(resumen);
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