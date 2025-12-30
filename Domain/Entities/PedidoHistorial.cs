namespace KaraokeEtniaApi.Domain.Entities;

public class PedidoHistorial
{
    public int Id { get; set; }
    public int MesaId { get; set; }
    public Mesa Mesa { get; set; }
    public decimal Total { get; set; }
    public DateTime RegistradoEn { get; set; }
    public DateTime CerradoEn { get; set; }
    public string? MetodoPago { get; set; }
    public decimal PagoCliente { get; set; }
    public List<PedidoDetalleHistorial> Detalles { get; set; } = new List<PedidoDetalleHistorial>();
}

public class PedidoDetalleHistorial
{
    public int Id { get; set; }
    public int PedidoHistorialId { get; set; }
    public PedidoHistorial PedidoHistorial { get; set; }
    public int ProductoId { get; set; }
    public Producto Producto { get; set; }
    public int Cantidad { get; set; }
    public decimal Total { get; set; }
}
