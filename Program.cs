using KaraokeEtniaApi.Administracion;
using KaraokeEtniaApi.Domain.Entities;
using KaraokeEtniaApi.Domain.Interfaces;
using KaraokeEtniaApi.Infrastructure.Data;
using KaraokeEtniaApi.Infrastructure.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
using KaraokeEtniaApi.API.Hubs;
using KaraokeEtniaApi.Deployment;

var builder = WebApplication.CreateBuilder(args);
DeploymentSetup.Validate(builder.Configuration, builder.Environment);

// Agregar servicios al contenedor
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// Configurar CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        policy.SetIsOriginAllowed(origin => origins.Contains(origin, StringComparer.OrdinalIgnoreCase)
            || (builder.Environment.IsDevelopment() && Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Host == "localhost"))
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Registrar AppDbContext con MySQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 0))));

builder.Services.AddDbContext<CajaDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 0)), x => x.MigrationsHistoryTable("__CajaMigrationsHistory")));
builder.Services.AddScoped<CajaService>();

// Configurar Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Karaoke Etnia API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Por favor ingrese el token JWT con el esquema Bearer (ejemplo: 'Bearer {token}')",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configurar autenticación JWT
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents { OnTokenValidated = async context => { var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>(); var user = await db.Usuarios.SingleOrDefaultAsync(u => u.NombreUsuario == context.Principal!.Identity!.Name); if(user == null || !user.Activo || !context.Principal!.IsInRole(user.Rol)) context.Fail("Usuario desactivado o permisos modificados."); } };
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

// Registrar IPedidoService
builder.Services.AddScoped<IPedidoService, PedidoService>();

// Registrar SignalR
builder.Services.AddSignalR();

// Construir la aplicación
var app = builder.Build();
app.UseForwardedHeaders(new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto,
    // Only the trusted internal proxy can supply the HTTPS scheme. API has no public port.
    KnownProxies = { System.Net.IPAddress.Parse("172.30.60.10") }
});

// Configurar el pipeline de middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
            if (exceptionHandlerPathFeature?.Error != null)
            {
                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
                {
                    StatusCode = 500,
                    Message = "Internal Server Error",
                    Detail = exceptionHandlerPathFeature.Error.Message,
                    StackTrace = exceptionHandlerPathFeature.Error.StackTrace
                }));
            }
        });
    });
}

// Usar CORS al inicio del pipeline
app.UseCors("AllowLocalhost");

app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Mapear controladores y hubs de SignalR
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHub<PedidoHub>("/pedidoHub");
});

await DeploymentSetup.Initialize(app);
app.MapGet("/api/version", () => Results.Ok(new {
    servicio = "KaraokeEtniaAPI",
    revision = System.Reflection.Assembly.GetExecutingAssembly()
        .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion,
    ambiente = app.Configuration["Deployment:Label"] ?? app.Environment.EnvironmentName
})).AllowAnonymous();
app.MapGet("/health/live", () => Results.Ok(new { estado = "activo" })).AllowAnonymous();
app.MapGet("/health/ready", async (AppDbContext db) =>
    await db.Database.CanConnectAsync() ? Results.Ok(new { estado = "disponible" }) : Results.StatusCode(503)).AllowAnonymous();

app.Run();

