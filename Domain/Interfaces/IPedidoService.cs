using KaraokeNoeliasApi.Domain.Entities;

namespace KaraokeNoeliasApi.Domain.Interfaces
{
    public interface IPedidoService
    {
        Task RegistrarPedidoAsync(int mesaId, int productoId, int cantidad);
        Task<List<PedidoActivo>> ObtenerPedidosPorMesaAsync(int mesaId);
    }
}
