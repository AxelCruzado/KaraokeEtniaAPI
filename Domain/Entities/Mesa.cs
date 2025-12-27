// Domain/Entities/Mesa.cs
namespace KaraokeNoeliasApi.Domain.Entities;

public enum EstadoMesa
{
    Disponible,
    Ocupado
}

public class Mesa
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public decimal Saldo { get; set; }
    public int TicketsDisponibles { get; set; }
    public EstadoMesa Estado { get; set; }

    public ICollection<PedidoActivo> PedidosActivos { get; set; }
    public ICollection<SolicitudCancion> SolicitudesCanciones { get; set; } // Relación actualizada
}