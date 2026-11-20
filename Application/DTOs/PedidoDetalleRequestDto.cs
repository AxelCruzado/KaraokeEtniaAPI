namespace KaraokeEtniaApi.Application.DTOs
{
    public record EleccionCarta(int ProductoId,int Cantidad);
    public class PedidoDetalleRequestDto
    {
        public Dictionary<string,List<EleccionCarta>> Opciones {get;set;}=new();
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
    }
}

