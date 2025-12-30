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

var builder = WebApplication.CreateBuilder(args);

// Agregar servicios al contenedor
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
        policy.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Registrar AppDbContext con MySQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))));

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

// Sembrar el usuario administrador inicial si no existe
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    if (!context.Usuarios.Any())
    {
        context.Usuarios.Add(new Usuario
        {
            NombreUsuario = "admin",
            ContraseñaHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Rol = "Administrador",
            FechaRegistro = DateTime.Now
        });
        context.SaveChanges();
    }
}

app.Run();

