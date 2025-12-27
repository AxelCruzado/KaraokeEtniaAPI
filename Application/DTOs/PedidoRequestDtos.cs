namespace KaraokeNoeliasApi.Application.DTOs
{
    public class PedidoRequestDto
    {
        public List<PedidoDetalleRequestDto> Detalles { get; set; }
        public decimal PagoCliente { get; set; } = 0; // Pago por defecto 0
        public string MetodoPago { get; set; } // "Efectivo", "Tarjeta", "Yape"
    }

}
