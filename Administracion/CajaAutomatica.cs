using KaraokeEtniaApi.API.Hubs;
using KaraokeEtniaApi.Infrastructure.Postgres;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace KaraokeEtniaApi.Administracion;

// All boundaries are local wall time in Lima; they never depend on the server's timezone.
public class CajaAutomatica(CajaDbContext db, IConfiguration config, IWebHostEnvironment env, IHubContext<PedidoHub> hub)
{
    public bool Enabled => config.GetValue<bool?>("Caja:Automatica") ?? true;
    public string Nombre => config["Caja:Nombre"] ?? "CAJA-01";
    public decimal Fondo => 0;
    public int[] Dias => config.GetSection("Caja:Dias").Get<int[]>() ?? [0, 2, 3, 4, 5, 6];
    public DateTime UtcNow
    {
        get
        {
            // Local HTTP integration tests advance a private clock file; production cannot use it.
            var path = config["Caja:RelojPruebasArchivo"];
            if (env.IsDevelopment() && !string.IsNullOrWhiteSpace(path))
                return DateTime.Parse(File.ReadAllText(path), null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
            return DateTime.UtcNow;
        }
    }
    public DateTime Ahora => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Lima")), DateTimeKind.Unspecified);
    public static DateTime FechaTurno(DateTime local) => local.Hour < 7 ? local.Date.AddDays(-1) : local.Date;

    public async Task Sincronizar()
    {
        if (!Enabled) return;
        if (Fondo < 0 || decimal.Round(Fondo, 2) != Fondo || Dias.Length == 0 || Dias.Any(x => x < 0 || x > 6))
            throw new InvalidOperationException("Configuración de horario/fondo de caja inválida.");
        bool changed = false;
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            // Seeded singleton serializes reconciliation across requests and API replicas.
            var cursor = (await DatabaseRows.Locked<CalendarioCaja>(db, 1).ToListAsync()).Single();
            await db.Entry(cursor).ReloadAsync();
            var now = Ahora;
            var expired = await db.Turnos.Where(t => t.Estado == "Abierto" && t.Fin <= now).Select(t => t.Id).ToListAsync();
            foreach (var id in expired)
            {
                var t = (await DatabaseRows.Locked<TurnoCaja>(db, id).ToListAsync()).Single();
                // A concurrent administrative action might have closed it while this request waited.
                await db.Entry(t).ReloadAsync();
                if (t.Estado != "Abierto") continue;
                t.Estado = "Cerrado"; t.CajaActiva = null;
                t.FinalizadoAutomaticamenteUtc = TimeZoneInfo.ConvertTimeToUtc(t.Fin, TimeZoneInfo.FindSystemTimeZoneById("America/Lima"));
                Audit(t, "Fin automático del turno", "Fin del horario 07:00 Lima; arqueo físico pendiente. No se presume dinero contado.");
                if(t.FondoInicial==0 && !await db.Movimientos.AnyAsync(m=>m.TurnoId==t.Id) && !await db.Cierres.AnyAsync(c=>c.TurnoId==t.Id))
                    Audit(t,"Conformidad automática sin movimientos","Turno sin movimientos y fondo S/0. No se registra un arqueo físico ficticio.");
                changed = true;
            }
            var extrasVencidas=await db.Turnos.Where(t=>t.AtencionExtraHasta!=null && t.AtencionExtraHasta<=now).Select(t=>t.Id).ToListAsync();
            foreach(var id in extrasVencidas){
                var t=(await DatabaseRows.Locked<TurnoCaja>(db,id).ToListAsync()).Single();await db.Entry(t).ReloadAsync();
                var action="Fin de atención extra v"+t.VersionExcepcion;
                if(t.AtencionExtraHasta<=now && !await db.Auditorias.AnyAsync(a=>a.TurnoId==id && a.Accion==action)){Audit(t,action,"Venció la autorización; no se admiten nuevos pedidos en este turno.");changed=true;}
            }
            // Release the unique active-cash index before opening the next night.
            await db.SaveChangesAsync();
            cursor.ProximaFecha ??= FechaTurno(now);
            for (var day = cursor.ProximaFecha.Value; day.AddHours(19) <= now; day = day.AddDays(1))
            {
                if (Dias.Contains((int)day.DayOfWeek))
                {
                    var start = day.AddHours(19); var end = day.AddDays(1).AddHours(7);
                    var t = await db.Turnos.SingleOrDefaultAsync(t => t.Caja == Nombre && t.Inicio == start);
                    if (t == null)
                    {
                        t = new TurnoCaja {Codigo = CajaService.Code("TUR"), Caja = Nombre, Inicio = start, Fin = end, FondoInicial = Fondo, Responsable = "Sistema automático", Automatico = true, Estado = now < end ? "Abierto" : "Cerrado", CajaActiva = now < end ? Nombre : null};
                        if (now >= end) t.FinalizadoAutomaticamenteUtc = TimeZoneInfo.ConvertTimeToUtc(end, TimeZoneInfo.FindSystemTimeZoneById("America/Lima"));
                        db.Turnos.Add(t);
                        Audit(t, "Apertura automática", "Horario 19:00–07:00 Lima. Fondo configurado; no es una venta.");
                        if (now >= end) {
                            Audit(t, "Fin automático del turno", "Periodo recuperado tras inactividad.");
                            Audit(t, "Conformidad automática sin movimientos", "Turno sin movimientos y fondo S/0; no es un arqueo físico.");
                        }
                        changed = true;
                    }
                }
                cursor.ProximaFecha = day.AddDays(1);
            }
            await db.SaveChangesAsync(); await tx.CommitAsync();
        }
        if (changed) await hub.Clients.All.SendAsync("CajaActualizada", "horario");
    }
    void Audit(TurnoCaja t, string action, string reason) => db.Auditorias.Add(new AuditoriaCaja {TurnoId = t.Id, EntidadCodigo = t.Codigo, Accion = action, Usuario = "Sistema automático", Motivo = reason, AnteriorJson = "{}", PosteriorJson = CajaService.Serialize(new { t.Inicio, t.Fin, t.Estado, t.FondoInicial, arqueoPendiente = true })});

