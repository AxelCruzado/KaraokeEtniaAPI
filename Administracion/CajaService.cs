using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KaraokeEtniaApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using KaraokeEtniaApi.Infrastructure.Postgres;

namespace KaraokeEtniaApi.Administracion;
public class CajaError(string message, int status = 400) : Exception(message) { public int Status { get; } = status; }
public record PagoInput(string Medio, decimal Importe);
public record AperturaInput(string Caja, DateTime Fecha, decimal FondoInicial);
public record MovimientoInput(string ClaveOperacion, string Tipo, string Naturaleza, string Concepto, string Categoria, string Documento, decimal Importe, DateTime FechaOperacion, List<PagoInput> Pagos, string? ReferenciaVenta = null, string? VentaId = null, int? PedidoHistorialId = null, int? PedidoOrigenId = null, bool Automatico = false);
public record CorreccionInput(int VersionEsperada, string Motivo, string Concepto, string Categoria, string Documento, decimal Importe, List<PagoInput> Pagos);
public record CierreInput(string ClaveOperacion, decimal? Contado, string? Motivo = null, decimal? Yape = null, decimal? Plin = null, decimal? Tarjeta = null, string? Observaciones = null, bool ConfirmoRevision = false);
public record FondoInput(decimal Monto, int VersionEsperada, string Motivo);
public record ExcepcionHorarioInput(DateTime Hasta, string Motivo, int VersionEsperada);
public record MotivoInput(string Motivo);
public record AnulacionInput(string Motivo, bool Anulado, int VersionEsperada);
public record VentaInput(string Referencia, decimal Total, int? PedidoHistorialId);

