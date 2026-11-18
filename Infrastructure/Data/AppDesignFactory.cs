using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace KaraokeEtniaApi.Infrastructure.Data;
public class AppDesignFactory:IDesignTimeDbContextFactory<AppDbContext>{public AppDbContext CreateDbContext(string[] args){var c=Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")??throw new InvalidOperationException("Configure conexión.");return new(new DbContextOptionsBuilder<AppDbContext>().UseMySql(c,new MySqlServerVersion(new Version(8,0,0))).Options);}}
