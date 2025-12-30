using KaraokeEtniaApi.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KaraokeEtniaApi.Domain.Entities;
using KaraokeEtniaApi.Application.DTOs;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace KaraokeEtniaApi.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductosController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly string _uploadPath = Path.Combine("images", "productos"); // Nueva ruta: wwwroot/images/productos

    public ProductosController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
        var fullPath = Path.Combine(_environment.WebRootPath, _uploadPath);
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetProductos([FromQuery] int? categoriaId)
    {
        var query = _context.Productos
            .Include(p => p.Categoria)
            .AsQueryable();

        if (categoriaId.HasValue)
        {
            query = query.Where(p => p.CategoriaId == categoriaId.Value);
        }

        var productos = await query
            .Select(p => new ProductoDto
            {
                Id = p.Id,
                CategoriaId = p.CategoriaId,
                Nombre = p.Nombre,
                ImagenUrl = p.ImagenUrl,
                Stock = p.Stock,
                Precio = p.Precio,
                TicketsPorUnidad = p.TicketsPorUnidad,
                CategoriaNombre = p.Categoria.Nombre
            })
            .ToListAsync();

        return Ok(productos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProducto(int id)
    {
        var producto = await _context.Productos
            .Include(p => p.Categoria)
            .Where(p => p.Id == id)
            .Select(p => new ProductoDto
            {
                Id = p.Id,
                CategoriaId = p.CategoriaId,
                Nombre = p.Nombre,
                ImagenUrl = p.ImagenUrl,
                Stock = p.Stock,
                Precio = p.Precio,
                TicketsPorUnidad = p.TicketsPorUnidad,
                CategoriaNombre = p.Categoria.Nombre
            })
            .FirstOrDefaultAsync();

        if (producto == null)
        {
            return NotFound();
        }

        return Ok(producto);
    }

    [HttpPost]
    public async Task<IActionResult> CrearProducto([FromForm] ProductoDto productoDto)
    {
        // Validar los datos
        if (string.IsNullOrEmpty(productoDto.Nombre) || productoDto.Stock < 0 || productoDto.Precio < 0 ||
            productoDto.TicketsPorUnidad < 0 || productoDto.CategoriaId <= 0)
        {
            return BadRequest("Datos inválidos.");
        }

        var categoriaExiste = await _context.Categorias.AnyAsync(c => c.Id == productoDto.CategoriaId);
        if (!categoriaExiste)
        {
            return BadRequest($"La categoría con ID {productoDto.CategoriaId} no existe.");
        }

        string imagenUrl = null;
        if (productoDto.Imagen != null && productoDto.Imagen.Length > 0)
        {
            var extension = Path.GetExtension(productoDto.Imagen.FileName).ToLower();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { Message = "Formato de imagen no permitido. Use JPG, JPEG, PNG o WEBP." });
            }

            if (productoDto.Imagen.Length > 5 * 1024 * 1024)
            {
                return BadRequest(new { Message = "La imagen no puede exceder los 5MB." });
            }

            imagenUrl = await SaveImage(productoDto.Imagen);
            if (string.IsNullOrEmpty(imagenUrl))
            {
                return StatusCode(500, new { Message = "Error al guardar la imagen." });
            }
        }

        var producto = new Producto
        {
            Nombre = productoDto.Nombre,
            ImagenUrl = imagenUrl,
            Stock = productoDto.Stock,
            Precio = productoDto.Precio,
            TicketsPorUnidad = productoDto.TicketsPorUnidad,
            CategoriaId = productoDto.CategoriaId
        };

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();

        var productoDtoResponse = new ProductoDto
        {
            Id = producto.Id,
            CategoriaId = producto.CategoriaId,
            Nombre = producto.Nombre,
            ImagenUrl = producto.ImagenUrl, // Asignar a ImagenUrl, no a Imagen
            Stock = producto.Stock,
            Precio = producto.Precio,
            TicketsPorUnidad = producto.TicketsPorUnidad,
            CategoriaNombre = (await _context.Categorias.FindAsync(producto.CategoriaId)).Nombre
        };

        return CreatedAtAction(nameof(GetProducto), new { id = producto.Id }, productoDtoResponse);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> ActualizarProducto(int id, [FromForm] ProductoDto productoDto)
    {
        var producto = await _context.Productos.FindAsync(id);
        if (producto == null)
        {
            return NotFound(new { Message = "Producto no encontrado." });
        }

        if (string.IsNullOrEmpty(productoDto.Nombre))
        {
            return BadRequest(new { Message = "El nombre del producto es requerido." });
        }

        string imageUrl = producto.ImagenUrl;
        if (productoDto.Imagen != null)
        {
            var extension = Path.GetExtension(productoDto.Imagen.FileName).ToLower();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { Message = "Formato de imagen no permitido. Use JPG, JPEG, PNG o WEBP." });
            }

            if (productoDto.Imagen.Length > 5 * 1024 * 1024)
            {
                return BadRequest(new { Message = "La imagen no puede exceder los 5MB." });
            }

            // Eliminar la imagen anterior si existe
            if (!string.IsNullOrEmpty(producto.ImagenUrl))
            {
                var oldImagePath = Path.Combine(_environment.WebRootPath, producto.ImagenUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldImagePath))
                {
                    System.IO.File.Delete(oldImagePath);
                }
            }

            imageUrl = await SaveImage(productoDto.Imagen);
            if (string.IsNullOrEmpty(imageUrl))
            {
                return StatusCode(500, new { Message = "Error al guardar la imagen." });
            }
        }

        producto.Nombre = productoDto.Nombre;
        producto.ImagenUrl = imageUrl;
        producto.CategoriaId = productoDto.CategoriaId;
        producto.Stock = productoDto.Stock;
        producto.Precio = productoDto.Precio;
        producto.TicketsPorUnidad = productoDto.TicketsPorUnidad;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> EliminarProducto(int id)
    {
        var producto = await _context.Productos.FindAsync(id);
        if (producto == null)
        {
            return NotFound("Producto no encontrado.");
        }

        var pedidosAsociados = await _context.PedidoDetalles.AnyAsync(pd => pd.ProductoId == id);
        if (pedidosAsociados)
        {
            return BadRequest("No se puede eliminar el producto porque está asociado a pedidos activos.");
        }

        if (!string.IsNullOrEmpty(producto.ImagenUrl))
        {
            var imagePath = Path.Combine(_environment.WebRootPath, producto.ImagenUrl.TrimStart('/'));
            if (System.IO.File.Exists(imagePath))
            {
                System.IO.File.Delete(imagePath);
            }
        }

        _context.Productos.Remove(producto);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private async Task<string> SaveImage(IFormFile image)
    {
        try
        {
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
            var filePath = Path.Combine(_environment.WebRootPath, _uploadPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            return $"{baseUrl}/images/productos/{fileName}"; 
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al guardar la imagen: {ex.Message}");
            return null;
        }
    }
}
