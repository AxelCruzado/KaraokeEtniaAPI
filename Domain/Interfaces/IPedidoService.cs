using KaraokeEtniaApi.Domain.Entities;

namespace KaraokeEtniaApi.Domain.Interfaces
{
    public interface IPedidoService
    {
        Task RegistrarPedidoAsync(int mesaId, int productoId, int cantidad);
        Task<List<PedidoActivo>> ObtenerPedidosPorMesaAsync(int mesaId);
    }
}

