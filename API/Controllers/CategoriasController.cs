using KaraokeEtniaApi.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KaraokeEtniaApi.Domain.Entities;
using KaraokeEtniaApi.Application.DTOs;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace KaraokeEtniaApi.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CategoriasController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public CategoriasController(AppDbContext context, IWebHostEnvironment environment, IConfiguration configuration)
    {
        _context = context;
        _environment = environment;
        _configuration = configuration;
    }

    private string GetImagePath()
    {
        var path = Path.Combine(_environment.WebRootPath, _configuration["Storage:ImagePath"]);
        Console.WriteLine($"Ruta de almacenamiento: {path}");
        return path;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategorias()
    {
        var categorias = await _context.Categorias
            .Select(c => new CategoriaDto
            {
                Id = c.Id,
                Nombre = c.Nombre,
                Imagen = null, // No devolvemos el archivo
                Estado = c.Estado == EstadoCategoria.Activo,
                ImagenUrl = c.ImagenUrl // Añadimos la URL
            })
            .ToListAsync();

        return Ok(categorias);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCategoria(int id)
    {
        var categoria = await _context.Categorias
            .Where(c => c.Id == id)
            .Select(c => new CategoriaDto
            {
                Id = c.Id,
                Nombre = c.Nombre,
                Imagen = null,
                Estado = c.Estado == EstadoCategoria.Activo
            })
            .FirstOrDefaultAsync();

        if (categoria == null)
        {
            return NotFound();
        }

        return Ok(categoria);
    }

    [HttpPost]
    public async Task<IActionResult> CrearCategoria([FromForm] CategoriaDto categoriaDto)
    {
        if (string.IsNullOrEmpty(categoriaDto.Nombre))
        {
            return BadRequest(new { Message = "El nombre de la categoría es requerido." });
        }

        string imageUrl = null;
        if (categoriaDto.Imagen != null)
        {
            var extension = Path.GetExtension(categoriaDto.Imagen.FileName).ToLower();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { Message = "Formato de imagen no permitido. Use JPG, JPEG, PNG o WEBP." });
            }

            if (categoriaDto.Imagen.Length > 5 * 1024 * 1024)
            {
                return BadRequest(new { Message = "La imagen no puede exceder los 5MB." });
            }

            imageUrl = await SaveImage(categoriaDto.Imagen);
            if (string.IsNullOrEmpty(imageUrl))
            {
                return StatusCode(500, new { Message = "Error al guardar la imagen." });
            }
        }

        var categoria = new Categoria
        {
            Nombre = categoriaDto.Nombre,
            ImagenUrl = imageUrl, // Usamos el valor generado por SaveImage
            Estado = categoriaDto.Estado ? EstadoCategoria.Activo : EstadoCategoria.Inactivo
        };

        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();

        // Devolvemos la respuesta con la URL generada
        return CreatedAtAction(nameof(GetCategoria), new { id = categoria.Id }, new CategoriaDto
        {
            Id = categoria.Id,
            Nombre = categoria.Nombre,
            Imagen = null,
            Estado = categoria.Estado == EstadoCategoria.Activo,
            ImagenUrl = categoria.ImagenUrl // Aseguramos que devuelva la URL generada
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> ActualizarCategoria(int id, [FromForm] CategoriaDto categoriaDto)
    {
        var categoria = await _context.Categorias.FindAsync(id);
        if (categoria == null)
        {
            return NotFound(new { Message = "Categoría no encontrada." });
        }

        if (string.IsNullOrEmpty(categoriaDto.Nombre))
        {
            return BadRequest(new { Message = "El nombre de la categoría es requerido." });
        }

        string imageUrl = categoria.ImagenUrl;
        if (categoriaDto.Imagen != null)
        {
            var extension = Path.GetExtension(categoriaDto.Imagen.FileName).ToLower();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { Message = "Formato de imagen no permitido. Use JPG, JPEG, PNG o WEBP." });
            }

            if (categoriaDto.Imagen.Length > 5 * 1024 * 1024)
            {
                return BadRequest(new { Message = "La imagen no puede exceder los 5MB." });
            }

            // Eliminar la imagen anterior si existe
            if (!string.IsNullOrEmpty(categoria.ImagenUrl))
            {
                var oldImagePath = Path.Combine(_environment.WebRootPath, categoria.ImagenUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldImagePath))
                {
                    System.IO.File.Delete(oldImagePath);
                }
            }

            imageUrl = await SaveImage(categoriaDto.Imagen);
            if (string.IsNullOrEmpty(imageUrl))
            {
                return StatusCode(500, new { Message = "Error al guardar la imagen." });
            }
        }

        categoria.Nombre = categoriaDto.Nombre;
        categoria.ImagenUrl = imageUrl;
        categoria.Estado = categoriaDto.Estado ? EstadoCategoria.Activo : EstadoCategoria.Inactivo;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> EliminarCategoria(int id)
    {
        var categoria = await _context.Categorias.FindAsync(id);
        if (categoria == null)
        {
            return NotFound(new { Message = "Categoría no encontrada." });
        }

        var productosAsociados = await _context.Productos.AnyAsync(p => p.CategoriaId == id);
        if (productosAsociados)
        {
            return BadRequest(new { Message = "No se puede eliminar la categoría porque tiene productos asociados." });
        }

        _context.Categorias.Remove(categoria);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private async Task<string> SaveImage(IFormFile image)
    {
        try
        {
            var imagePath = Path.Combine(_environment.WebRootPath, "images", "categorias");
            Console.WriteLine($"Ruta de almacenamiento: {imagePath}");
            if (!Directory.Exists(imagePath))
            {
                Console.WriteLine($"Creando directorio: {imagePath}");
                Directory.CreateDirectory(imagePath);
            }

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
            var filePath = Path.Combine(imagePath, fileName);
            Console.WriteLine($"Guardando imagen en: {filePath}");

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            // Devolvemos una URL dinámica basada en la petición actual
            var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            var imageUrl = $"{baseUrl}/images/categorias/{fileName}";
            Console.WriteLine($"Imagen guardada correctamente: {imageUrl}");
            return imageUrl;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al guardar la imagen: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
            return null;
        }
    }

}
