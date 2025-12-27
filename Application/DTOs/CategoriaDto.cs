namespace KaraokeNoeliasApi.Application.DTOs
{
    public class CategoriaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public IFormFile? Imagen { get; set; } // Para subir la imagen
        public bool Estado { get; set; }
        public string? ImagenUrl { get; set; } // Opcional, se usa solo para respuesta
    }
}