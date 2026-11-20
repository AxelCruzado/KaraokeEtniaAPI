using KaraokeEtniaApi.Administracion;
using KaraokeEtniaApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
namespace KaraokeEtniaApi.Infrastructure.Postgres;

public class StoredImage
{
    public string Id { get; set; } = "";
    public string Folder { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Content { get; set; } = [];
}
public static class PostgresModel
{
    public static void Configure(ModelBuilder b)
    {
        foreach (var entity in b.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties())
        {
            if (property.GetColumnType() == "longtext") property.SetColumnType("text");
            if (property.GetColumnType() == "INT") property.SetColumnType("integer");
            if (property.ClrType == typeof(DateTime))
            {
                // Business times keep the existing Lima wall-clock contract; *Utc fields retain UTC JSON semantics.
                property.SetColumnType("timestamp without time zone");
                property.SetValueConverter(property.Name.EndsWith("Utc")
                    ? new ValueConverter<DateTime,DateTime>(v=>DateTime.SpecifyKind(v,DateTimeKind.Unspecified),v=>DateTime.SpecifyKind(v,DateTimeKind.Utc))
                    : new ValueConverter<DateTime,DateTime>(v=>DateTime.SpecifyKind(v,DateTimeKind.Unspecified),v=>DateTime.SpecifyKind(v,DateTimeKind.Unspecified)));
            }
        }
    }
}
public class PostgresAppDbContext(DbContextOptions<PostgresAppDbContext> options,IHttpContextAccessor? actor=null) : AppDbContext(options,actor)
{
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<StoredImage>(e=>{e.ToTable("StoredImages");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasMaxLength(100);e.Property(x=>x.Folder).HasMaxLength(30);e.Property(x=>x.ContentType).HasMaxLength(50);});
        PostgresModel.Configure(b);
    }
}
public class PostgresCajaDbContext(DbContextOptions<PostgresCajaDbContext> options) : CajaDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder b) { base.OnModelCreating(b);PostgresModel.Configure(b); }
}
public class PostgresAppDesignFactory : IDesignTimeDbContextFactory<PostgresAppDbContext>
{
    public PostgresAppDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PostgresAppDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")??"Host=localhost;Database=etnia_design;Username=etnia;Password=not-used",x=>x.MigrationsHistoryTable("__PostgresAppMigrationsHistory")).Options);
}
public class PostgresCajaDesignFactory : IDesignTimeDbContextFactory<PostgresCajaDbContext>
{
    public PostgresCajaDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PostgresCajaDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")??"Host=localhost;Database=etnia_design;Username=etnia;Password=not-used",x=>x.MigrationsHistoryTable("__PostgresCajaMigrationsHistory")).Options);
}
