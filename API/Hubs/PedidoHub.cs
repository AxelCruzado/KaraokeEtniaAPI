using Microsoft.AspNetCore.SignalR;

namespace KaraokeEtniaApi.API.Hubs;

public class PedidoHub : Hub
{
    public async Task EnviarNotificacionNuevoPedido(int mesaId, string mensaje)
    {
        await Clients.All.SendAsync("RecibirNotificacion", mesaId, mensaje);
    }

    public async Task EnviarNotificacionGrupo(string groupName, int mesaId, string mensaje)
    {
        await Clients.Group(groupName).SendAsync("RecibirNotificacion", mesaId, mensaje);
    }

    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        Console.WriteLine($"{Context.ConnectionId} se unió al grupo {groupName}");
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        Console.WriteLine($"{Context.ConnectionId} salió del grupo {groupName}");
    }

    public async Task MesaActualizada(int mesaId, string estado)
    {
        await Clients.Others.SendAsync("MesaActualizada", mesaId, estado); // Enviar a todos menos al propio cliente
    }

    public async Task SolicitudActualizada(int mesaId, string mensaje)
    {
        await Clients.Others.SendAsync("SolicitudActualizada", mesaId, mensaje); // Enviar a todos menos al propio cliente
    }
}
