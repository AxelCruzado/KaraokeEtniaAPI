namespace KaraokeNoeliasApi.Application.DTOs
{
    public class PedidoDetalleDto
    {
        public int Id { get; set; }
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public decimal Total { get; set; }
        public ProductoDto Producto { get; set; }
    }
}
