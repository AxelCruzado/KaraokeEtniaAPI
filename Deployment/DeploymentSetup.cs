using KaraokeEtniaApi.Administracion;
using KaraokeEtniaApi.Domain.Entities;
using KaraokeEtniaApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace KaraokeEtniaApi.Deployment;

public static class DeploymentSetup
{
    public static void Validate(IConfiguration config, IHostEnvironment env)
    {
        if (string.IsNullOrWhiteSpace(config.GetConnectionString("DefaultConnection")))
            throw new InvalidOperationException("Configure ConnectionStrings__DefaultConnection.");
        if (!env.IsDevelopment() && (config["Jwt:Key"]?.Length < 32 || string.IsNullOrWhiteSpace(config["Jwt:Key"])))
            throw new InvalidOperationException("Configure una clave JWT privada de al menos 32 caracteres.");
        if (!env.IsDevelopment() && (string.IsNullOrWhiteSpace(config["Jwt:Issuer"]) || string.IsNullOrWhiteSpace(config["Jwt:Audience"])))
            throw new InvalidOperationException("Configure emisor y audiencia JWT.");
    }

    public static async Task Initialize(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            await db.Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<CajaDbContext>().Database.MigrateAsync();
        }
        // Historical migration seeds this account. Rotate it before serving a public request.
        const string historicalHash = "$2a$11$6YpBKqMTPgGYXBvdV1NFe.QTJN49p6K4zAoblaZx2FEkCsqWFb3CG";
        var admin = await db.Usuarios.SingleOrDefaultAsync(u => u.NombreUsuario == "admin");
        var empty = !await db.Usuarios.AnyAsync();
        var knownDefault = admin != null && (admin.ContraseñaHash == historicalHash || BCrypt.Net.BCrypt.Verify("admin123", admin.ContraseñaHash));
        if (empty || (!app.Environment.IsDevelopment() && knownDefault))
        {
            var password = app.Configuration["Bootstrap:AdminPassword"];
            if (string.IsNullOrEmpty(password) && app.Environment.IsDevelopment()) password = "admin123";
            if (!app.Environment.IsDevelopment() && (string.IsNullOrEmpty(password) || password.Length < 16 || password == "admin123"))
                throw new InvalidOperationException("Configure Bootstrap__AdminPassword con al menos 16 caracteres para la cuenta inicial.");
            if (admin == null)
            {
                admin = new Usuario { NombreUsuario = "admin", Rol = "Administrador", FechaRegistro = DateTime.UtcNow };
                db.Usuarios.Add(admin);
            }
            admin.ContraseñaHash = BCrypt.Net.BCrypt.HashPassword(password!);
            await db.SaveChangesAsync();
        }
        Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath, "images", "productos"));
        Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath, "images", "categorias"));
    }
}
