using System.Linq.Expressions;
using Inventory.Core.Domain.Entities;
using Inventory.Core.Domain.Enums;

namespace Inventory.Core.Application.RepositoryContrast;


public interface IStockRepository
{
    Task CancelReservationAsync(StockReservation reservation, CancellationToken ct = default);
    Task<bool> FulfillReservationAsync(Guid reservationId, CancellationToken ct = default);
    Task<Order?> GetOrderAsync(Guid orderId, CancellationToken ct = default);
    Task<IEnumerable<OrderItem>> GetOrderItemsAsync(Guid orderId, CancellationToken ct = default);
    Task<IEnumerable<StockReservation>> GetOrderReservationsAsync(Guid orderId, CancellationToken ct = default);
    Task<StockReservation?> GetReservationAsync(Guid reservationId, CancellationToken ct = default);
    Task<bool> ReleaseReservationAsync(StockReservation reservationId, CancellationToken ct = default);
    Task<bool> ReserveStockAsync(Guid orderId, string productId, long quantity, CancellationToken ct = default);
    Task<bool> SaveChanges(CancellationToken ct = default);
    Task<bool> UpdateOrderStatusAsync(Guid orderId, OrderStatus status, CancellationToken ct);
    Task<IEnumerable<StockItem>> GetStockItemsAsync(string productId, CancellationToken ct = default);

    Task<IEnumerable<Order>> FiltiringOrder(Expression<Func<Order, bool>> expression, CancellationToken ct, int? pageNum, int? pageSize);
    Task<IEnumerable<OrderItem>> FiltiringOrderItems(Expression<Func<OrderItem, bool>> expression, CancellationToken ct, int? pageNum, int? pageSize);

    Task<IEnumerable<StockItem>> FiltiringStockItems(Expression<Func<StockItem, bool>> expression, CancellationToken ct, int? pageNum, int? pageSize);
    Task<IEnumerable<StockReservation>> FiltiringStockReservations(Expression<Func<StockReservation, bool>> expression, CancellationToken ct, int? pageNum, int? pageSize);
    Task<bool> ReserveOrderAsync(Guid orderId, CancellationToken ct);
    Task<IEnumerable<StockItem>> GetAllStockItemsAsync(CancellationToken ct=default);
}
