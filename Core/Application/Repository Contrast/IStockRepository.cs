using Inventory.Core.Domain.Entities;
using Inventory.Core.Domain.Enums;

namespace Inventory.Core.Application.RepositoryContrast;


public interface IStockRepository
{
    Task CancelReservationAsync(Guid reservationId, CancellationToken ct = default);
    Task<bool> FulfillReservationAsync(Guid reservationId, CancellationToken ct = default);
    Task<Order?> GetOrderAsync(Guid orderId);
    Task<IEnumerable<OrderItem>> GetOrderItemsAsync(Guid orderId);
    Task<IEnumerable<StockReservation>> GetOrderReservationsAsync(Guid orderId);
    Task<StockReservation?> GetReservationAsync(Guid reservationId);
    Task<IEnumerable<StockReservation>> GetReservationsAsync(Guid inventoryId);
    Task<IEnumerable<StockItem>> GetStockItemsAsync(Guid inventoryId);
    Task<bool> ReleaseReservationAsync(Guid reservationId);
    Task<bool> ReserveStockAsync(Guid orderId, Guid inventoryId, Guid productId, long quantity);
    Task<bool> SaveChanges();
    Task<bool> UpdateOrderStatusAsync(Guid orderId, OrderStatus status, CancellationToken ct);
}
