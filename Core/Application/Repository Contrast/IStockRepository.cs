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
    Task<IEnumerable<StockReservation>> GetReservationAsync(Expression<Func<StockReservation, bool>> ex, CancellationToken ct = default);
    // Task<StockReservation?> GetReservationAsync(Guid reservationId, CancellationToken ct = default);
    Task<bool> ReleaseReservationAsync(StockReservation reservationId, CancellationToken ct = default);
    Task<bool> ReserveStockAsync(Guid orderId, string productId, long quantity, CancellationToken ct = default);
    Task<bool> SaveChanges(CancellationToken ct = default);
    Task<bool> UpdateOrderStatusAsync(Guid orderId, OrderStatus status, CancellationToken ct=default);
    Task<IEnumerable<StockItem>> GetStockItemsAsync(string productId, CancellationToken ct = default);
    Task<IEnumerable<T>> Filtiring<T>(Expression<Func<T, bool>> expression, CancellationToken? ct, int? pageNum, int? pageSize)where T:class;
    Task<IEnumerable<StockItem>> GetAllStockItemsAsync(CancellationToken ct = default);
    Task AddNewOrder(Order order);
    Task AddOrderItem(OrderItem orderItem);
    Task AddNewStock(string productId, int Quantity);
}
