using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KaraokeEtniaApi.Application.DTOs;
using KaraokeEtniaApi.Domain.Entities;
using KaraokeEtniaApi.Infrastructure.Data;
using KaraokeEtniaApi.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
namespace KaraokeEtniaApi.Administracion;

public class CartaService(AppDbContext db) {
 public static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
 static string Dump(object value)=>JsonSerializer.Serialize(value,Json);
 static string Fingerprint(object input,string who)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(who+Dump(input))));
 static string Key(string? value)=>string.IsNullOrWhiteSpace(value)?Guid.NewGuid().ToString("N"):value.Length<=100?value:throw new CajaError("Clave de operación inválida.");
 static ReglaCarta Rule(CartaProducto p)=>JsonSerializer.Deserialize<ReglaCarta>(p.ConfigJson,Json)??new();
 public async Task<object> Configurar(ConfigurarCarta input,string who) {
  if(input.Productos==null||input.Productos.Count is <1 or >200||input.Productos.Select(x=>x.Codigo).Distinct().Count()!=input.Productos.Count)throw new CajaError("La carta debe tener códigos únicos.");
  foreach(var i in input.Productos)if(string.IsNullOrWhiteSpace(i.Codigo)||i.Codigo.Length>70||string.IsNullOrWhiteSpace(i.Nombre)||i.Nombre.Length>100||string.IsNullOrWhiteSpace(i.Categoria)||i.Categoria.Length>100||i.Precio<0||decimal.Round(i.Precio,2)!=i.Precio||i.Tickets<0||i.Tickets>200||!new[]{"Simple","Preparado","Paquete","Insumo","Hielo"}.Contains(i.Tipo))throw new CajaError("Definición de producto inválida.");
  await using var tx=await db.Database.BeginTransactionAsync();
  var cats=await db.Categorias.ToListAsync();var configs=await db.CartaProductos.Include(x=>x.Producto).ToListAsync();
  var map=new Dictionary<string,CartaProducto>();
  foreach(var i in input.Productos) {
   var cat=cats.SingleOrDefault(c=>c.Nombre==i.Categoria);
   if(cat==null){cat=new Categoria{Nombre=i.Categoria,Estado=EstadoCategoria.Activo,ImagenUrl=""};db.Categorias.Add(cat);cats.Add(cat);}
   var existing=configs.SingleOrDefault(c=>c.Codigo==i.Codigo);
   if(existing==null){existing=new CartaProducto{Codigo=i.Codigo,Producto=new Producto{Nombre=i.Nombre,ImagenUrl="",Stock=0}};db.CartaProductos.Add(existing);configs.Add(existing);}
   existing.Producto.Nombre=i.Nombre;existing.Producto.Categoria=cat;existing.Producto.Precio=i.Precio;existing.Producto.TicketsPorUnidad=i.Tickets;
   existing.Tipo=i.Tipo;existing.Unidad=i.Unidad;existing.Vendible=i.Vendible;existing.HieloInicial=i.HieloInicial;map[i.Codigo]=existing;
  }
  await db.SaveChangesAsync();
  foreach(var i in input.Productos){
   foreach(var component in i.Regla.Fijos.Concat(i.Regla.Grupos.SelectMany(g=>g.Opciones))){
    if(!map.TryGetValue(component.Codigo,out var target)||target.Tipo is "Preparado" or "Paquete"||component.Cantidad<=0||component.Cantidad>100000||component.Recargo<0||decimal.Round(component.Recargo,2)!=component.Recargo)throw new CajaError("Componente inválido en "+i.Nombre);
    component.ProductoId=target.ProductoId;
   }
   if(i.Regla.Grupos.Select(g=>g.Clave).Distinct().Count()!=i.Regla.Grupos.Count||i.Regla.Grupos.Any(g=>string.IsNullOrWhiteSpace(g.Clave)||g.Cantidad<=0||g.Cantidad>100||g.Opciones.Count==0||g.Opciones.Select(o=>o.ProductoId).Distinct().Count()!=g.Opciones.Count))throw new CajaError("Opciones inválidas en "+i.Nombre);
   if(i.Tipo is "Preparado" or "Paquete" && i.Regla.Fijos.Count+i.Regla.Grupos.Count==0)throw new CajaError("Falta composición de "+i.Nombre);
   if(i.HieloInicial&&!map.Values.Any(x=>x.Tipo=="Hielo"))throw new CajaError("Falta definir hielo.");
   map[i.Codigo].ConfigJson=Dump(i.Regla);
  }
  db.CartaRegistros.Add(new(){Clave=Guid.NewGuid().ToString("N"),Tipo="Configuración de carta",Usuario=who,Hash="",Json=Dump(new{codigos=input.Productos.Select(x=>x.Codigo),nota="Precios/reglas configurados; existencias previas conservadas y productos nuevos con saldo registrado cero hasta el conteo."})});
  await db.SaveChangesAsync();await tx.CommitAsync();return new{productos=map.Values.Select(x=>new{x.Codigo,x.ProductoId}),message="Carta configurada. Registra las existencias reales antes de operar."};
 }
 async Task<(Dictionary<int,Producto> products,Dictionary<int,CartaProducto> cards)> Data(){return ((await db.Productos.Include(p=>p.Categoria).ToListAsync()).ToDictionary(x=>x.Id),(await db.CartaProductos.ToListAsync()).ToDictionary(x=>x.ProductoId));}
 public async Task<object> Catalogo(bool inventario,int? categoriaId){
  var (products,cards)=await Data();var ice=cards.Values.SingleOrDefault(x=>x.Tipo=="Hielo");
  return products.Values.Where(p=>inventario?(!cards.TryGetValue(p.Id,out var c)||c.Tipo is not ("Preparado" or "Paquete")):(p.Categoria.Estado==EstadoCategoria.Activo&&(!cards.TryGetValue(p.Id,out var cc)||cc.Vendible))).Where(p=>categoriaId==null||p.CategoriaId==categoriaId).Select(p=>{
   cards.TryGetValue(p.Id,out var c);var rule=c==null?new ReglaCarta():Rule(c);var disponible=p.Stock;
   if(c?.Tipo is "Preparado" or "Paquete"){
    disponible=int.MaxValue;
    foreach(var f in rule.Fijos)disponible=Math.Min(disponible,products[f.ProductoId].Stock/f.Cantidad);
    foreach(var g in rule.Grupos)disponible=Math.Min(disponible,g.Opciones.Sum(o=>products[o.ProductoId].Stock/o.Cantidad)/g.Cantidad);
   }
   if(c?.HieloInicial==true)disponible=Math.Min(disponible,ice==null?0:products[ice.ProductoId].Stock);
   return new{p.Id,p.Nombre,p.Precio,p.TicketsPorUnidad,p.CategoriaId,categoriaNombre=p.Categoria.Nombre,p.ImagenUrl,stock=Math.Max(0,disponible),stockRegistrado=p.Stock,p.StockMinimo,codigo=c?.Codigo,unidad=c?.Unidad??"Unidad",tipo=c?.Tipo??"Simple",vendible=c?.Vendible??true,hieloInicial=c?.HieloInicial??false,familia=rule.Familia,contenidoMl=rule.ContenidoMl,grupos=rule.Grupos.Select(g=>new{g.Clave,g.Nombre,g.Cantidad,opciones=g.Opciones.Select(o=>new{o.ProductoId,nombre=products[o.ProductoId].Nombre,o.Recargo,consumo=o.Cantidad,stock=products[o.ProductoId].Stock})})};
  }).OrderBy(x=>x.Nombre).ToList();
 }
 async Task<bool> Habilitada(int mesaId)=>await db.PedidoDetalles.AnyAsync(d=>d.Pedido.MesaId==mesaId&&db.CartaProductos.Any(c=>c.ProductoId==d.ProductoId&&c.HieloInicial));
 async Task<PlanCarta> Plan(int mesaId,PedidoRequestDto input,Dictionary<int,Producto> products,Dictionary<int,CartaProducto> cards){
  if(input.Detalles==null||input.Detalles.Count is <1 or >100||input.Detalles.Any(x=>x==null))throw new CajaError("Incluye productos en el pedido.");
  var plan=new PlanCarta();var eligible=await Habilitada(mesaId)||input.Detalles.Any(d=>cards.TryGetValue(d.ProductoId,out var c)&&c.HieloInicial&&d.Cantidad>0);var ice=cards.Values.SingleOrDefault(x=>x.Tipo=="Hielo");
  void consume(int id,int amount){if(amount<=0||!products.ContainsKey(id))throw new CajaError("Componente inválido.");plan.Consumos[id]=checked(plan.Consumos.GetValueOrDefault(id)+amount);}
  foreach(var d in input.Detalles){
   d.Opciones??=new();
   if(d.Cantidad is <1 or >1000||!products.TryGetValue(d.ProductoId,out var p)||p.Categoria.Estado!=EstadoCategoria.Activo)throw new CajaError("Producto o cantidad inválidos.");
   cards.TryGetValue(p.Id,out var card);if(card?.Vendible==false)throw new CajaError("El insumo no se vende directamente.");
   var rule=card==null?new ReglaCarta():Rule(card);var price=p.Precio;var notes=new List<string>();
   if(rule.RequiereHabilitacion&&!eligible)throw new CajaError("Este adicional requiere una botella o combo habilitante en la atención.");
   if(card?.Tipo is "Preparado" or "Paquete"){
    foreach(var f in rule.Fijos){consume(f.ProductoId,checked(f.Cantidad*d.Cantidad));notes.Add(products[f.ProductoId].Nombre+" × "+f.Cantidad);}
    if(d.Opciones.Keys.Any(k=>!rule.Grupos.Any(g=>g.Clave==k)))throw new CajaError("Opción desconocida.");
    foreach(var g in rule.Grupos){
     if(!d.Opciones.TryGetValue(g.Clave,out var choices)||choices==null||choices.Count==0||choices.Any(x=>x.Cantidad<=0||x.Cantidad>100)||choices.Sum(x=>x.Cantidad)!=g.Cantidad||choices.Select(x=>x.ProductoId).Distinct().Count()!=choices.Count)throw new CajaError("Selecciona "+g.Cantidad+" en "+g.Nombre+".");
     foreach(var choice in choices){var o=g.Opciones.SingleOrDefault(o=>o.ProductoId==choice.ProductoId)??throw new CajaError("Elección no permitida en "+g.Nombre);consume(o.ProductoId,checked(o.Cantidad*choice.Cantidad*d.Cantidad));price+=o.Recargo*choice.Cantidad;notes.Add(products[o.ProductoId].Nombre+" × "+choice.Cantidad+(o.Recargo>0?" (+S/ "+o.Recargo+")":""));}
    }
   }else{if(d.Opciones.Count>0)throw new CajaError("Este producto no admite opciones.");consume(p.Id,d.Cantidad);}
   if(card?.Tipo=="Hielo"){price=eligible?0:p.Precio;notes.Add(eligible?"Hielo de cortesía de la atención actual":"Hielo vendido por porción de ¼ de bolsa");}
   if(card?.HieloInicial==true){if(ice==null)throw new CajaError("Falta configurar hielo.");consume(ice.ProductoId,d.Cantidad);notes.Add("Incluye "+d.Cantidad+" porción(es) inicial(es) de hielo de ¼ de bolsa, sin cobro");}
   plan.Lineas.Add(new(){ProductoId=p.Id,Cantidad=d.Cantidad,PrecioUnitario=price,Tickets=checked(p.TicketsPorUnidad*d.Cantidad),Descripcion=string.Join("; ",notes)});
  }
  foreach(var (id,qty) in plan.Consumos)if(products[id].Stock<qty)plan.Faltantes.Add(products[id].Nombre+": requiere "+qty+", disponible "+products[id].Stock);
  return plan;
 }
 public async Task<object> Cotizar(int mesaId,PedidoRequestDto input){if(!await db.Mesas.AnyAsync(x=>x.Id==mesaId&&x.Estado!=EstadoMesa.Inactivo))throw new CajaError("Mesa no disponible.",404);var (p,c)=await Data();return await Plan(mesaId,input,p,c);}
 public async Task<object> Registrar(int mesaId,PedidoRequestDto input,string who){
  var key=Key(input.ClaveOperacion);var hash=Fingerprint(new{mesaId,input.Detalles,input.MetodoPago,input.PagoCliente},who);
  var previous=await db.CartaRegistros.SingleOrDefaultAsync(x=>x.Clave==key);
  if(previous!=null){if(previous.Hash!=hash||previous.Tipo!="Pedido")throw new CajaError("La clave ya se usó con otros datos.",409);return JsonSerializer.Deserialize<JsonElement>(previous.Json);}
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
  var mesa=(await DatabaseRows.Locked<Mesa>(db,mesaId).ToListAsync()).SingleOrDefault()??throw new CajaError("Mesa no encontrada.",404);
  if(mesa.Estado==EstadoMesa.Inactivo)throw new CajaError("La mesa está inactiva.");
  var (products,cards)=await Data();var plan=await Plan(mesaId,input,products,cards);
  if(!plan.Disponible)throw new CajaError("Existencias insuficientes: "+string.Join("; ",plan.Faltantes),409);
  if(input.PagoCliente<0||decimal.Round(input.PagoCliente,2)!=input.PagoCliente||input.PagoCliente<Math.Max(0,plan.Total-mesa.Saldo))throw new CajaError("El pago y saldo no cubren el pedido.");
  var order=new PedidoActivo{MesaId=mesaId,Total=plan.Total,MetodoPago=input.MetodoPago,PagoCliente=input.PagoCliente};
  foreach(var line in plan.Lineas)order.Detalles.Add(new(){ProductoId=line.ProductoId,Cantidad=line.Cantidad,Total=line.Total,Descripcion=line.Descripcion});
  foreach(var (id,qty) in plan.Consumos)products[id].Stock-=qty;
  mesa.Saldo=mesa.Saldo+input.PagoCliente-plan.Total;mesa.TicketsDisponibles=checked(mesa.TicketsDisponibles+plan.Tickets);mesa.Estado=EstadoMesa.Ocupado;
  db.MotivoStock="Consumo de pedido de carta en mesa "+mesa.Numero;db.PedidosActivos.Add(order);await db.SaveChangesAsync();
  var result=new{message="Pedido registrado.",pedidoId=order.Id,plan.Total,plan.Tickets,plan.Lineas,consumos=plan.Consumos.Select(x=>new{productoId=x.Key,cantidad=x.Value,unidad=cards.GetValueOrDefault(x.Key)?.Unidad??"Unidad"})};
  db.CartaRegistros.Add(new(){Clave=key,Hash=hash,Tipo="Pedido",MesaId=mesaId,PedidoId=order.Id,Usuario=who,Json=Dump(result)});
  await db.SaveChangesAsync();await tx.CommitAsync();return result;
 }
 public async Task<object> AbrirBotella(AperturaBotella input,string who){
  var key=Key(input.ClaveOperacion);var hash=Fingerprint(input,who);var previous=await db.CartaRegistros.SingleOrDefaultAsync(x=>x.Clave==key);
  if(previous!=null){if(previous.Hash!=hash||previous.Tipo!="Apertura botella")throw new CajaError("Clave usada con otros datos.",409);return JsonSerializer.Deserialize<JsonElement>(previous.Json);}
  if(input.Botellas is <1 or >100||input.MililitrosPorBotella is <1 or >5000||string.IsNullOrWhiteSpace(input.Motivo)||input.ProductoCerradoId==input.InsumoId)throw new CajaError("Indica botellas, contenido real y motivo.");
  await using var tx=await db.Database.BeginTransactionAsync();var (p,c)=await Data();
  if(!p.TryGetValue(input.ProductoCerradoId,out var closed)||!p.TryGetValue(input.InsumoId,out var opened)||!c.TryGetValue(closed.Id,out var cc)||!c.TryGetValue(opened.Id,out var oc))throw new CajaError("Selecciona productos configurados.");
  var cr=Rule(cc);var or=Rule(oc);
  if(cc.Unidad!="Unidad"||oc.Unidad!="ml"||string.IsNullOrWhiteSpace(cr.Familia)||cr.Familia!=or.Familia||(cr.ContenidoMl!=null&&cr.ContenidoMl!=input.MililitrosPorBotella))throw new CajaError("La presentación y el licor de destino no corresponden.");
  if(closed.Stock<input.Botellas)throw new CajaError("No hay suficientes botellas cerradas.",409);
  closed.Stock-=input.Botellas;opened.Stock=checked(opened.Stock+input.Botellas*input.MililitrosPorBotella);db.MotivoStock="Apertura de botella: "+input.Motivo;
  var result=new{cerradoId=closed.Id,salidaBotellas=input.Botellas,insumoId=opened.Id,entradaMl=input.Botellas*input.MililitrosPorBotella,saldoMl=opened.Stock};
  db.CartaRegistros.Add(new(){Clave=key,Hash=hash,Tipo="Apertura botella",Usuario=who,Json=Dump(result)});await db.SaveChangesAsync();await tx.CommitAsync();return result;
 }
 public async Task<object> Demostracion(CargaCartaDemo input,string who){
  var key=Key(input.ClaveOperacion);var hash=Fingerprint(input,who);
  var previous=await db.CartaRegistros.SingleOrDefaultAsync(x=>x.Clave==key);
  if(previous!=null){if(previous.Hash!=hash||previous.Tipo!="Carga demostración")throw new CajaError("Lote usado con otros datos.",409);return JsonSerializer.Deserialize<JsonElement>(previous.Json);}
  if(string.IsNullOrWhiteSpace(input.Fuente)||input.Fuente.Length>2000||input.Saldos==null||input.Aperturas==null||input.Saldos.Count is <1 or >200||input.Saldos.Select(x=>x.Codigo).Distinct().Count()!=input.Saldos.Count||input.Saldos.Any(x=>x.Cantidad<0||x.Minimo<0||x.Cantidad>10000000||x.CostoReferencia<0))throw new CajaError("Saldos de demostración inválidos.");
  await using var tx=await db.Database.BeginTransactionAsync();
  var (products,cards)=await Data();var by=cards.Values.ToDictionary(x=>x.Codigo);
  foreach(var row in input.Saldos){
   if(!by.TryGetValue(row.Codigo,out var c)||c.Tipo is "Preparado" or "Paquete")throw new CajaError("El saldo debe corresponder a un producto físico.");
   if(products[c.ProductoId].Stock!=0||await db.MovimientosStock.AnyAsync(x=>x.ProductoId==c.ProductoId))throw new CajaError("El producto ya tiene movimientos; no se reemplazarán sus existencias.",409);
  }
  foreach(var row in input.Saldos){var p=products[by[row.Codigo].ProductoId];p.Stock=row.Cantidad;p.StockMinimo=row.Minimo;}
  db.MotivoStock="DEMO / saldos iniciales ficticios para feria / lote "+key;await db.SaveChangesAsync();
  foreach(var row in input.Aperturas){
   if(!by.TryGetValue(row.CodigoOrigen,out var c)||!by.TryGetValue(row.CodigoDestino,out var o)||!input.Saldos.Any(x=>x.Codigo==c.Codigo)||!input.Saldos.Any(x=>x.Codigo==o.Codigo)||row.Botellas is <1 or >100||row.MililitrosPorBotella is <1 or >5000)throw new CajaError("Apertura de demostración inválida.");
   var cr=Rule(c);var orule=Rule(o);var closed=products[c.ProductoId];var opened=products[o.ProductoId];
   if(c.Unidad!="Unidad"||o.Unidad!="ml"||string.IsNullOrWhiteSpace(cr.Familia)||cr.Familia!=orule.Familia||(cr.ContenidoMl!=null&&cr.ContenidoMl!=row.MililitrosPorBotella)||closed.Stock<row.Botellas)throw new CajaError("Presentación o saldo incompatible en apertura de demostración.");
   closed.Stock-=row.Botellas;opened.Stock=checked(opened.Stock+row.Botellas*row.MililitrosPorBotella);
   db.MotivoStock="DEMO / apertura ficticia / lote "+key;await db.SaveChangesAsync();
  }
  var result=new{lote=key,procedencia="Demostración: no es conteo físico ni operación real del establecimiento",input.Fuente,fechaImportacionUtc=DateTime.UtcNow,saldos=input.Saldos.Select(x=>new{x.Codigo,productoId=by[x.Codigo].ProductoId,inicial=x.Cantidad,final=products[by[x.Codigo].ProductoId].Stock,x.Minimo,x.CostoReferencia}),aperturas=input.Aperturas,nota="Costos de referencia; no se crean comprobantes, compras, ventas ni egresos de caja."};
  db.CartaRegistros.Add(new(){Clave=key,Hash=hash,Tipo="Carga demostración",Usuario=who,Json=Dump(result)});await db.SaveChangesAsync();await tx.CommitAsync();return result;
 }

}
