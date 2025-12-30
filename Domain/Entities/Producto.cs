using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaraokeEtniaApi.Domain.Entities
{
    [Table("Productos")]
    public class Producto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nombre { get; set; }

        [StringLength(255)]
        public string ImagenUrl { get; set; } // Usamos ImagenUrl para consistencia

        [Required]
        public int Stock { get; set; }

        [Required]
        [Column(TypeName = "DECIMAL(10,2)")]
        public decimal Precio { get; set; }

        [Required]
        public int TicketsPorUnidad { get; set; }

        [Required]
        public int CategoriaId { get; set; }

        // Propiedad de navegación
        public virtual Categoria Categoria { get; set; }
    }
}
