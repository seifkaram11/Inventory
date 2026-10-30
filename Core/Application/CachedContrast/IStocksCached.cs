using Inventory.Core.Domain.Entities;

namespace Inventory.Core.Application.CachedContrast;

public interface IStocksCached
{
    Task<bool> addNewNumOfStocksAsync(string productId, long Quantity);
    Task<Order?> GetOrderAsync(Guid orderId);
    Task<IEnumerable<OrderItem>> GetOrderItemsAsync(Guid orderId);
    Task<IEnumerable<StockReservation>> GetOrderReservationsAsync(Guid orderId);
    Task<StockReservation?> GetReservationAsync(Guid reservationId);
    Task<IEnumerable<StockItem>> GetStockItemsAsync();
    Task<bool> RefreshTheValue(string productId, long Quantity);
    Task<bool> removeNumOfStocksAsync(string productId, long Quantity);
    Task<StockItem?> GetStockItemAsync(string productId);
}
