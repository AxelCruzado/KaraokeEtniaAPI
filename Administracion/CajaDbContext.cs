using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KaraokeEtniaApi.Administracion;

public class TurnoCaja
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Codigo { get; set; } = "";
    public string Caja { get; set; } = "";
    public string? CajaActiva { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime Fin { get; set; }
    public DateTime AperturaRegistradaUtc { get; set; } = DateTime.UtcNow;
    public decimal FondoInicial { get; set; }
    public string Responsable { get; set; } = "";
    public string Estado { get; set; } = "Abierto";
}
public class VentaCaja
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Referencia { get; set; } = "";
    public int? PedidoHistorialId { get; set; }
    public decimal Total { get; set; }
    public string Estado { get; set; } = "Pendiente";
}
public class MovimientoCaja
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Codigo { get; set; } = "";
    public string TurnoId { get; set; } = "";
    public string? VentaId { get; set; }
    public string Tipo { get; set; } = "";
    public string Naturaleza { get; set; } = "";
    public string Concepto { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string Documento { get; set; } = "";
    public decimal Importe { get; set; }
    public DateTime FechaOperacion { get; set; }
    public DateTime RegistradoUtc { get; set; } = DateTime.UtcNow;
    public string Usuario { get; set; } = "";
    public string ClaveOperacion { get; set; } = "";
    public string HuellaSolicitud { get; set; } = "";
    public int Version { get; set; } = 1;
    public bool Anulado { get; set; }
    public List<PagoCaja> Pagos { get; set; } = new();
}
public class PagoCaja
{
    public int Id { get; set; }
    public string MovimientoCajaId { get; set; } = "";
    public string Medio { get; set; } = "";
    public decimal Importe { get; set; }
}
public class CierreCaja
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Codigo { get; set; } = "";
    public string TurnoId { get; set; } = "";
    public int Version { get; set; }
    public DateTime RegistradoUtc { get; set; } = DateTime.UtcNow;
    public string Responsable { get; set; } = "";
    public decimal Contado { get; set; }
    public decimal Esperado { get; set; }
    public decimal Diferencia { get; set; }
    public string SnapshotJson { get; set; } = "";
    public string ClaveOperacion { get; set; } = "";
}
public class AuditoriaCaja
{
    public long Id { get; set; }
    public string TurnoId { get; set; } = "";
    public string EntidadCodigo { get; set; } = "";
    public string Accion { get; set; } = "";
    public string Usuario { get; set; } = "";
    public DateTime RegistradoUtc { get; set; } = DateTime.UtcNow;
    public string Motivo { get; set; } = "";
    public string AnteriorJson { get; set; } = "";
    public string PosteriorJson { get; set; } = "";
}
public class ReporteCaja
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Codigo { get; set; } = "";
    public string TurnoId { get; set; } = "";
    public int Version { get; set; }
    public DateTime GeneradoUtc { get; set; } = DateTime.UtcNow;
    public string Usuario { get; set; } = "";
    public string SnapshotJson { get; set; } = "";
}
public class CajaDbContext : DbContext
{
    public CajaDbContext(DbContextOptions<CajaDbContext> options) : base(options) { }
    protected CajaDbContext(DbContextOptions options) : base(options) { }
    public DbSet<TurnoCaja> Turnos => Set<TurnoCaja>();
    public DbSet<VentaCaja> Ventas => Set<VentaCaja>();
    public DbSet<MovimientoCaja> Movimientos => Set<MovimientoCaja>();
    public DbSet<PagoCaja> Pagos => Set<PagoCaja>();
    public DbSet<CierreCaja> Cierres => Set<CierreCaja>();
    public DbSet<AuditoriaCaja> Auditorias => Set<AuditoriaCaja>();
    public DbSet<ReporteCaja> Reportes => Set<ReporteCaja>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        foreach (var type in new[] { typeof(TurnoCaja), typeof(VentaCaja), typeof(MovimientoCaja), typeof(PagoCaja), typeof(CierreCaja), typeof(AuditoriaCaja), typeof(ReporteCaja) })
        {
            b.Entity(type).ToTable("Caja_" + type.Name);
            foreach (var prop in type.GetProperties())
            {
                if (prop.PropertyType == typeof(string)) b.Entity(type).Property(prop.Name).HasMaxLength(prop.Name.EndsWith("Json") ? 16000 : (prop.Name == "Id" || prop.Name.EndsWith("Id")) ? 32 : prop.Name == "ClaveOperacion" ? 100 : prop.Name == "Codigo" ? 32 : 500);
                if (prop.PropertyType == typeof(decimal)) b.Entity(type).Property(prop.Name).HasPrecision(14, 2);
            }
        }
        b.Entity<TurnoCaja>().HasIndex(x => x.Codigo).IsUnique();
        b.Entity<TurnoCaja>().HasIndex(x => new { x.Caja, x.Inicio }).IsUnique();
        b.Entity<TurnoCaja>().Property(x => x.CajaActiva).HasMaxLength(40);
        b.Entity<TurnoCaja>().HasIndex(x => x.CajaActiva).IsUnique();
        b.Entity<VentaCaja>().HasIndex(x => x.Referencia).IsUnique();
        b.Entity<VentaCaja>().HasIndex(x => x.PedidoHistorialId).IsUnique();
        b.Entity<MovimientoCaja>().HasIndex(x => new { x.TurnoId, x.ClaveOperacion }).IsUnique();
        b.Entity<MovimientoCaja>().HasIndex(x => x.Codigo).IsUnique();
        b.Entity<MovimientoCaja>().HasIndex(x => x.VentaId).IsUnique();
        b.Entity<MovimientoCaja>().HasOne<TurnoCaja>().WithMany().HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MovimientoCaja>().HasOne<VentaCaja>().WithMany().HasForeignKey(x => x.VentaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PagoCaja>().HasOne<MovimientoCaja>().WithMany(x => x.Pagos).HasForeignKey(x => x.MovimientoCajaId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<CierreCaja>().HasIndex(x => new { x.TurnoId, x.Version }).IsUnique();
        b.Entity<CierreCaja>().HasIndex(x => new { x.TurnoId, x.ClaveOperacion }).IsUnique();
        b.Entity<ReporteCaja>().HasIndex(x => new { x.TurnoId, x.Version }).IsUnique();
        foreach (var type in new[] { typeof(CierreCaja), typeof(ReporteCaja), typeof(AuditoriaCaja) })
            b.Entity(type).HasOne(typeof(TurnoCaja)).WithMany().HasForeignKey("TurnoId").OnDelete(DeleteBehavior.Restrict);
        foreach (var entity in b.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(string) && p.Name.EndsWith("Json"))) property.SetColumnType("longtext");
    }
    void ProtectEvidence()
    {
        if (ChangeTracker.Entries().Any(e => (e.Entity is CierreCaja or ReporteCaja or AuditoriaCaja) && (e.State == EntityState.Modified || e.State == EntityState.Deleted)))
            throw new InvalidOperationException("Cierres, reportes y auditoría son de solo adición.");
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess) { ProtectEvidence(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default) { ProtectEvidence(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct); }
}
public class CajaDesignFactory : IDesignTimeDbContextFactory<CajaDbContext>
{
    public CajaDbContext CreateDbContext(string[] args)
    {
        var c = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? throw new InvalidOperationException("Configure la conexión para aplicar migraciones.");
        return new(new DbContextOptionsBuilder<CajaDbContext>().UseMySql(c, new MySqlServerVersion(new Version(8, 0, 0)), x => x.MigrationsHistoryTable("__CajaMigrationsHistory")).Options);
    }
}
