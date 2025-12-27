namespace KaraokeNoeliasApi.Application.DTOs
{
    public class PedidoActivoDto
    {
        public int Id { get; set; }
        public int MesaId { get; set; }
        public decimal Total { get; set; }
        public DateTime RegistradoEn { get; set; }
        public List<PedidoDetalleDto> Detalles { get; set; }
        public string? MetodoPago { get; set; }
        public decimal PagoCliente { get; set; }
    }
}
