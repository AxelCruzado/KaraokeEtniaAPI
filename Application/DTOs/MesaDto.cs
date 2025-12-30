namespace KaraokeEtniaApi.Application.DTOs
{
    public class MesaDto
    {
        public int Id { get; set; }
        public int Numero { get; set; }
        public decimal Saldo { get; set; }
        public int TicketsDisponibles { get; set; }
        public string Estado { get; set; }
        public List<PedidoActivoDto> PedidosActivos { get; set; }
    }
}

