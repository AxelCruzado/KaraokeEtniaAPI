namespace KaraokeNoeliasApi.Application.DTOs
{
    public class SolicitudCancionDto
    {
        public int Id { get; set; }
        public int MesaId { get; set; }
        public int MesaNumero { get; set; }
        public int CancionId { get; set; }
        public string Titulo { get; set; }
        public string Artista { get; set; }
        public string Tipo { get; set; }
        public DateTime RegistradoEn { get; set; }
        public string Estado { get; set; }
    }
}
