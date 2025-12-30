// Domain/Entities/SolicitudCancion.cs
namespace KaraokeEtniaApi.Domain.Entities;

public class SolicitudCancion
{
    public int Id { get; set; }
    public int MesaId { get; set; }
    public Mesa Mesa { get; set; }
    public string Titulo { get; set; } // Nombre de la canción ingresado por el cliente
    public string Artista { get; set; } // Artista ingresado por el cliente
    public TipoSolicitud Tipo { get; set; } // Escuchar o Cantar
    public DateTime RegistradoEn { get; set; } = DateTime.Now;
    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;
}

public enum TipoSolicitud
{
    Escuchar,
    Cantar
}

public enum EstadoSolicitud
{
    Pendiente,
    EnReproduccion,
    Completada
}
