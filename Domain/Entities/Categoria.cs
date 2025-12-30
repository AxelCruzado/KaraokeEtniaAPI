using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace KaraokeEtniaApi.Domain.Entities
{
    [Table("Categorias")]
    public class Categoria
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(100)] // Ajusté a 100 para consistencia con Producto
        public string Nombre { get; set; }

        [StringLength(255)]
        public string ImagenUrl { get; set; } // Usamos ImagenUrl como acordamos

        public EstadoCategoria Estado { get; set; } = EstadoCategoria.Activo;

        // Propiedad de navegación para la relación uno-a-muchos
        public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();
    }

    public enum EstadoCategoria
    {
        Activo,
        Inactivo
    }
}
