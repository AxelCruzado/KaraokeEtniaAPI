using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KaraokeEtniaApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KaraokeEtniaApi.Administracion;
public class CajaError(string message, int status = 400) : Exception(message) { public int Status { get; } = status; }
public record PagoInput(string Medio, decimal Importe);
public record AperturaInput(string Caja, DateTime Fecha, decimal FondoInicial);
public record MovimientoInput(string ClaveOperacion, string Tipo, string Naturaleza, string Concepto, string Categoria, string Documento, decimal Importe, DateTime FechaOperacion, List<PagoInput> Pagos, string? ReferenciaVenta = null, string? VentaId = null, int? PedidoHistorialId = null);
public record CorreccionInput(int VersionEsperada, string Motivo, string Concepto, string Categoria, string Documento, decimal Importe, List<PagoInput> Pagos);
public record CierreInput(string ClaveOperacion, decimal? Contado);
public record MotivoInput(string Motivo);
public record AnulacionInput(string Motivo, bool Anulado, int VersionEsperada);
public record VentaInput(string Referencia, decimal Total, int? PedidoHistorialId);

public class CajaService(CajaDbContext db, AppDbContext operaciones)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string Serialize(object value) => JsonSerializer.Serialize(value, Json);
    public static string Code(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    static string Text(string? value, string field, int max = 200)
    {
        var v = value?.Trim() ?? "";
        if (v.Length == 0 || v.Length > max) throw new CajaError($"{field}: complete entre 1 y {max} caracteres.");
        return v;
    }
    static void Money(decimal amount, bool zero = false)
    {
        if (amount < 0 || (!zero && amount == 0) || amount > 99999999999.99m || decimal.Round(amount, 2) != amount)
            throw new CajaError("Importe inválido; use como máximo dos decimales.");
    }
    static void Payments(List<PagoInput>? payments, decimal amount)
    {
        Money(amount);
        if (payments is null || payments.Count == 0 || payments.Count > 3 || payments.Any(p => p == null) || payments.Select(p => p.Medio).Distinct().Count() != payments.Count)
            throw new CajaError("Indique cada medio de pago una sola vez.");
        foreach (var p in payments)
        {
            if (!new[] { "Efectivo", "Yape", "Tarjeta" }.Contains(p.Medio)) throw new CajaError("Medio de pago inválido.");
            Money(p.Importe);
        }
        if (payments.Sum(p => p.Importe) != amount) throw new CajaError("Los componentes no suman el importe total.");
    }
    static void Open(TurnoCaja t) { if (t.Estado != "Abierto") throw new CajaError("El turno está cerrado; requiere reapertura autorizada con motivo.", 409); }
    static void Category(string type, string nature, string category)
    {
        if (type == "Ingreso" && nature != "Venta cobrada") throw new CajaError("Solo se habilitan ingresos de ventas cobradas.");
        if (type == "Egreso" && (!new[] { "Gasto", "Retiro", "Devolución" }.Contains(nature) || !new[] { "Insumos", "Servicios", "Otros" }.Contains(category))) throw new CajaError("Naturaleza o categoría inválida.");
        if (!new[] { "Ingreso", "Egreso" }.Contains(type)) throw new CajaError("Tipo de movimiento inválido.");
    }
    async Task<T> Locked<T>(string id, Func<TurnoCaja, Task<T>> action)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var turns = await db.Turnos.FromSqlInterpolated($"SELECT * FROM Caja_TurnoCaja WHERE Id = {id} FOR UPDATE").ToListAsync();
        var turn = turns.SingleOrDefault() ?? throw new CajaError("Turno no encontrado.", 404);
        var result = await action(turn);
        await db.SaveChangesAsync(); await tx.CommitAsync(); return result;
    }
    void Audit(TurnoCaja t, string code, string action, string who, string reason, object before, object after) => db.Auditorias.Add(new() { TurnoId = t.Id, EntidadCodigo = code, Accion = action, Usuario = who, Motivo = reason, AnteriorJson = Serialize(before), PosteriorJson = Serialize(after) });
    public async Task<TurnoCaja> Apertura(AperturaInput i, string user)
    {
        var caja = Text(i.Caja, "Caja", 40).ToUpperInvariant(); Money(i.FondoInicial, true);
        if (i.Fecha.TimeOfDay != TimeSpan.Zero || i.Fecha.DayOfWeek == DayOfWeek.Monday) throw new CajaError("Seleccione fecha de apertura de martes a domingo.");
        if (await db.Turnos.AnyAsync(t => t.CajaActiva == caja || (t.Caja == caja && t.Inicio == i.Fecha.Date.AddHours(19)))) throw new CajaError("La caja ya tiene turno abierto o apertura en esa fecha.", 409);
        var t = new TurnoCaja { Codigo = Code("TUR"), Caja = caja, CajaActiva = caja, Inicio = DateTime.SpecifyKind(i.Fecha.Date.AddHours(19), DateTimeKind.Unspecified), Fin = DateTime.SpecifyKind(i.Fecha.Date.AddDays(1).AddHours(7), DateTimeKind.Unspecified), FondoInicial = i.FondoInicial, Responsable = user };
        db.Turnos.Add(t); Audit(t, t.Codigo, "Apertura", user, "Fondo inicial separado de ventas", new { }, t);
        await db.SaveChangesAsync(); return t;
    }
    async Task<VentaCaja> NewSale(string reference, decimal total, int? history)
    {
        var r = Text(reference, "Referencia de venta", 100); Money(total);
        if (await db.Ventas.AnyAsync(v => v.Referencia == r || (history != null && v.PedidoHistorialId == history))) throw new CajaError("Venta o pedido ya vinculado; no se duplicó el ingreso.", 409);
        if (history != null)
        {
            var order = await operaciones.PedidosHistorial.AsNoTracking().SingleOrDefaultAsync(p => p.Id == history);
            if (order is null || order.Total != total) throw new CajaError("Pedido histórico inexistente o importe diferente.");
        }
        var sale = new VentaCaja { Referencia = r, Total = total, PedidoHistorialId = history }; db.Ventas.Add(sale); return sale;
    }
    public async Task<VentaCaja> Venta(VentaInput i) { var sale = await NewSale(i.Referencia, i.Total, i.PedidoHistorialId); await db.SaveChangesAsync(); return sale; }
    public Task<MovimientoCaja> Registrar(string turnoId, MovimientoInput i, string user) => Locked(turnoId, async t =>
    {
        var key = Text(i.ClaveOperacion, "Clave de operación", 100);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(i))));
        var previous = await db.Movimientos.Include(m => m.Pagos).SingleOrDefaultAsync(m => m.TurnoId == t.Id && m.ClaveOperacion == key);
        if (previous != null) { if (previous.HuellaSolicitud != hash) throw new CajaError("Clave utilizada con otros datos.", 409); return previous; }
        Open(t); Payments(i.Pagos, i.Importe); Category(i.Tipo, i.Naturaleza, i.Categoria);
        if (i.FechaOperacion.Kind != DateTimeKind.Unspecified || i.FechaOperacion < t.Inicio || i.FechaOperacion >= t.Fin) throw new CajaError("Fecha/hora local fuera del turno: inicio 19:00 incluido y fin 07:00 del día siguiente excluido, America/Lima.");
        var m = new MovimientoCaja { Codigo = Code("MOV"), TurnoId = t.Id, Tipo = i.Tipo, Naturaleza = i.Naturaleza, Concepto = Text(i.Concepto, "Concepto"), Categoria = i.Tipo == "Ingreso" ? "Ventas" : i.Categoria, Documento = Text(i.Documento, "Comprobante"), Importe = i.Importe, FechaOperacion = i.FechaOperacion, Usuario = user, ClaveOperacion = key, HuellaSolicitud = hash, Pagos = i.Pagos.Select(p => new PagoCaja { Medio = p.Medio, Importe = p.Importe }).ToList() };
        if (i.Tipo == "Ingreso")
        {
            var sale = i.VentaId == null ? await NewSale(i.ReferenciaVenta ?? "", i.Importe, i.PedidoHistorialId) : await db.Ventas.SingleOrDefaultAsync(v => v.Id == i.VentaId) ?? throw new CajaError("Venta no encontrada.");
            if (sale.Estado != "Pendiente" || sale.Total != i.Importe || await db.Movimientos.AnyAsync(x => x.VentaId == sale.Id)) throw new CajaError("Venta ya cobrada o total diferente.", 409);
            sale.Estado = "Cobrada"; m.VentaId = sale.Id;
        }
        db.Movimientos.Add(m); Audit(t, m.Codigo, "Registro de " + m.Tipo, user, "Registro documentado", new { }, m); return m;
    });
    public Task<MovimientoCaja> Corregir(string turnoId, string id, CorreccionInput i, string user) => Locked(turnoId, async t =>
    {
        Open(t); var reason = Text(i.Motivo, "Motivo", 500);
        var m = await db.Movimientos.Include(x => x.Pagos).SingleOrDefaultAsync(x => x.Id == id && x.TurnoId == t.Id) ?? throw new CajaError("Movimiento no encontrado.", 404);
        if (m.Version != i.VersionEsperada || m.Anulado) throw new CajaError("Versión modificada o movimiento anulado.", 409);
        Payments(i.Pagos, i.Importe); Category(m.Tipo, m.Naturaleza, i.Categoria); var before = JsonSerializer.SerializeToElement(m, Json);
        if (m.Tipo == "Ingreso" && i.Importe != m.Importe) throw new CajaError("No se altera el total de una venta cobrada: anule con motivo y emita la correcta.");
        m.Importe = i.Importe; m.Concepto = Text(i.Concepto, "Concepto"); m.Documento = Text(i.Documento, "Comprobante"); m.Categoria = m.Tipo == "Ingreso" ? "Ventas" : i.Categoria; m.Version++;
        db.Pagos.RemoveRange(m.Pagos); m.Pagos = i.Pagos.Select(p => new PagoCaja { Medio = p.Medio, Importe = p.Importe }).ToList();
        Audit(t, m.Codigo, "Corrección", user, reason, before, m); return m;
    });
    public Task<MovimientoCaja> Anular(string turnoId, string id, AnulacionInput i, string user) => Locked(turnoId, async t =>
    {
        Open(t); var reason = Text(i.Motivo, "Motivo", 500);
        var m = await db.Movimientos.Include(x => x.Pagos).SingleOrDefaultAsync(x => x.Id == id && x.TurnoId == t.Id) ?? throw new CajaError("Movimiento no encontrado.", 404);
        if (m.Version != i.VersionEsperada || m.Anulado == i.Anulado) throw new CajaError("La versión o estado ya cambió; no se aplicó dos veces.", 409);
        var before = JsonSerializer.SerializeToElement(m, Json); m.Anulado = i.Anulado; m.Version++;
        if (m.VentaId != null) { var sale = await db.Ventas.SingleAsync(x => x.Id == m.VentaId); sale.Estado = m.Anulado ? "Cobro anulado" : "Cobrada"; }
        Audit(t, m.Codigo, i.Anulado ? "Anulación" : "Reversión de anulación", user, reason, before, m); return m;
    });
    public async Task<object> Summary(TurnoCaja t)
    {
        var all = await db.Movimientos.AsNoTracking().Include(m => m.Pagos).Where(m => m.TurnoId == t.Id).OrderBy(m => m.FechaOperacion).ToListAsync();
        var active = all.Where(x => !x.Anulado).ToList();
        decimal Tot(string tipo, string medio) => active.Where(m => m.Tipo == tipo).SelectMany(m => m.Pagos).Where(p => p.Medio == medio).Sum(p => p.Importe);
        var income = active.Where(m => m.Tipo == "Ingreso").Sum(m => m.Importe); var expense = active.Where(m => m.Tipo == "Egreso").Sum(m => m.Importe);
        var entries = Tot("Ingreso", "Efectivo"); var exits = Tot("Egreso", "Efectivo"); var ids = all.Where(x => x.VentaId != null).Select(x => x.VentaId).ToList();
        var sales = await db.Ventas.AsNoTracking().Where(v => ids.Contains(v.Id)).ToListAsync();
        return new { turno = t, zonaHoraria = "America/Lima", cantidadMovimientos = active.Count, anulados = all.Count - active.Count, ingresos = income, egresos = expense, saldoMovimientos = income - expense, entradasEfectivo = entries, salidasEfectivo = exits, efectivoEsperado = t.FondoInicial + entries - exits, cobrosPorMedio = new { efectivo = entries, yape = Tot("Ingreso", "Yape"), tarjeta = Tot("Ingreso", "Tarjeta") }, egresosPorMedio = new { efectivo = exits, yape = Tot("Egreso", "Yape"), tarjeta = Tot("Egreso", "Tarjeta") }, egresosPorCategoria = new[] { "Insumos", "Servicios", "Otros" }.Select(c => new { categoria = c, importe = active.Where(m => m.Tipo == "Egreso" && m.Categoria == c).Sum(m => m.Importe) }), egresosPorNaturaleza = new[] { "Gasto", "Retiro", "Devolución" }.Select(c => new { naturaleza = c, importe = active.Where(m => m.Tipo == "Egreso" && m.Naturaleza == c).Sum(m => m.Importe) }), movimientos = all, ventas = sales, filtros = new { turno = t.Codigo, fechaTurno = t.Inicio.Date.ToString("yyyy-MM-dd"), desde = t.Inicio, hastaExclusivo = t.Fin, criterio = "Turno completo. Anulados conservados en detalle, fuera de totales." } };
    }
    public async Task<object> Detail(string id)
    {
        var t = await db.Turnos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id) ?? throw new CajaError("Turno no encontrado.", 404);
        return new { resumen = await Summary(t), cierres = await db.Cierres.AsNoTracking().Where(c => c.TurnoId == id).OrderBy(c => c.Version).ToListAsync(), reportes = await db.Reportes.AsNoTracking().Where(c => c.TurnoId == id).OrderBy(c => c.Version).ToListAsync(), historial = await db.Auditorias.AsNoTracking().Where(c => c.TurnoId == id).OrderByDescending(c => c.Id).ToListAsync() };
    }
    public Task<CierreCaja> Cerrar(string id, CierreInput i, string user) => Locked(id, async t =>
    {
        var key = Text(i.ClaveOperacion, "Clave de cierre", 100);
        if (i.Contado == null) throw new CajaError("Introduzca el efectivo contado físicamente."); Money(i.Contado.Value, true);
        var prev = await db.Cierres.SingleOrDefaultAsync(c => c.TurnoId == id && c.ClaveOperacion == key);
        if (prev != null) { if (prev.Contado != i.Contado.Value) throw new CajaError("Clave utilizada con otro conteo.", 409); return prev; }
        Open(t); var s = await Summary(t); var expected = JsonSerializer.SerializeToElement(s, Json).GetProperty("efectivoEsperado").GetDecimal();
        var close = new CierreCaja { Codigo = Code("CIE"), TurnoId = id, Version = await db.Cierres.CountAsync(c => c.TurnoId == id) + 1, Responsable = user, Contado = i.Contado.Value, Esperado = expected, Diferencia = i.Contado.Value - expected, SnapshotJson = Serialize(s), ClaveOperacion = key };
        t.Estado = "Cerrado"; t.CajaActiva = null; db.Cierres.Add(close);
        Audit(t, close.Codigo, "Cierre v" + close.Version, user, "Conteo físico informado por el responsable", new { estado = "Abierto" }, new { estado = "Cerrado", close.Codigo, close.Contado, close.Esperado, close.Diferencia }); return close;
    });
    public Task<TurnoCaja> Reabrir(string id, MotivoInput i, string user) => Locked(id, async t =>
    {
        var reason = Text(i.Motivo, "Motivo de reapertura", 500);
        if (t.Estado != "Cerrado") throw new CajaError("El turno no está cerrado.", 409);
        if (await db.Turnos.AnyAsync(x => x.CajaActiva == t.Caja)) throw new CajaError("Otra caja del mismo código está abierta.", 409);
        var last = await db.Cierres.AsNoTracking().Where(x => x.TurnoId == id).OrderByDescending(x => x.Version).FirstAsync();
        t.Estado = "Abierto"; t.CajaActiva = t.Caja;
        Audit(t, last.Codigo, "Reapertura", user, reason, new { estado = "Cerrado", cierreConservado = last.Codigo, version = last.Version, last.Contado, last.Diferencia }, new { estado = "Abierto", primerCierreConservado = true }); return t;
    });
    public Task<ReporteCaja> Reportar(string id, string user) => Locked(id, async t =>
    {
        var r = new ReporteCaja { Codigo = Code("R01"), TurnoId = id, Version = await db.Reportes.CountAsync(x => x.TurnoId == id) + 1, Usuario = user, SnapshotJson = Serialize(await Summary(t)) };
        db.Reportes.Add(r); Audit(t, r.Codigo, "Generación R01 v" + r.Version, user, "Salida independiente conservada", new { }, new { r.Codigo, r.Version, r.GeneradoUtc }); return r;
    });
}
