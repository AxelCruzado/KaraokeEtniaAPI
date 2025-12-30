namespace KaraokeEtniaApi.Application.DTOs
{
    public class RegistrarUsuarioDto
    {
        public string NombreUsuario { get; set; }
        public string Password { get; set; }
        public string Rol { get; set; } // Cliente, Mesera, Administrador, Musica
    }
}

