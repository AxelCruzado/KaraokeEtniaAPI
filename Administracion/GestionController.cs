using KaraokeEtniaApi.Domain.Entities;
using KaraokeEtniaApi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using KaraokeEtniaApi.Infrastructure.Postgres;
namespace KaraokeEtniaApi.Administracion;
public record Ajuste(int ProductoId,int Cantidad,string Motivo,int StockEsperado);
public record Minimo(int Cantidad);
public record CompraInput(int ProveedorId,string Documento,List<LineaInput> Lineas);
public record LineaInput(int ProductoId,int Cantidad,decimal CostoUnitario);
public record Recepcion(string TurnoId,List<PagoInput> Pagos,DateTime FechaOperacion,int VersionEsperada);
public record CambioCompra(CompraInput Datos,string Motivo,int VersionEsperada);
public record MotivoCompra(string Motivo,int VersionEsperada);
public record EstadoUsuario(bool Activo,string Rol,string Motivo);
[ApiController,Route("api/gestion"),Authorize(Roles="Administrador,Cajero"),TypeFilter(typeof(CajaExceptionFilter))]
public class GestionController(AppDbContext db,CajaDbContext ledger,CajaService caja):ControllerBase
{
 string Who=>User.Identity!.Name!;
 void Audit(string action,string detail)=>db.AccionesAdministrativas.Add(new(){Usuario=Who,Accion=action,Detalle=detail});
 [HttpGet("resumen")]
 public async Task<IActionResult> Resumen()=>Ok(new { productos=await db.Productos.AsNoTracking().OrderBy(p=>p.Nombre).Select(p=>new{p.Id,p.Nombre,p.Stock,p.StockMinimo,reponer=p.Stock<=p.StockMinimo}).ToListAsync(), proveedores=await db.Proveedores.AsNoTracking().ToListAsync(), compras=await db.Compras.AsNoTracking().Include(c=>c.Lineas).ThenInclude(l=>l.Producto).Include(c=>c.Proveedor).Select(c=>new {c.Id,c.Documento,c.Estado,c.Version,c.ProveedorId,c.FechaUtc,c.Usuario,c.MovimientoCajaId,proveedor=c.Proveedor.Nombre,total=c.Lineas.Sum(l=>l.Cantidad*l.CostoUnitario),lineas=c.Lineas.Select(l=>new{l.ProductoId,producto=l.Producto.Nombre,l.Cantidad,l.CostoUnitario})}).ToListAsync() });
 [HttpPost("proveedores"),Authorize(Roles="Administrador")]
 public async Task<IActionResult> Proveedor(Proveedor p){if(string.IsNullOrWhiteSpace(p.Nombre)||string.IsNullOrWhiteSpace(p.Documento)||p.Documento.Length>100)throw new CajaError("Nombre y documento de proveedor obligatorios.");p.Id=0;db.Proveedores.Add(p);Audit("Proveedor",p.Nombre);await db.SaveChangesAsync();return Ok(p);}
 [HttpPost("inventario"),Authorize(Roles="Administrador")]
 public async Task<IActionResult> Inventario(Ajuste i){if(i.Cantidad==0||string.IsNullOrWhiteSpace(i.Motivo))throw new CajaError("Indique cantidad con signo y motivo.");var p=await db.Productos.FindAsync(i.ProductoId)??throw new CajaError("Producto no encontrado.",404);if(p.Stock!=i.StockEsperado)throw new CajaError("Stock cambió; actualice.",409);p.Stock=checked(p.Stock+i.Cantidad);if(p.Stock<0)throw new CajaError("Existencias insuficientes.");db.MotivoStock=i.Motivo.Trim();Audit("Ajuste de inventario",$"Producto {p.Id}: {i.StockEsperado} → {p.Stock}; {i.Motivo}");await db.SaveChangesAsync();return Ok(new{p.Id,p.Stock});}
 [HttpPost("inventario/{id}/minimo"),Authorize(Roles="Administrador")]
 public async Task<IActionResult> Reposicion(int id,Minimo i){if(i.Cantidad<0)throw new CajaError("Mínimo inválido.");var p=await db.Productos.FindAsync(id)??throw new CajaError("Producto no encontrado.",404);Audit("Umbral de reposición",$"Producto {id}: {p.StockMinimo} → {i.Cantidad}");p.StockMinimo=i.Cantidad;await db.SaveChangesAsync();return Ok(new{p.Id,p.StockMinimo});}
 [HttpPost("compras"),Authorize(Roles="Administrador")]
 public async Task<IActionResult> Comprar(CompraInput i){if(string.IsNullOrWhiteSpace(i.Documento)||i.Documento.Length>100||i.Lineas==null||i.Lineas.Count==0||i.Lineas.Any(l=>l.Cantidad<=0||l.CostoUnitario<=0||decimal.Round(l.CostoUnitario,2)!=l.CostoUnitario)||i.Lineas.Select(l=>l.ProductoId).Distinct().Count()!=i.Lineas.Count)throw new CajaError("Compra inválida: documento, productos únicos, cantidades y costos positivos.");if(!await db.Proveedores.AnyAsync(p=>p.Id==i.ProveedorId&&p.Activo))throw new CajaError("Proveedor inexistente o inactivo.");if(await db.Productos.CountAsync(p=>i.Lineas.Select(l=>l.ProductoId).Contains(p.Id))!=i.Lineas.Count)throw new CajaError("Producto inexistente.");var c=new Compra{ProveedorId=i.ProveedorId,Documento=i.Documento.Trim(),Usuario=Who,Lineas=i.Lineas.Select(l=>new CompraLinea{ProductoId=l.ProductoId,Cantidad=l.Cantidad,CostoUnitario=l.CostoUnitario}).ToList()};db.Compras.Add(c);Audit("Registro de compra",c.Documento);await db.SaveChangesAsync();return Ok(new{c.Id,c.Estado});}
 [HttpPost("compras/{id}/recibir"),Authorize(Roles="Administrador")]
 public async Task<IActionResult> Recibir(int id,Recepcion i){
  if(i.VersionEsperada<=0)throw new CajaError("Indique una versionEsperada positiva para recibir la compra.");
  // One transaction spans receipt, inventory ledger and the corresponding cash expense.
  await ledger.Database.CloseConnectionAsync();ledger.Database.SetDbConnection(db.Database.GetDbConnection());
  await using var tx=await db.Database.BeginTransactionAsync();await ledger.Database.UseTransactionAsync(tx.GetDbTransaction());
  var rows=await DatabaseRows.Locked<Compra>(db,id).ToListAsync();var c=rows.SingleOrDefault()??throw new CajaError("Compra no encontrada.",404);
  if(c.Version!=i.VersionEsperada)throw new CajaError("La compra cambió desde que la consultó. Actualice y revise sus productos antes de recibirla.",409);
  if(c.Estado!="Pendiente")throw new CajaError("Compra ya recibida; no se duplica stock ni egreso.",409);
  await db.Entry(c).Collection(x=>x.Lineas).LoadAsync();var turno=await ledger.Turnos.SingleOrDefaultAsync(t=>t.Id==i.TurnoId)??throw new CajaError("Turno no encontrado.");
  foreach(var l in c.Lineas){var product=await db.Productos.FindAsync(l.ProductoId)??throw new CajaError("Producto no encontrado.");product.Stock=checked(product.Stock+l.Cantidad);}
  var amount=c.Lineas.Sum(l=>l.Cantidad*l.CostoUnitario);var mov=await caja.Registrar(i.TurnoId,new MovimientoInput("compra-"+id+"-v"+c.Version,"Egreso","Gasto","Compra "+c.Documento,"Insumos",c.Documento,amount,i.FechaOperacion,i.Pagos),Who);
  c.Estado="Recibida";c.Version++;c.MovimientoCajaId=mov.Id;db.MotivoStock="Recepción de compra "+c.Documento;Audit("Recepción de compra",CajaService.Serialize(new{compraId=id,c.Version,egreso=mov.Codigo,movimientoId=mov.Id,motivo="Recepción y pago completos",lineas=c.Lineas.Select(l=>new{l.ProductoId,l.Cantidad,l.CostoUnitario})}));await db.SaveChangesAsync();await tx.CommitAsync();return Ok(new{c.Id,c.Estado,c.MovimientoCajaId});
 }
 [HttpGet("usuarios"),Authorize(Roles="Administrador")]
 public async Task<IActionResult> Usuarios()=>Ok(await db.Usuarios.Select(u=>new{u.Id,u.NombreUsuario,u.Rol,u.Activo}).ToListAsync());
 [HttpPost("usuarios/{id}/estado"),Authorize(Roles="Administrador")]
 public async Task<IActionResult> Usuario(int id,EstadoUsuario i){if(string.IsNullOrWhiteSpace(i.Motivo)||!Enum.TryParse<Rol>(i.Rol,out var role)||!Enum.IsDefined(role))throw new CajaError("Rol o motivo inválido.");var u=await db.Usuarios.FindAsync(id)??throw new CajaError("Usuario no encontrado.",404);if(u.NombreUsuario==Who)throw new CajaError("No puede desactivar ni cambiar su propio rol.");Audit("Permisos/estado de usuario",$"{u.NombreUsuario}: {u.Rol}/{u.Activo} → {i.Rol}/{i.Activo}; {i.Motivo}");u.Activo=i.Activo;u.Rol=i.Rol;await db.SaveChangesAsync();return Ok(new{u.Id,u.Activo,u.Rol});}

