using KaraokeEtniaApi.Application.DTOs;
namespace KaraokeEtniaApi.Administracion;
public class ComponenteCarta {public int ProductoId{get;set;} public string Codigo{get;set;}=""; public int Cantidad{get;set;}=1;public decimal Recargo{get;set;} }
public class GrupoCarta {public string Clave{get;set;}="";public string Nombre{get;set;}="";public int Cantidad{get;set;}=1; public List<ComponenteCarta> Opciones{get;set;}=[];}
public class ReglaCarta {public bool RequiereHabilitacion{get;set;}public List<ComponenteCarta> Fijos{get;set;}=[];public List<GrupoCarta> Grupos{get;set;}=[];public string Familia{get;set;}="";public int? ContenidoMl{get;set;} }
public class DefinicionCarta {public string Codigo{get;set;}="";public string Nombre{get;set;}="";public string Categoria{get;set;}="";public decimal Precio{get;set;}public int Tickets{get;set;}public string Tipo{get;set;}="Simple";public string Unidad{get;set;}="Unidad";public bool Vendible{get;set;}=true;public bool HieloInicial{get;set;}public ReglaCarta Regla{get;set;}=new();}
public record ConfigurarCarta(List<DefinicionCarta> Productos);
public record AperturaBotella(string ClaveOperacion,int ProductoCerradoId,int InsumoId,int Botellas,int MililitrosPorBotella,string Motivo);
public record HieloInput(string ClaveOperacion,int Porciones);
public class PlanLineaCarta {public int ProductoId{get;set;}public int Cantidad{get;set;}public decimal PrecioUnitario{get;set;}public decimal Total=>PrecioUnitario*Cantidad;public int Tickets{get;set;}public string Descripcion{get;set;}="";}
public class PlanCarta {public decimal Total=>Lineas.Sum(x=>x.Total);public int Tickets=>Lineas.Sum(x=>x.Tickets);public List<PlanLineaCarta> Lineas{get;set;}=[];public Dictionary<int,int> Consumos{get;set;}=new();public List<string> Faltantes{get;set;}=[];public bool Disponible=>Faltantes.Count==0;}

public record SaldoCartaDemo(string Codigo,int Cantidad,int Minimo,decimal CostoReferencia);
public record AperturaCartaDemo(string CodigoOrigen,string CodigoDestino,int Botellas,int MililitrosPorBotella,string Referencia);
public record CargaCartaDemo(string ClaveOperacion,string Fuente,List<SaldoCartaDemo> Saldos,List<AperturaCartaDemo> Aperturas);
