namespace KaraokeNoeliasApi.Application.DTOs
{
    public class SolicitudCancionRequestDto
    {
        public int MesaId { get; set; }
        public string Titulo { get; set; }
        public string Artista { get; set; }
        public string Tipo { get; set; } // "Escuchar" o "Cantar"
    }
}
