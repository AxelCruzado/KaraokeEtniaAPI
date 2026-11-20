using KaraokeEtniaApi.Application.DTOs;
using KaraokeEtniaApi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using KaraokeEtniaApi.API.Hubs;
using Microsoft.EntityFrameworkCore;
namespace KaraokeEtniaApi.Administracion;
[ApiController,Route("api/carta"),Authorize(Roles="Administrador,Cajero,Mesera"),TypeFilter(typeof(CajaExceptionFilter))]
public class CartaController(CartaService service,AppDbContext db,IHubContext<PedidoHub> hub):ControllerBase {
 [HttpGet("version")]public object Version()=>new{version=1,componentes=true,recetasMl=true,hielo=true};
 [HttpGet("catalogo")]public async Task<object> Catalogo(int? categoriaId=null)=>await service.Catalogo(false,categoriaId);
 [HttpGet("inventario"),Authorize(Roles="Administrador,Cajero")]public async Task<object> Inventario()=>await service.Catalogo(true,null);
 [HttpPost("configurar"),Authorize(Roles="Administrador")]public async Task<object> Configurar(ConfigurarCarta input)=>await service.Configurar(input,User.Identity!.Name!);
 [HttpPost("mesas/{id}/cotizar")]public async Task<object> Cotizar(int id,PedidoRequestDto input)=>await service.Cotizar(id,input);
 [HttpPost("abrir-botella"),Authorize(Roles="Administrador,Cajero")]public async Task<object> Abrir(AperturaBotella input)=>await service.AbrirBotella(input,User.Identity!.Name!);
 [HttpPost("demostracion"),Authorize(Roles="Administrador")]public async Task<object> Demostracion(CargaCartaDemo input)=>await service.Demostracion(input,User.Identity!.Name!);
 [HttpGet("registros"),Authorize(Roles="Administrador,Cajero")]public async Task<object> Registros(int? mesaId=null)=>await db.CartaRegistros.AsNoTracking().Where(x=>mesaId==null||x.MesaId==mesaId).OrderByDescending(x=>x.FechaUtc).Take(200).ToListAsync();
}
