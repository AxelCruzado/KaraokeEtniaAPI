namespace KaraokeEtniaApi.Application.DTOs
{
    public class ProductoDto
    {
        public int Id { get; set; }
        public int CategoriaId { get; set; }
        public string Nombre { get; set; }
        public IFormFile? Imagen { get; set; } // Para subir la imagen (opcional)
        public string? ImagenUrl { get; set; } // Para almacenar o recibir la URL de la imagen
        public int Stock { get; set; }
        public decimal Precio { get; set; }
        public int TicketsPorUnidad { get; set; }
        public string? CategoriaNombre { get; set; }
    }
}
