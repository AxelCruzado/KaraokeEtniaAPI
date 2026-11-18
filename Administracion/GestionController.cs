using KaraokeEtniaApi.Domain.Entities;
using KaraokeEtniaApi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace KaraokeEtniaApi.Administracion;
public record Ajuste(int ProductoId,int Cantidad,string Motivo,int StockEsperado);
public record Minimo(int Cantidad);
public record CompraInput(int ProveedorId,string Documento,List<LineaInput> Lineas);
public record LineaInput(int ProductoId,int Cantidad,decimal CostoUnitario);
public record Recepcion(string TurnoId,List<PagoInput> Pagos,DateTime FechaOperacion);
public record EstadoUsuario(bool Activo,string Rol,string Motivo);
[ApiController,Route("api/gestion"),Authorize(Roles="Administrador,Cajero"),TypeFilter(typeof(CajaExceptionFilter))]
public class GestionController(AppDbContext db,CajaDbContext ledger,CajaService caja):ControllerBase
{
 string Who=>User.Identity!.Name!;
 void Audit(string action,string detail)=>db.AccionesAdministrativas.Add(new(){Usuario=Who,Accion=action,Detalle=detail});
 [HttpGet("resumen")]
 public async Task<IActionResult> Resumen()=>Ok(new { productos=await db.Productos.AsNoTracking().OrderBy(p=>p.Nombre).Select(p=>new{p.Id,p.Nombre,p.Stock,p.StockMinimo,reponer=p.Stock<=p.StockMinimo}).ToListAsync(), proveedores=await db.Proveedores.AsNoTracking().ToListAsync(), compras=await db.Compras.AsNoTracking().Include(c=>c.Lineas).ThenInclude(l=>l.Producto).Include(c=>c.Proveedor).Select(c=>new {c.Id,c.Documento,c.Estado,c.FechaUtc,c.Usuario,c.MovimientoCajaId,proveedor=c.Proveedor.Nombre,total=c.Lineas.Sum(l=>l.Cantidad*l.CostoUnitario),lineas=c.Lineas.Select(l=>new{l.ProductoId,producto=l.Producto.Nombre,l.Cantidad,l.CostoUnitario})}).ToListAsync(), movimientos=await db.MovimientosStock.AsNoTracking().OrderByDescending(m=>m.Id).Take(200).Select(m=>new{m.Id,m.ProductoId,producto=m.Producto.Nombre,m.Anterior,m.Posterior,m.Motivo,m.Usuario,m.FechaUtc}).ToListAsync(), acciones=await db.AccionesAdministrativas.AsNoTracking().OrderByDescending(a=>a.Id).Take(100).ToListAsync() });
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
  // One transaction spans receipt, inventory ledger and the corresponding cash expense.
  await ledger.Database.CloseConnectionAsync();ledger.Database.SetDbConnection(db.Database.GetDbConnection());
  await using var tx=await db.Database.BeginTransactionAsync();await ledger.Database.UseTransactionAsync(tx.GetDbTransaction());
  var rows=await db.Compras.FromSqlInterpolated($"SELECT * FROM Compras WHERE Id={id} FOR UPDATE").ToListAsync();var c=rows.SingleOrDefault()??throw new CajaError("Compra no encontrada.",404);
  if(c.Estado!="Pendiente")throw new CajaError("Compra ya recibida; no se duplica stock ni egreso.",409);
  await db.Entry(c).Collection(x=>x.Lineas).LoadAsync();var turno=await ledger.Turnos.SingleOrDefaultAsync(t=>t.Id==i.TurnoId)??throw new CajaError("Turno no encontrado.");
  foreach(var l in c.Lineas){var product=await db.Productos.FindAsync(l.ProductoId)??throw new CajaError("Producto no encontrado.");product.Stock=checked(product.Stock+l.Cantidad);}
  var amount=c.Lineas.Sum(l=>l.Cantidad*l.CostoUnitario);var mov=await caja.Registrar(i.TurnoId,new MovimientoInput("compra-"+id,"Egreso","Gasto","Compra "+c.Documento,"Insumos",c.Documento,amount,i.FechaOperacion,i.Pagos),Who);
  c.Estado="Recibida";c.MovimientoCajaId=mov.Id;db.MotivoStock="Recepción de compra "+c.Documento;Audit("Recepción de compra",$"Compra {id}; egreso {mov.Codigo}");await db.SaveChangesAsync();await tx.CommitAsync();return Ok(new{c.Id,c.Estado,c.MovimientoCajaId});
 }
 [HttpGet("usuarios"),Authorize(Roles="Administrador")]
 public async Task<IActionResult> Usuarios()=>Ok(await db.Usuarios.Select(u=>new{u.Id,u.NombreUsuario,u.Rol,u.Activo}).ToListAsync());
 [HttpPost("usuarios/{id}/estado"),Authorize(Roles="Administrador")]
 public async Task<IActionResult> Usuario(int id,EstadoUsuario i){if(string.IsNullOrWhiteSpace(i.Motivo)||!Enum.TryParse<Rol>(i.Rol,out var role)||!Enum.IsDefined(role))throw new CajaError("Rol o motivo inválido.");var u=await db.Usuarios.FindAsync(id)??throw new CajaError("Usuario no encontrado.",404);if(u.NombreUsuario==Who)throw new CajaError("No puede desactivar ni cambiar su propio rol.");Audit("Permisos/estado de usuario",$"{u.NombreUsuario}: {u.Rol}/{u.Activo} → {i.Rol}/{i.Activo}; {i.Motivo}");u.Activo=i.Activo;u.Rol=i.Rol;await db.SaveChangesAsync();return Ok(new{u.Id,u.Activo,u.Rol});}
}
