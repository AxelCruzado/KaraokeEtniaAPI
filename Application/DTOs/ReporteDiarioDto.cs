namespace KaraokeEtniaApi.Application.DTOs
{
    public class ReporteDiarioDto
    {
        public decimal VentaTotal { get; set; }
        public decimal Efectivo { get; set; }
        public decimal Yape { get; set; }
        public List<ProductoVendidoDto> ProductosVendidos { get; set; }
    }

    public class ProductoVendidoDto
    {
        public string NombreProducto { get; set; }
        public int Cantidad { get; set; }
        public decimal Total { get; set; }
    }
}

