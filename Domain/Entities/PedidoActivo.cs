// Domain/Entities/PedidoActivo.cs
using System;
using System.Collections.Generic;

namespace KaraokeNoeliasApi.Domain.Entities;

public class PedidoActivo
{
    public int Id { get; set; }
    public int MesaId { get; set; }
    public Mesa Mesa { get; set; }
    public decimal Total { get; set; }
    public DateTime RegistradoEn { get; set; } = DateTime.Now;
    public List<PedidoDetalle> Detalles { get; set; } = new List<PedidoDetalle>();
    public string? MetodoPago { get; set; }
    public decimal PagoCliente { get; set; }
}

public class PedidoDetalle
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public PedidoActivo Pedido { get; set; }
    public int ProductoId { get; set; }
    public Producto Producto { get; set; }
    public int Cantidad { get; set; }
    public decimal Total { get; set; }
}