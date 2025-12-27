namespace KaraokeNoeliasApi.Application.DTOs
{
    public class GastoRequestDto
    {
        public decimal Monto { get; set; }
        public string Descripcion { get; set; }
        public DateTime Fecha { get; set; }
        public string Tipo { get; set; } // "Diario", "Semanal", "Mensual"
    }
}