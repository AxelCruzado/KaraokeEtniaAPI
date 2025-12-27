using KaraokeNoeliasApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KaraokeNoeliasApi.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public DbSet<Mesa> Mesas { get; set; }
    public DbSet<PedidoActivo> PedidosActivos { get; set; }
    public DbSet<PedidoDetalle> PedidoDetalles { get; set; }
    public DbSet<Producto> Productos { get; set; }
    public DbSet<Categoria> Categorias { get; set; }
    public DbSet<Gasto> Gastos { get; set; }
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<PedidoHistorial> PedidosHistorial { get; set; } // Agregar DbSet para PedidoHistorial
    public DbSet<PedidoDetalleHistorial> PedidoDetallesHistorial { get; set; }
    public DbSet<SolicitudCancion> SolicitudesCanciones { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configuración de Mesa
        modelBuilder.Entity<Mesa>(entity =>
        {
            entity.ToTable("Mesas");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Numero).IsRequired().HasColumnType("INT").HasColumnName("Numero");
            entity.Property(m => m.Saldo).HasColumnType("DECIMAL(10,2)").HasDefaultValue(0.00);
            entity.Property(m => m.TicketsDisponibles).HasColumnType("INT").HasDefaultValue(0);
            entity.Property(m => m.Estado)
                  .IsRequired()
                  .HasDefaultValue(EstadoMesa.Disponible)
                  .HasConversion<string>();

            entity.HasMany(m => m.PedidosActivos)
                  .WithOne(p => p.Mesa)
                  .HasForeignKey(p => p.MesaId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(m => m.SolicitudesCanciones)
                  .WithOne(s => s.Mesa)
                  .HasForeignKey(s => s.MesaId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // Configuración de PedidoActivo (sin cambios)
        modelBuilder.Entity<PedidoActivo>(entity =>
        {
            entity.ToTable("PedidosActivos");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.MesaId).IsRequired().HasColumnType("INT");
            entity.Property(p => p.Total).HasColumnType("DECIMAL(10,2)").IsRequired();
            entity.Property(p => p.RegistradoEn).HasDefaultValueSql("GETDATE()");
            entity.Property(p => p.MetodoPago).HasMaxLength(50);
            entity.Property(p => p.PagoCliente).HasColumnType("DECIMAL(10,2)").HasDefaultValue(0.00);

            entity.HasOne(p => p.Mesa)
                  .WithMany(m => m.PedidosActivos)
                  .HasForeignKey(p => p.MesaId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(p => p.Detalles)
                  .WithOne(d => d.Pedido)
                  .HasForeignKey(d => d.PedidoId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuración de PedidoHistorial
        modelBuilder.Entity<PedidoHistorial>(entity =>
        {
            entity.ToTable("PedidosHistorial");
            entity.HasKey(ph => ph.Id);
            entity.Property(ph => ph.MesaId).IsRequired().HasColumnType("INT");
            entity.Property(ph => ph.Total).HasColumnType("DECIMAL(18,2)").IsRequired().HasPrecision(18, 2);
            entity.Property(ph => ph.RegistradoEn).HasDefaultValueSql("GETDATE()");
            entity.Property(ph => ph.CerradoEn).IsRequired();
            entity.Property(ph => ph.MetodoPago).HasMaxLength(50);
            entity.Property(ph => ph.PagoCliente).HasColumnType("DECIMAL(10,2)").IsRequired();

            entity.HasOne(ph => ph.Mesa)
                  .WithMany()
                  .HasForeignKey(ph => ph.MesaId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(ph => ph.Detalles)
                  .WithOne(pdh => pdh.PedidoHistorial)
                  .HasForeignKey(pdh => pdh.PedidoHistorialId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuración de PedidoDetalle (sin cambios)
        modelBuilder.Entity<PedidoDetalle>(entity =>
        {
            entity.ToTable("PedidoDetalles");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.PedidoId).IsRequired().HasColumnType("INT");
            entity.Property(d => d.ProductoId).IsRequired().HasColumnType("INT");
            entity.Property(d => d.Cantidad).IsRequired();
            entity.Property(d => d.Total).HasColumnType("DECIMAL(10,2)").IsRequired();

            entity.HasOne(d => d.Pedido)
                  .WithMany(p => p.Detalles)
                  .HasForeignKey(d => d.PedidoId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Producto)
                  .WithMany()
                  .HasForeignKey(d => d.ProductoId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configuración de PedidoDetalleHistorial
        modelBuilder.Entity<PedidoDetalleHistorial>(entity =>
        {
            entity.ToTable("PedidoDetallesHistorial");
            entity.HasKey(pdh => pdh.Id);
            entity.Property(pdh => pdh.PedidoHistorialId).IsRequired().HasColumnType("INT");
            entity.Property(pdh => pdh.ProductoId).IsRequired().HasColumnType("INT");
            entity.Property(pdh => pdh.Cantidad).IsRequired();
            entity.Property(pdh => pdh.Total).HasColumnType("DECIMAL(18,2)").IsRequired().HasPrecision(18, 2);

            entity.HasOne(pdh => pdh.PedidoHistorial)
                  .WithMany(ph => ph.Detalles)
                  .HasForeignKey(pdh => pdh.PedidoHistorialId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pdh => pdh.Producto)
                  .WithMany()
                  .HasForeignKey(pdh => pdh.ProductoId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configuración de Producto
        modelBuilder.Entity<Producto>(entity =>
        {
            entity.ToTable("Productos");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.CategoriaId).IsRequired().HasColumnType("INT");
            entity.Property(p => p.Nombre).IsRequired().HasMaxLength(100);
            entity.Property(p => p.ImagenUrl).HasMaxLength(255); // Cambiado a ImagenUrl
            entity.Property(p => p.Stock).IsRequired().HasDefaultValue(0);
            entity.Property(p => p.Precio).HasColumnType("DECIMAL(10,2)").IsRequired();
            entity.Property(p => p.TicketsPorUnidad).IsRequired().HasDefaultValue(0);

            entity.HasOne(p => p.Categoria)
                  .WithMany(c => c.Productos) // Ahora funciona porque Productos existe
                  .HasForeignKey(p => p.CategoriaId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configuración de Categoria
        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.ToTable("Categorias");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Nombre).IsRequired().HasMaxLength(100); // Ajustado a 100
            entity.Property(c => c.ImagenUrl).HasMaxLength(255); // Cambiado a ImagenUrl
            entity.Property(c => c.Estado)
                  .IsRequired()
                  .HasDefaultValue(EstadoCategoria.Activo)
                  .HasConversion<string>();
        });

        // Configuración de SolicitudCancion (sin cambios)
        modelBuilder.Entity<SolicitudCancion>(entity =>
        {
            entity.ToTable("SolicitudCanciones");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.MesaId).IsRequired().HasColumnType("INT");
            entity.Property(s => s.Titulo).IsRequired().HasMaxLength(100);
            entity.Property(s => s.Artista).IsRequired().HasMaxLength(100);
            entity.Property(s => s.RegistradoEn).HasDefaultValueSql("GETDATE()");
            entity.Property(s => s.Tipo).IsRequired().HasConversion<string>();
            entity.Property(s => s.Estado).IsRequired().HasDefaultValue(EstadoSolicitud.Pendiente).HasConversion<string>();

            entity.HasOne(s => s.Mesa)
                  .WithMany(m => m.SolicitudesCanciones)
                  .HasForeignKey(s => s.MesaId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // Configuración de Usuario (sin cambios)
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("Usuarios");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.NombreUsuario).IsRequired().HasMaxLength(50).IsUnicode(false);
            entity.Property(u => u.ContraseñaHash).IsRequired().HasMaxLength(255);
            entity.Property(u => u.Rol).IsRequired().HasMaxLength(20).HasConversion<string>();
            entity.Property(u => u.FechaRegistro).HasDefaultValueSql("GETDATE()");
        });
    }
}