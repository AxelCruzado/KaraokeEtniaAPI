using KaraokeEtniaApi.Infrastructure.Data;
namespace KaraokeEtniaApi.Infrastructure.Postgres;

public class ImageStorage(AppDbContext db,IConfiguration config,IWebHostEnvironment env,IHttpContextAccessor accessor)
{
    public async Task<string> Save(IFormFile image,string folder)
    {
        var extension=Path.GetExtension(image.FileName).ToLowerInvariant();
        if (!new[]{".jpg",".jpeg",".png",".webp"}.Contains(extension) || image.Length<=0 || image.Length>5*1024*1024)
            throw new InvalidOperationException("Imagen inválida o mayor a 5 MB.");
        var file=Guid.NewGuid().ToString("N")+extension;
        var request=accessor.HttpContext!.Request;
        var origin=config["Deployment:PublicUrl"] ?? $"{request.Scheme}://{request.Host}{request.PathBase}";
        if(config["Storage:Backend"]=="Database")
        {
            await using var stream=new MemoryStream();await image.CopyToAsync(stream);
            var contentType=extension is ".jpg" or ".jpeg"?"image/jpeg":extension==".png"?"image/png":"image/webp";
            // Saved in the same unit of work as the category/product; no early SaveChanges.
            db.Set<StoredImage>().Add(new(){Id=file,Folder=folder,ContentType=contentType,Content=stream.ToArray()});
        }
        else
        {
            var directory=Path.Combine(env.WebRootPath,"images",folder);Directory.CreateDirectory(directory);
            await using var stream=File.Create(Path.Combine(directory,file));await image.CopyToAsync(stream);
        }
        return origin.TrimEnd('/')+"/images/"+folder+"/"+file;
    }
}
