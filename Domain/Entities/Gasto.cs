using System.ComponentModel.DataAnnotations.Schema;

namespace KaraokeNoeliasApi.Domain.Entities
{
    public class Gasto
    {
        public int Id { get; set; }

        [Column(TypeName = "DECIMAL(10,2)")]
        public decimal Monto { get; set; }
        public string Descripcion { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
        public string Tipo { get; set; } // "Diario", "Semanal", "Mensual"
    }
}
