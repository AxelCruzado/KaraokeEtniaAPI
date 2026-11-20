namespace KaraokeEtniaApi.Application.DTOs
{
    public class PedidoDetalleDto
    {
        public string Descripcion {get;set;}="";
        public int Id { get; set; }
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public decimal Total { get; set; }
        public ProductoDto Producto { get; set; }
    }
}

