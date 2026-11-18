using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
namespace KaraokeEtniaApi.Administracion;
// Legacy financial URLs now read the same ledger as R01, never the old Gastos table.
[ApiController,Authorize(Roles="Administrador,Cajero"),Route("api/reportes"),Route("api/ganancias"),Route("api/cajachica"),TypeFilter(typeof(CajaExceptionFilter))]
public class FinanzasController(CajaDbContext db):ControllerBase
{
 [HttpGet("mensual"),HttpGet("mes"),HttpGet("resumen")]
 public Task<IActionResult> Mes(int year,int month)=>Rango(new DateTime(year,month,1),new DateTime(year,month,1).AddMonths(1));
 [HttpGet("diario"),HttpGet("dia")]
 public Task<IActionResult> Dia(int year,int month,int day)=>Rango(new DateTime(year,month,day),new DateTime(year,month,day).AddDays(1));
 async Task<IActionResult> Rango(DateTime desde,DateTime hasta){var ids=await db.Turnos.Where(t=>t.Inicio>=desde&&t.Inicio<hasta).Select(t=>t.Id).ToListAsync();var m=await db.Movimientos.AsNoTracking().Include(x=>x.Pagos).Where(x=>ids.Contains(x.TurnoId)&&!x.Anulado).ToListAsync();var ingresos=m.Where(x=>x.Tipo=="Ingreso").Sum(x=>x.Importe);var egresos=m.Where(x=>x.Tipo=="Egreso").Sum(x=>x.Importe);return Ok(new{fuente="Libro de caja por turno / R01",criterio="Fecha de apertura; incluye madrugada; sin fondo inicial",desde,hastaExclusivo=hasta,ingresos,egresos,saldoMovimientos=ingresos-egresos,cantidadMovimientos=m.Count,cobrosPorMedio=new[]{"Efectivo","Yape","Tarjeta"}.Select(p=>new{medio=p,importe=m.Where(x=>x.Tipo=="Ingreso").SelectMany(x=>x.Pagos).Where(x=>x.Medio==p).Sum(x=>x.Importe)}),egresosPorCategoria=new[]{"Insumos","Servicios","Otros"}.Select(c=>new{categoria=c,importe=m.Where(x=>x.Tipo=="Egreso"&&x.Categoria==c).Sum(x=>x.Importe)})});}
 [HttpPost("registrar-gasto")]
 public IActionResult Gasto()=>StatusCode(410,new{message="Registre egresos en /api/caja/turnos/{id}/movimientos para conservar el turno, respaldo y trazabilidad."});
}
