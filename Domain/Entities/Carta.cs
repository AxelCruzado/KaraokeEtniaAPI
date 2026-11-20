namespace KaraokeEtniaApi.Domain.Entities;
public class CartaProducto {
 public int ProductoId {get;set;}
 public Producto Producto {get;set;}=null!;
 public string Codigo {get;set;}="";
 public string Tipo {get;set;}="Simple";
 public string Unidad {get;set;}="Unidad";
 public bool Vendible {get;set;}=true;
 public bool HieloInicial {get;set;}
 public string ConfigJson {get;set;}="{}";
}
public class CartaRegistro {
 public string Id {get;set;}=Guid.NewGuid().ToString("N");
 public string Clave {get;set;}="";
 public string Hash {get;set;}="";
 public string Tipo {get;set;}="";
 public int? MesaId {get;set;}
 public int? PedidoId {get;set;}
 public string Usuario {get;set;}="";
 public DateTime FechaUtc {get;set;}=DateTime.UtcNow;
 public string Json {get;set;}="";
}
