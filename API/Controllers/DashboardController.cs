using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KaraokeNoeliasApi.Infrastructure.Data;
using System.Linq;
using KaraokeNoeliasApi.Domain.Entities;
using Microsoft.Extensions.Configuration;
using System;
using KaraokeNoeliasApi.Application.DTOs;

namespace KaraokeNoeliasApi.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;


    public DashboardController(AppDbContext context, IWebHostEnvironment environment, IConfiguration configuration)
    {
        _context = context;
        _environment = environment;
        _configuration = configuration;
    }
    private string GetImagePath() => Path.Combine(_environment.WebRootPath, _configuration["Storage:ImagePath"]);

    

    [HttpGet("movimientos-actuales")]
    public async Task<IActionResult> GetMovimientosActuales()
    {
        var mesasOcupadas = await _context.Mesas.CountAsync(m => m.Estado == EstadoMesa.Ocupado);
        var ventasActuales = await _context.PedidosActivos.SumAsync(p => (decimal?)p.Total) ?? 0;
        var ticketsDisponiblesTotales = await _context.Mesas.SumAsync(m => (int?)m.TicketsDisponibles) ?? 0;
        var pedidosActivos = await _context.PedidosActivos.CountAsync();

        var movimientos = new
        {
            mesasOcupadas,
            ventasActuales,
            ticketsDisponiblesTotales,
            pedidosActivos
        };

        return Ok(movimientos);
    }

    [HttpGet("reportes/diarios")]
    public async Task<IActionResult> GetReportesDiarios()
    {
        var reportes = await _context.PedidosHistorial
            .GroupBy(p => p.CerradoEn.Date)
            .Select(g => new
            {
                fecha = g.Key.ToString("yyyy-MM-dd"),
                total = g.Sum(p => p.Total)
            })
            .OrderBy(r => r.fecha)
            .ToListAsync();

        return Ok(reportes);
    }

    [HttpGet("reportes/semanales")]
    public async Task<IActionResult> GetReportesSemanales()
    {
        var reportes = await _context.PedidosHistorial
            .GroupBy(p => new { p.CerradoEn.Year, p.CerradoEn.Month, Week = (p.CerradoEn.Day - 1) / 7 })
            .Select(g => new
            {
                semana = $"{g.Key.Year}-Semana {(g.Key.Week + 1)}",
                total = g.Sum(p => p.Total)
            })
            .OrderBy(r => r.semana)
            .ToListAsync();

        return Ok(reportes);
    }

    [HttpGet("reportes/mensuales")]
    public async Task<IActionResult> GetReportesMensuales()
    {
        var reportes = await _context.PedidosHistorial
            .GroupBy(p => new { p.CerradoEn.Year, p.CerradoEn.Month })
            .Select(g => new
            {
                mes = $"{g.Key.Year}-{g.Key.Month:D2}",
                total = g.Sum(p => p.Total)
            })
            .OrderBy(r => r.mes)
            .ToListAsync();

        return Ok(reportes);
    }
}