    // Caller owns a shared inventory/cash transaction. Recheck after acquiring the turn lock.
    public async Task<TurnoCaja> ExigirTurno()
    {
        var now = Ahora;
        var id = await db.Turnos.Where(t => t.Caja == Nombre && t.Inicio <= now && t.Fin > now).Select(t => t.Id).SingleOrDefaultAsync();
        if (id == null) id = await db.Turnos.Where(t => t.Caja == Nombre && t.Estado == "Cerrado" && t.Fin <= now && t.AtencionExtraHasta > now).OrderByDescending(t => t.Fin).Select(t => t.Id).FirstOrDefaultAsync();
        if (id == null) throw new CajaError("Fuera del horario de atención. Los pedidos se registran de 19:00 a 07:00, hora de Lima, en los días configurados.", 409);
        var t = (await DatabaseRows.Locked<TurnoCaja>(db, id).ToListAsync()).Single();
        await db.Entry(t).ReloadAsync();
        now = Ahora;
        if (!((t.Estado == "Abierto" && now >= t.Inicio && now < t.Fin) || (t.Estado == "Cerrado" && now >= t.Fin && t.AtencionExtraHasta > now))) throw new CajaError("El turno ya terminó. El pedido no fue cobrado ni descontado del inventario.", 409);
        return t;
    }
    public async Task<object> Estado()
    {
        await Sincronizar();
        var now = Ahora;
        var t = await db.Turnos.AsNoTracking().SingleOrDefaultAsync(t => t.Caja == Nombre && t.Inicio <= now && t.Fin > now);
        t ??= await db.Turnos.AsNoTracking().Where(t => t.Caja == Nombre && t.Estado == "Cerrado" && t.Fin <= now && t.AtencionExtraHasta > now).OrderByDescending(t => t.Fin).FirstOrDefaultAsync();
        return new { automatico = Enabled, horaLima = now, dias = Dias, fondoConfigurado = Fondo, habilitado = t != null && (t.Estado == "Abierto" || t.AtencionExtraHasta > now), fueraDeHorario = t?.Estado == "Cerrado", turno = t, horario = "19:00–07:00 · America/Lima" };
    }
}

public class RelojCajaWorker(IServiceScopeFactory scopes, ILogger<RelojCajaWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<CajaAutomatica>().Sincronizar(); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "No se pudo sincronizar el horario de caja; se reintentará y se verificará antes de registrar pedidos."); }
            await Task.Delay(TimeSpan.FromSeconds(30), stop);
        }
    }
}