public class CajaService(CajaDbContext db, AppDbContext operaciones, CajaAutomatica horario)
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
        if (payments is null || payments.Count == 0 || payments.Count > 4 || payments.Any(p => p == null) || payments.Select(p => p.Medio).Distinct().Count() != payments.Count)
            throw new CajaError("Indique cada medio de pago una sola vez.");
        foreach (var p in payments)
        {
            if (!new[] { "Efectivo", "Yape", "Plin", "Tarjeta" }.Contains(p.Medio)) throw new CajaError("Medio de pago inválido.");
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
        await using var tx = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted) : null;
        var turns = await DatabaseRows.Locked<TurnoCaja>(db,id).ToListAsync();
        var turn = turns.SingleOrDefault() ?? throw new CajaError("Turno no encontrado.", 404);
        var result = await action(turn);
        await db.SaveChangesAsync(); if(tx != null) await tx.CommitAsync(); return result;
    }
    void Audit(TurnoCaja t, string code, string action, string who, string reason, object before, object after) => db.Auditorias.Add(new() { TurnoId = t.Id, EntidadCodigo = code, Accion = action, Usuario = who, RegistradoUtc = horario.UtcNow, Motivo = reason, AnteriorJson = Serialize(before), PosteriorJson = Serialize(after) });
    public async Task<TurnoCaja> Apertura(AperturaInput i, string user)
    {
        var caja = Text(i.Caja, "Caja", 40).ToUpperInvariant(); Money(i.FondoInicial, true);
        if (i.Fecha.TimeOfDay != TimeSpan.Zero || i.Fecha.DayOfWeek == DayOfWeek.Monday) throw new CajaError("Seleccione fecha de apertura de martes a domingo.");
        if (await db.Turnos.AnyAsync(t => t.CajaActiva == caja || (t.Caja == caja && t.Inicio == i.Fecha.Date.AddHours(19)))) throw new CajaError("La caja ya tiene turno abierto o apertura en esa fecha.", 409);
        var t = new TurnoCaja { Codigo = Code("TUR"), Caja = caja, CajaActiva = caja, Inicio = DateTime.SpecifyKind(i.Fecha.Date.AddHours(19), DateTimeKind.Unspecified), Fin = DateTime.SpecifyKind(i.Fecha.Date.AddDays(1).AddHours(7), DateTimeKind.Unspecified), FondoInicial = i.FondoInicial, Responsable = user };
        db.Turnos.Add(t); Audit(t, t.Codigo, "Apertura", user, "Fondo inicial separado de ventas", new { }, t);
        await db.SaveChangesAsync(); return t;
    }
    async Task<VentaCaja> NewSale(string reference, decimal total, int? history, int? origin = null)
    {
        var r = Text(reference, "Referencia de venta", 100); Money(total);
        if (await db.Ventas.AnyAsync(v => v.Referencia == r || (history != null && v.PedidoHistorialId == history) || (origin != null && v.PedidoOrigenId == origin))) throw new CajaError("Venta o pedido ya vinculado; no se duplicó el ingreso.", 409);
        if (history != null)
        {
            var order = await operaciones.PedidosHistorial.AsNoTracking().SingleOrDefaultAsync(p => p.Id == history);
            if (order is null || order.Total != total) throw new CajaError("Pedido histórico inexistente o importe diferente.");
        }
        if (origin != null) {
            var order = await operaciones.PedidosActivos.SingleOrDefaultAsync(p => p.Id == origin);
            if (order == null || order.PagoCliente != total || total > order.Total) throw new CajaError("El cobro no corresponde al pedido.");
        }
        var sale = new VentaCaja { Referencia = r, Total = total, PedidoHistorialId = history, PedidoOrigenId = origin }; db.Ventas.Add(sale); return sale;
    }
    public async Task<VentaCaja> Venta(VentaInput i) { var sale = await NewSale(i.Referencia, i.Total, i.PedidoHistorialId); await db.SaveChangesAsync(); return sale; }
    public Task<MovimientoCaja> Registrar(string turnoId, MovimientoInput i, string user) => Locked(turnoId, async t =>
    {
        var key = Text(i.ClaveOperacion, "Clave de operación", 100);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(i))));
        var previous = await db.Movimientos.Include(m => m.Pagos).SingleOrDefaultAsync(m => m.TurnoId == t.Id && m.ClaveOperacion == key);
        if (previous != null) { if (previous.HuellaSolicitud != hash) throw new CajaError("Clave utilizada con otros datos.", 409); return previous; }
        var extra = i.Automatico && i.PedidoOrigenId != null && t.Estado == "Cerrado" && t.AtencionExtraHasta > horario.Ahora && horario.Ahora >= t.Fin;
        if (!extra) Open(t); Payments(i.Pagos, i.Importe); Category(i.Tipo, i.Naturaleza, i.Categoria);
        if (i.FechaOperacion.Kind != DateTimeKind.Unspecified || i.FechaOperacion < t.Inicio || (i.FechaOperacion >= t.Fin && !(extra && i.FechaOperacion < t.AtencionExtraHasta))) throw new CajaError("Fecha/hora local fuera del turno: inicio 19:00 incluido y fin 07:00 del día siguiente excluido, America/Lima.");
        var m = new MovimientoCaja { Codigo = Code("MOV"), TurnoId = t.Id, Tipo = i.Tipo, Naturaleza = i.Naturaleza, Concepto = Text(i.Concepto + (extra ? " · Fuera de horario" : ""), "Concepto"), Categoria = i.Tipo == "Ingreso" ? "Ventas" : i.Categoria, Documento = Text(i.Documento, "Comprobante"), Importe = i.Importe, FechaOperacion = i.FechaOperacion, Usuario = user, RegistradoUtc = horario.UtcNow, ClaveOperacion = key, HuellaSolicitud = hash, Pagos = i.Pagos.Select(p => new PagoCaja { Medio = p.Medio, Importe = p.Importe }).ToList() };
        if (i.Tipo == "Ingreso")
        {
            var sale = i.VentaId == null ? await NewSale(i.ReferenciaVenta ?? "", i.Importe, i.PedidoHistorialId, i.PedidoOrigenId) : await db.Ventas.SingleOrDefaultAsync(v => v.Id == i.VentaId) ?? throw new CajaError("Venta no encontrada.");
            if (sale.Estado != "Pendiente" || sale.Total != i.Importe || await db.Movimientos.AnyAsync(x => x.VentaId == sale.Id)) throw new CajaError("Venta ya cobrada o total diferente.", 409);
            sale.Estado = "Cobrada"; m.VentaId = sale.Id;
        }
        db.Movimientos.Add(m); Audit(t, m.Codigo, "Registro de " + m.Tipo, user, extra ? "Venta fuera de horario autorizada por "+t.AtencionExtraPor+": "+t.MotivoAtencionExtra : i.Automatico ? "Cobro automático del pedido: sin registro manual duplicado" : "Registro documentado", new { }, m); return m;
    });
    public Task<MovimientoCaja> Corregir(string turnoId, string id, CorreccionInput i, string user) => Locked(turnoId, async t =>
    {
        Open(t); var reason = Text(i.Motivo, "Motivo", 500);
        if(await operaciones.Compras.AnyAsync(c=>c.MovimientoCajaId==id)) throw new CajaError("El egreso está vinculado a una recepción de compra; no se modifica ni anula aisladamente.",409);
        var m = await db.Movimientos.Include(x => x.Pagos).SingleOrDefaultAsync(x => x.Id == id && x.TurnoId == t.Id) ?? throw new CajaError("Movimiento no encontrado.", 404);
        if (m.Version != i.VersionEsperada || m.Anulado) throw new CajaError("Versión modificada o movimiento anulado.", 409);
        Payments(i.Pagos, i.Importe); Category(m.Tipo, m.Naturaleza, i.Categoria); var before = JsonSerializer.SerializeToElement(m, Json);
        if (m.Tipo == "Ingreso" && i.Importe != m.Importe) throw new CajaError("No se altera el total de una venta cobrada: anule con motivo y emita la correcta.");
        m.Importe = i.Importe; m.Concepto = Text(i.Concepto, "Concepto"); m.Documento = Text(i.Documento, "Comprobante"); m.Categoria = m.Tipo == "Ingreso" ? "Ventas" : i.Categoria; m.Version++;
        db.Pagos.RemoveRange(m.Pagos); m.Pagos = i.Pagos.Select(p => new PagoCaja { Medio = p.Medio, Importe = p.Importe }).ToList();
        Audit(t, m.Codigo, "Corrección", user, reason, before, m); return m;
    });
    public Task<MovimientoCaja> Anular(string turnoId, string id, AnulacionInput i, string user, bool compraAutorizada = false) => Locked(turnoId, async t =>
    {
        Open(t); var reason = Text(i.Motivo, "Motivo", 500);
        if(!compraAutorizada && await operaciones.Compras.AnyAsync(c=>c.MovimientoCajaId==id)) throw new CajaError("El egreso está vinculado a una recepción de compra; no se modifica ni anula aisladamente.",409);
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
        var ultimo=await db.Cierres.AsNoTracking().Where(c=>c.TurnoId==t.Id).OrderByDescending(c=>c.Version).FirstOrDefaultAsync();
        var sinActividad=t.Estado=="Cerrado" && all.Count==0 && t.FondoInicial==0 && ultimo==null;
        var pendiente=ultimo==null || string.IsNullOrEmpty(ultimo.EstadoConformidad) || ultimo.EstadoConformidad=="Arqueo anterior" || all.Any(m=>m.RegistradoUtc>ultimo.RegistradoUtc);
        var income = active.Where(m => m.Tipo == "Ingreso").Sum(m => m.Importe); var expense = active.Where(m => m.Tipo == "Egreso").Sum(m => m.Importe);
        var entries = Tot("Ingreso", "Efectivo"); var exits = Tot("Egreso", "Efectivo"); var ids = all.Where(x => x.VentaId != null).Select(x => x.VentaId).ToList();
        var sales = await db.Ventas.AsNoTracking().Where(v => ids.Contains(v.Id)).ToListAsync();
        return new { conformidad = sinActividad?"Conforme automáticamente: sin movimientos":pendiente?(ultimo==null?"Pendiente de conformidad":"Pendiente de nueva conformidad"):ultimo!.EstadoConformidad, efectivoContado=ultimo?.Contado, diferenciaContada=ultimo?.Diferencia, turno = t, zonaHoraria = "America/Lima", cantidadMovimientos = active.Count, anulados = all.Count - active.Count, ventasFueraHorario = active.Count(m=>m.Tipo=="Ingreso" && m.FechaOperacion>=t.Fin), ingresos = income, egresos = expense, saldoMovimientos = income - expense, entradasEfectivo = entries, salidasEfectivo = exits, efectivoEsperado = t.FondoInicial + entries - exits, cobrosPorMedio = new { efectivo = entries, yape = Tot("Ingreso", "Yape"), plin = Tot("Ingreso", "Plin"), tarjeta = Tot("Ingreso", "Tarjeta") }, egresosPorMedio = new { efectivo = exits, yape = Tot("Egreso", "Yape"), plin = Tot("Egreso", "Plin"), tarjeta = Tot("Egreso", "Tarjeta") }, egresosPorCategoria = new[] { "Insumos", "Servicios", "Otros" }.Select(c => new { categoria = c, importe = active.Where(m => m.Tipo == "Egreso" && m.Categoria == c).Sum(m => m.Importe) }), egresosPorNaturaleza = new[] { "Gasto", "Retiro", "Devolución" }.Select(c => new { naturaleza = c, importe = active.Where(m => m.Tipo == "Egreso" && m.Naturaleza == c).Sum(m => m.Importe) }), movimientos = all, ventas = sales, filtros = new { turno = t.Codigo, fechaTurno = t.Inicio.Date.ToString("yyyy-MM-dd"), desde = t.Inicio, hastaExclusivo = t.AtencionExtraHasta ?? t.Fin, finOrdinario = t.Fin, criterio = "Turno completo, incluidas ventas fuera de horario autorizadas. Anulados conservados en detalle, fuera de totales." } };
    }
    public async Task<object> Detail(string id)
    {
        var t = await db.Turnos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id) ?? throw new CajaError("Turno no encontrado.", 404);
        var ultimoArqueo = await db.Cierres.AsNoTracking().Where(c=>c.TurnoId==id).OrderByDescending(c=>c.Version).FirstOrDefaultAsync();
        var sinActividad=t.Estado=="Cerrado" && t.FondoInicial==0 && ultimoArqueo==null && !await db.Movimientos.AnyAsync(m=>m.TurnoId==id);
        var cambiado = ultimoArqueo != null && await db.Movimientos.AnyAsync(m=>m.TurnoId==id && m.RegistradoUtc>ultimoArqueo.RegistradoUtc);
        return new { arqueoPendiente = !sinActividad && (ultimoArqueo == null || string.IsNullOrEmpty(ultimoArqueo.EstadoConformidad) || cambiado), arqueoDesactualizado = cambiado, atencionExtraVigente = t.AtencionExtraHasta > horario.Ahora, resumen = await Summary(t), cierres = await db.Cierres.AsNoTracking().Where(c => c.TurnoId == id).OrderBy(c => c.Version).ToListAsync(), reportes = await db.Reportes.AsNoTracking().Where(c => c.TurnoId == id).OrderBy(c => c.Version).ToListAsync(), historial = await db.Auditorias.AsNoTracking().Where(c => c.TurnoId == id).OrderByDescending(c => c.Id).ToListAsync() };
    }
    public Task<CierreCaja> Cerrar(string id, CierreInput i, string user) => Locked(id, async t =>
    {
        var key = Text(i.ClaveOperacion, "Clave de cierre", 100);
        if (i.Contado == null) throw new CajaError("Introduzca el efectivo contado físicamente."); Money(i.Contado.Value, true);
        var prev = await db.Cierres.SingleOrDefaultAsync(c => c.TurnoId == id && c.ClaveOperacion == key);
        if (prev != null) { if (prev.Contado != i.Contado.Value || prev.YapeVerificado!=i.Yape || prev.PlinVerificado!=i.Plin || prev.TarjetaVerificado!=i.Tarjeta || prev.Observaciones!=(i.Observaciones??"")) throw new CajaError("Clave utilizada con otro conteo.", 409); return prev; }
        if (t.Estado != "Abierto" && !(horario.Enabled && t.Estado=="Cerrado")) Open(t);
        if (await db.Cierres.AnyAsync(c=>c.TurnoId==id) && string.IsNullOrWhiteSpace(i.Motivo)) throw new CajaError("Indica el motivo del nuevo arqueo; el anterior se conserva.");
        var s = await Summary(t); var expected = JsonSerializer.SerializeToElement(s, Json).GetProperty("efectivoEsperado").GetDecimal();
        var digital=JsonSerializer.SerializeToElement(s,Json).GetProperty("cobrosPorMedio");
        if(horario.Enabled && (!i.ConfirmoRevision || i.Yape==null || i.Plin==null || i.Tarjeta==null))throw new CajaError("Confirma la revisión e indica el efectivo y los importes verificados de Yape, Plin y tarjeta, incluyendo cero cuando corresponda.");
        foreach(var amount in new[]{i.Yape,i.Plin,i.Tarjeta})if(amount!=null)Money(amount.Value,true);
        var difference=i.Contado.Value!=expected || (i.Yape!=null && i.Yape!=digital.GetProperty("yape").GetDecimal()) || (i.Plin!=null && i.Plin!=digital.GetProperty("plin").GetDecimal()) || (i.Tarjeta!=null && i.Tarjeta!=digital.GetProperty("tarjeta").GetDecimal());
        if(horario.Enabled && difference && string.IsNullOrWhiteSpace(i.Observaciones))throw new CajaError("Registra observaciones para explicar las diferencias encontradas.");
        if((i.Observaciones?.Length??0)>2000)throw new CajaError("Las observaciones no deben superar 2000 caracteres.");
        var close = new CierreCaja { RegistradoUtc=horario.UtcNow, YapeVerificado=i.Yape,PlinVerificado=i.Plin,TarjetaVerificado=i.Tarjeta,Observaciones=i.Observaciones??"",EstadoConformidad=horario.Enabled?(difference?"Con diferencias":string.IsNullOrWhiteSpace(i.Observaciones)?"Conforme":"Conforme con observaciones"):"Arqueo anterior", Codigo = Code("CIE"), TurnoId = id, Version = await db.Cierres.CountAsync(c => c.TurnoId == id) + 1, Responsable = user, Contado = i.Contado.Value, Esperado = expected, Diferencia = i.Contado.Value - expected, SnapshotJson = Serialize(s), ClaveOperacion = key };
        t.Estado = "Cerrado"; t.CajaActiva = null; db.Cierres.Add(close);
        Audit(t, close.Codigo, "Cierre v" + close.Version, user, "Conteo físico informado por el responsable. "+i.Motivo, new { estado = t.Estado }, new { estado = "Cerrado", close.Codigo, close.Contado, close.Esperado, close.Diferencia }); return close;
    });
    public Task<TurnoCaja> DeclararFondo(string id, FondoInput i, string user) => Locked(id, async t =>
    {
        Money(i.Monto,true); var reason=Text(i.Motivo,"Motivo"); var now=horario.Ahora;
        if(t.Estado!="Abierto" || now<t.Inicio || now>=t.Fin) throw new CajaError("El fondo se declara durante el turno, de 19:00 a 07:00.",409);
        if(t.VersionFondo!=i.VersionEsperada) throw new CajaError("El fondo cambió; revisa el monto actual.",409);
        var before=new{t.FondoInicial,t.FondoDeclaradoUtc,t.FondoDeclaradoPor,t.VersionFondo};
        t.FondoInicial=i.Monto;t.FondoDeclaradoUtc=horario.UtcNow;t.FondoDeclaradoPor=user;t.VersionFondo++;
        Audit(t,t.Codigo,"Declaración de fondo inicial",user,reason,before,new{t.FondoInicial,t.FondoDeclaradoUtc,t.FondoDeclaradoPor,t.VersionFondo,horasDesdeApertura=(now-t.Inicio).TotalHours});
        return t;
    });
    public Task<TurnoCaja> AutorizarExtra(string id, ExcepcionHorarioInput i, string user) => Locked(id, async t =>
    {
        var reason=Text(i.Motivo,"Motivo de excepción");var now=horario.Ahora;
        if(t.Estado!="Cerrado" || now<t.Fin || now>=t.Fin.Date.AddHours(19))throw new CajaError("La excepción corresponde al turno terminado esta mañana, antes del siguiente turno.",409);
        if(i.Hasta.Kind!=DateTimeKind.Unspecified || i.Hasta<=now || i.Hasta>t.Fin.Date.AddHours(19))throw new CajaError("Indica una hora límite futura anterior o igual a las 19:00.");
        if(t.VersionExcepcion!=i.VersionEsperada)throw new CajaError("La autorización cambió; revisa la hora límite.",409);
        var before=new{t.AtencionExtraHasta,t.MotivoAtencionExtra,t.VersionExcepcion};
        t.AtencionExtraHasta=i.Hasta;t.MotivoAtencionExtra=reason;t.AtencionExtraAutorizadaUtc=horario.UtcNow;t.AtencionExtraPor=user;t.VersionExcepcion++;
        Audit(t,t.Codigo,"Autorización de atención fuera de horario",user,reason,before,new{t.AtencionExtraHasta,t.AtencionExtraAutorizadaUtc,t.AtencionExtraPor,t.VersionExcepcion,estadoConservado=t.Estado});return t;
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
