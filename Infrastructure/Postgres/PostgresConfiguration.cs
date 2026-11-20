using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Npgsql;
namespace KaraokeEtniaApi.Infrastructure.Postgres;

public static class PostgresConfiguration
{
    public static void Prepare(IConfiguration config)
    {
        if(config["Deployment:Render"]=="true" && string.IsNullOrWhiteSpace(config["Deployment:PublicUrl"]))
            config["Deployment:PublicUrl"]=Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL");
        if(config["Database:Provider"]?.Equals("PostgreSQL",StringComparison.OrdinalIgnoreCase)!=true)return;
        var raw=config.GetConnectionString("DefaultConnection")??"";
        if(string.IsNullOrWhiteSpace(raw))return;
        NpgsqlConnectionStringBuilder connection;
        if(Uri.TryCreate(raw,UriKind.Absolute,out var uri) && uri.Scheme is "postgres" or "postgresql")
        {
            var credentials=uri.UserInfo.Split(':',2);
            if(credentials.Length!=2)throw new InvalidOperationException("La URI PostgreSQL debe identificar usuario y contraseña.");
            connection=new(){Host=uri.Host,Port=uri.Port>0?uri.Port:5432,Database=Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),Username=Uri.UnescapeDataString(credentials[0]),Password=Uri.UnescapeDataString(credentials[1]),SslMode=SslMode.VerifyFull};
        }
        else connection=new(raw);
        if(!string.IsNullOrWhiteSpace(config["Database:RootCertificate"])) connection.RootCertificate=config["Database:RootCertificate"];
        var pem=config["Database:CaCertificatePem"];
        if(!string.IsNullOrWhiteSpace(pem))
        {
            using var certificate=X509Certificate2.CreateFromPem(pem);
            var path=Path.Combine(Path.GetTempPath(),"etnia-ca-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pem)))[..16]+".crt");
            File.WriteAllText(path,pem);
            connection.RootCertificate=path;
            connection.SslMode=SslMode.VerifyFull;
        }
        connection.MaxPoolSize=config.GetValue<int?>("Database:MaximumPoolSize")??5;
        connection.MinPoolSize=0;
        connection.Timeout=30;
        connection.CommandTimeout=60;
        config["ConnectionStrings:DefaultConnection"]=connection.ConnectionString;
    }
}