 static void Reason(string motivo){if(string.IsNullOrWhiteSpace(motivo)||motivo.Trim().Length>500)throw new CajaError("Indique un motivo de 1 a 500 caracteres.");}
 async Task<Compra> LockCompra(int id){var rows=await DatabaseRows.Locked<Compra>(db,id).ToListAsync();var c=rows.SingleOrDefault()??throw new CajaError("Compra no encontrada.",404);await db.Entry(c).Collection(x=>x.Lineas).LoadAsync();return c;}
 object Snap(Compra c)=>new{c.Id,c.Version,c.ProveedorId,c.Documento,c.Estado,c.MovimientoCajaId,lineas=c.Lineas.Select(l=>new{l.ProductoId,l.Cantidad,l.CostoUnitario}).ToArray()};
 [HttpPost("compras/{id}/corregir"),Authorize(Roles="Administrador")]
 public async Task<IActionResult> CorregirCompra(int id,CambioCompra i){
  Reason(i.Motivo);await using var tx=await db.Database.BeginTransactionAsync();var c=await LockCompra(id);
  if(c.Version!=i.VersionEsperada||!(c.Estado=="Pendiente"||c.Estado=="Revertida"))throw new CajaError("La compra cambió o requiere revertir su recepción antes de corregirla.",409);
  var x=i.Datos;if(x==null||string.IsNullOrWhiteSpace(x.Documento)||x.Documento.Length>100||x.Lineas==null||x.Lineas.Count==0||x.Lineas.Any(l=>l==null||l.Cantidad<=0||l.CostoUnitario<=0||decimal.Round(l.CostoUnitario,2)!=l.CostoUnitario)||x.Lineas.Select(l=>l.ProductoId).Distinct().Count()!=x.Lineas.Count)throw new CajaError("Datos de compra inválidos.");
  if(!await db.Proveedores.AnyAsync(p=>p.Id==x.ProveedorId&&p.Activo)||await db.Productos.CountAsync(p=>x.Lineas.Select(l=>l.ProductoId).Contains(p.Id))!=x.Lineas.Count)throw new CajaError("Proveedor o productos inválidos.");
  var before=Snap(c);db.CompraLineas.RemoveRange(c.Lineas);c.Lineas=x.Lineas.Select(l=>new CompraLinea{ProductoId=l.ProductoId,Cantidad=l.Cantidad,CostoUnitario=l.CostoUnitario}).ToList();c.ProveedorId=x.ProveedorId;c.Documento=x.Documento.Trim();c.Estado="Pendiente";c.MovimientoCajaId=null;c.Version++;
  Audit("Corrección de compra",CajaService.Serialize(new{motivo=i.Motivo,anterior=before,posterior=Snap(c)}));await db.SaveChangesAsync();await tx.CommitAsync();return Ok(new{c.Id,c.Estado,c.Version});
 }
 [HttpPost("compras/{id}/cancelar"),Authorize(Roles="Administrador")]
 public async Task<IActionResult> CancelarCompra(int id,MotivoCompra i){Reason(i.Motivo);await using var tx=await db.Database.BeginTransactionAsync();var c=await LockCompra(id);if(c.Version!=i.VersionEsperada||!(c.Estado=="Pendiente"||c.Estado=="Revertida"))throw new CajaError("Compra cambió o fue recibida: revierta primero.",409);var before=Snap(c);c.Estado="Cancelada";c.Version++;Audit("Cancelación de compra",CajaService.Serialize(new{motivo=i.Motivo,anterior=before,posterior=Snap(c)}));await db.SaveChangesAsync();await tx.CommitAsync();return Ok(new{c.Id,c.Estado,c.Version});}
 [HttpPost("compras/{id}/revertir"),Authorize(Roles="Administrador")]
 public async Task<IActionResult> RevertirCompra(int id,MotivoCompra i){
  Reason(i.Motivo);await ledger.Database.CloseConnectionAsync();ledger.Database.SetDbConnection(db.Database.GetDbConnection());await using var tx=await db.Database.BeginTransactionAsync();await ledger.Database.UseTransactionAsync(tx.GetDbTransaction());var c=await LockCompra(id);
  if(c.Version!=i.VersionEsperada||c.Estado!="Recibida"||c.MovimientoCajaId==null)throw new CajaError("La recepción cambió o ya se revirtió.",409);
  var before=Snap(c);var mov=await ledger.Movimientos.SingleAsync(m=>m.Id==c.MovimientoCajaId);if(mov.Anulado)throw new CajaError("Egreso ya anulado; revise la integridad antes de continuar.",409);
  foreach(var l in c.Lineas){var p=await db.Productos.FindAsync(l.ProductoId)??throw new CajaError("Producto no encontrado.");if(p.Stock<l.Cantidad)throw new CajaError("Stock insuficiente para revertir; parte de la recepción ya fue utilizada.",409);p.Stock-=l.Cantidad;}
  await caja.Anular(mov.TurnoId,mov.Id,new AnulacionInput(i.Motivo,true,mov.Version),Who,true);
  c.Estado="Revertida";c.Version++;db.MotivoStock="Reversión de recepción "+c.Documento+": "+i.Motivo;Audit("Reversión de compra",CajaService.Serialize(new{motivo=i.Motivo,anterior=before,posterior=Snap(c)}));await db.SaveChangesAsync();await tx.CommitAsync();return Ok(new{c.Id,c.Estado,c.Version});
 }
 static void Page(int pagina,int tamano,DateTime? desde,DateTime? hasta){if(pagina<1||pagina>1000000||tamano<1||tamano>200||desde>hasta)throw new CajaError("Paginación o rango inválido. Tamaño: 1 a 200; fechas UTC, fin exclusivo.");}
 [HttpGet("inventario/historial")]
 public async Task<IActionResult> Historial(int pagina=1,int tamano=25,int? productoId=null,string? usuario=null,DateTime? desde=null,DateTime? hasta=null,string? buscar=null){Page(pagina,tamano,desde,hasta);var q=db.MovimientosStock.AsNoTracking().AsQueryable();if(productoId!=null)q=q.Where(m=>m.ProductoId==productoId);if(!string.IsNullOrWhiteSpace(usuario))q=q.Where(m=>m.Usuario==usuario);if(desde!=null)q=q.Where(m=>m.FechaUtc>=desde);if(hasta!=null)q=q.Where(m=>m.FechaUtc<hasta);if(!string.IsNullOrWhiteSpace(buscar))q=q.Where(m=>m.Motivo.Contains(buscar));var total=await q.CountAsync();return Ok(new{pagina,tamano,total,totalPaginas=(int)Math.Ceiling((double)total/tamano),items=await q.OrderByDescending(m=>m.FechaUtc).ThenByDescending(m=>m.Id).Skip((pagina-1)*tamano).Take(tamano).Select(m=>new{m.Id,m.ProductoId,producto=m.Producto.Nombre,m.Anterior,m.Posterior,m.Motivo,m.Usuario,m.FechaUtc}).ToListAsync()});}
 [HttpGet("acciones/historial")]
 public async Task<IActionResult> Acciones(int pagina=1,int tamano=25,string? usuario=null,string? accion=null,DateTime? desde=null,DateTime? hasta=null,string? buscar=null){Page(pagina,tamano,desde,hasta);var q=db.AccionesAdministrativas.AsNoTracking().AsQueryable();if(!string.IsNullOrWhiteSpace(usuario))q=q.Where(m=>m.Usuario==usuario);if(!string.IsNullOrWhiteSpace(accion))q=q.Where(m=>m.Accion.Contains(accion));if(desde!=null)q=q.Where(m=>m.FechaUtc>=desde);if(hasta!=null)q=q.Where(m=>m.FechaUtc<hasta);if(!string.IsNullOrWhiteSpace(buscar))q=q.Where(m=>m.Detalle.Contains(buscar));var total=await q.CountAsync();return Ok(new{pagina,tamano,total,totalPaginas=(int)Math.Ceiling((double)total/tamano),items=await q.OrderByDescending(m=>m.FechaUtc).ThenByDescending(m=>m.Id).Skip((pagina-1)*tamano).Take(tamano).ToListAsync()});}
}
