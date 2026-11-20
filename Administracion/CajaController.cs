using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace KaraokeEtniaApi.Administracion;
public class CajaExceptionFilter(ILogger<CajaExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext c)
    {
        int status = c.Exception is CajaError e ? e.Status : c.Exception is DbUpdateException ? 409 : 500;
        string message = c.Exception is CajaError ce ? ce.Message : status == 409 ? "Conflicto de integridad; actualice los datos. No se duplicó el registro." : "No se pudo completar la operación de caja.";
        if (status == 500) logger.LogError(c.Exception, "Error de caja");
        c.Result = new ObjectResult(new { message }) { StatusCode = status }; c.ExceptionHandled = true;
    }
}
[ApiController, Route("api/caja"), Authorize(Roles = "Administrador,Cajero"), TypeFilter(typeof(CajaExceptionFilter))]
public class CajaController(CajaDbContext db, CajaService service, IWebHostEnvironment env, IConfiguration config) : ControllerBase
{
    string Who => User.Identity?.Name ?? throw new CajaError("Usuario no identificado.", 401);
    IActionResult Denied(string operation) => StatusCode(403, new { message = "No tiene permisos para " + operation + ". La operación fue rechazada y los datos permanecen sin cambios." });
    [HttpGet("sesion")]
    public IActionResult Sesion() => Ok(new { usuario = Who, rol = User.FindFirstValue(ClaimTypes.Role), datosPrueba = env.IsDevelopment() && config.GetValue<bool>("Caja:DatosPrueba"), zonaHoraria = "America/Lima" });
    [HttpGet("turnos")]
    public async Task<IActionResult> Turnos(DateTime? desde = null, DateTime? hasta = null)
    {
        if (desde > hasta) throw new CajaError("Rango inválido.");
        var q = db.Turnos.AsNoTracking().AsQueryable();
        if (desde != null) q = q.Where(t => t.Inicio >= desde.Value.Date);
        if (hasta != null) { var fin = hasta.Value.Date.AddDays(1); q = q.Where(t => t.Inicio < fin); }
        return Ok(await q.OrderByDescending(t => t.AperturaRegistradaUtc).Take(200).ToListAsync());
    }
    [HttpPost("turnos")]
    public async Task<IActionResult> Abrir(AperturaInput i) => Ok(await service.Apertura(i, Who));
    [HttpGet("turnos/{id}")]
    public async Task<IActionResult> Detalle(string id) => Ok(await service.Detail(id));
    [HttpGet("movimientos")]
    public async Task<IActionResult> Movimientos(string? turnoId = null, DateTime? desdeTurno = null, DateTime? hastaTurno = null, string? tipo = null, string? categoria = null, string? medio = null, string? usuario = null, DateTime? desdeOperacion = null, DateTime? hastaOperacion = null)
    {
        if (desdeTurno > hastaTurno || desdeOperacion > hastaOperacion) throw new CajaError("Rango inválido.");
        var q = db.Movimientos.AsNoTracking().Include(m => m.Pagos).AsQueryable();
        if (turnoId != null) q = q.Where(m => m.TurnoId == turnoId);
        if (desdeTurno != null) q = q.Where(m => db.Turnos.Any(t => t.Id == m.TurnoId && t.Inicio >= desdeTurno.Value.Date));
        if (hastaTurno != null) { var end = hastaTurno.Value.Date.AddDays(1); q = q.Where(m => db.Turnos.Any(t => t.Id == m.TurnoId && t.Inicio < end)); }
        if (tipo != null) q = q.Where(m => m.Tipo == tipo);
        if (categoria != null) q = q.Where(m => m.Categoria == categoria);
        if (medio != null) q = q.Where(m => m.Pagos.Any(p => p.Medio == medio));
        if (usuario != null) q = q.Where(m => m.Usuario == usuario);
        if (desdeOperacion != null) q = q.Where(m => m.FechaOperacion >= desdeOperacion);
        if (hastaOperacion != null) q = q.Where(m => m.FechaOperacion < hastaOperacion);
        return Ok(await q.OrderBy(m => m.FechaOperacion).ToListAsync());
    }
    [HttpGet("pedidos-pendientes")]
    public async Task<IActionResult> Pedidos([FromServices] KaraokeEtniaApi.Infrastructure.Data.AppDbContext operaciones)
    {
        var vinculados = await db.Ventas.Where(v => v.PedidoHistorialId != null).Select(v => v.PedidoHistorialId!.Value).ToListAsync();
        return Ok(await operaciones.PedidosHistorial.AsNoTracking().Where(p => p.Total > 0 && !vinculados.Contains(p.Id)).OrderByDescending(p => p.CerradoEn).Take(200).Select(p => new { p.Id, p.MesaId, p.Total, p.CerradoEn, p.MetodoPago }).ToListAsync());
    }
    [HttpPost("ventas")]
    public async Task<IActionResult> Venta(VentaInput i) => Ok(await service.Venta(i));
    [HttpPost("turnos/{id}/movimientos")]
    public async Task<IActionResult> Movimiento(string id, MovimientoInput i) => Ok(await service.Registrar(id, i, Who));
    [HttpPost("turnos/{id}/movimientos/{movimientoId}/corregir")]
    public async Task<IActionResult> Corregir(string id, string movimientoId, CorreccionInput i) => !User.IsInRole("Administrador") ? Denied("corregir movimientos") : Ok(await service.Corregir(id, movimientoId, i, Who));
    [HttpPost("turnos/{id}/movimientos/{movimientoId}/anulacion")]
    public async Task<IActionResult> Anular(string id, string movimientoId, AnulacionInput i) => !User.IsInRole("Administrador") ? Denied("anular o revertir movimientos") : Ok(await service.Anular(id, movimientoId, i, Who));
    [HttpPost("turnos/{id}/cierres")]
    public async Task<IActionResult> Cerrar(string id, CierreInput i) => Ok(await service.Cerrar(id, i, Who));
    [HttpPost("turnos/{id}/reabrir")]
    public async Task<IActionResult> Reabrir(string id, MotivoInput i) => !User.IsInRole("Administrador") ? Denied("reabrir este cierre") : Ok(await service.Reabrir(id, i, Who));
    [HttpPost("turnos/{id}/reportes")]
    public async Task<IActionResult> Reportar(string id) => Ok(await service.Reportar(id, Who));
    [HttpGet("reportes/{id}")]
    public async Task<IActionResult> Reporte(string id) => Ok(await db.Reportes.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id) ?? throw new CajaError("Reporte no encontrado.", 404));
    static string Cell(string value)
    {
        if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@')) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
    [HttpGet("reportes/{id}/exportar")]
    public async Task<IActionResult> Exportar(string id, string formato = "json")
    {
        var r = await db.Reportes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id) ?? throw new CajaError("Reporte no encontrado.", 404);
        var s = JsonSerializer.Deserialize<JsonElement>(r.SnapshotJson);
        var envelope = new { r.Id, r.Codigo, r.Version, r.Usuario, r.GeneradoUtc, datos = s, fuente = "Registros persistidos; versión conservada" };
        if (formato == "json") return File(Encoding.UTF8.GetBytes(CajaService.Serialize(envelope)), "application/json", r.Codigo + "-v" + r.Version + ".json");
        if (formato != "csv") throw new CajaError("Formato permitido: json o csv.");
        var lines = new List<string>();
        void Row(params string[] cells) => lines.Add(string.Join(",", cells.Select(Cell)));
        Row("Reporte", r.Codigo, "Versión", r.Version.ToString(), "Usuario", r.Usuario, "Generado UTC", r.GeneradoUtc.ToString("O"));
        Row("Filtros", s.GetProperty("filtros").ToString());
        foreach (var name in new[] { "cantidadMovimientos", "ingresos", "egresos", "saldoMovimientos", "efectivoEsperado", "cobrosPorMedio", "egresosPorCategoria", "egresosPorNaturaleza" }) Row(name, s.GetProperty(name).ToString());
        Row("Código", "Tipo", "Naturaleza", "Concepto", "Categoría", "Documento", "Importe", "Fecha operación Lima", "Responsable", "Medios", "Anulado", "Versión");
        foreach (var m in s.GetProperty("movimientos").EnumerateArray()) Row(new[] { "codigo", "tipo", "naturaleza", "concepto", "categoria", "documento", "importe", "fechaOperacion", "usuario", "pagos", "anulado", "version" }.Select(k => m.GetProperty(k).ToString()).ToArray());
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(string.Join("\r\n", lines))).ToArray(), "text/csv; charset=utf-8", r.Codigo + "-v" + r.Version + ".csv");
    }
}
