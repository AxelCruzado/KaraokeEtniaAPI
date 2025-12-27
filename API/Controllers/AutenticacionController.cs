// API/Controllers/AutenticacionController.cs
using KaraokeNoeliasApi.Domain.Entities;
using KaraokeNoeliasApi.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using KaraokeNoeliasApi.Application.DTOs;

namespace KaraokeNoeliasApi.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AutenticacionController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AutenticacionController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        try
        {
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.NombreUsuario == loginDto.NombreUsuario);
            if (usuario == null)
            {
                return Unauthorized("Usuario o contraseña incorrectos.");
            }

            if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, usuario.ContraseñaHash))
            {
                return Unauthorized("Usuario o contraseña incorrectos.");
            }

            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, usuario.NombreUsuario),
                    new Claim(ClaimTypes.Role, usuario.Rol)
                }),
                Expires = DateTime.UtcNow.AddHours(1),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            return Ok(new { Token = tokenString, Rol = usuario.Rol });
        }
        catch (Exception ex)
        {
            return BadRequest($"Error al iniciar sesión: {ex.Message}");
        }
    }

    [HttpPost("registrar")]
    [Authorize(Roles = "Administrador")] // Solo administradores pueden registrar usuarios
    public async Task<IActionResult> Registrar([FromBody] RegistrarUsuarioDto registrarDto)
    {
        try
        {
            // Validar que el nombre de usuario no exista
            if (await _context.Usuarios.AnyAsync(u => u.NombreUsuario == registrarDto.NombreUsuario))
            {
                return BadRequest("El nombre de usuario ya existe.");
            }

            // Determinar el rol: si no hay usuarios, el primero será Administrador
            string rolFinal = registrarDto.Rol;
            if (!await _context.Usuarios.AnyAsync()) // Si la tabla está vacía
            {
                rolFinal = "Administrador"; // El primer usuario será Administrador por defecto
            }
            else
            {
                // Validar el rol si no es el primer usuario
                if (!Enum.TryParse<Rol>(registrarDto.Rol, true, out var rol))
                {
                    return BadRequest("Rol inválido. Debe ser 'Cliente', 'Mesera', 'Administrador' o 'Musica'.");
                }
                rolFinal = registrarDto.Rol;
            }

            // Hashear la contraseña con BCrypt
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(registrarDto.Contraseña);

            // Crear el nuevo usuario
            var nuevoUsuario = new Usuario
            {
                NombreUsuario = registrarDto.NombreUsuario,
                ContraseñaHash = hashedPassword,
                Rol = rolFinal,
                FechaRegistro = DateTime.Now
            };

            // Guardar en la base de datos
            _context.Usuarios.Add(nuevoUsuario);
            await _context.SaveChangesAsync();

            return Ok("Usuario registrado exitosamente.");
        }
        catch (Exception ex)
        {
            return BadRequest($"Error al registrar usuario: {ex.Message}");
        }
    }
}

public class LoginDto
{
    public string NombreUsuario { get; set; }
    public string Password { get; set; }
}