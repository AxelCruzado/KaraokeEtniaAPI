namespace KaraokeNoeliasApi.Application.DTOs
{
    public class RegistrarUsuarioDto
    {
        public string NombreUsuario { get; set; }
        public string Contraseña { get; set; }
        public string Rol { get; set; } // Cliente, Mesera, Administrador, Musica
    }
}
