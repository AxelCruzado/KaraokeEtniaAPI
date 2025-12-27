// Domain/Entities/Usuario.cs
namespace KaraokeNoeliasApi.Domain.Entities;

public class Usuario
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; }
    public string ContraseñaHash { get; set; } // Almacenaremos el hash de la contraseña
    public string Rol { get; set; } // Rol: Cliente, Mesera, Administrador
    public DateTime FechaRegistro { get; set; } = DateTime.Now;
}

public enum Rol
{
    Cliente,
    Mesera,
    Administrador,
    Musica
}