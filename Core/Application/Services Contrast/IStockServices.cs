using Inventory.Core.Application.DTOs;
using Inventory.Core.Domain.Entities;

namespace Inventory.Core.Application.ServicesContrast;


public interface IStockServices
{
    Task<IEnumerable<StockItem>> GetStockItemsAsync();
    Task<Order?> GetOrderAsync(Guid orderId, CancellationToken ct = default);
    Task<IEnumerable<OrderItem>> GetOrderItemsAsync(Guid orderId, CancellationToken ct = default);
    Task<StockReservation?> GetReservationAsync(Guid reservationId, CancellationToken ct = default);
    Task<IEnumerable<StockReservation>> GetOrderReservationsAsync(Guid orderId, CancellationToken ct = default);
    Task<bool> ReserveOrderAsync(Guid orderId, CancellationToken ct = default);
    Task<OperationResult> ReleaseReservationAsync(Guid reservationId, CancellationToken ct = default);
    Task<OperationResult> FulfillReservationAsync(Guid reservationId, CancellationToken ct = default);
    Task<OperationResult> CancelReservationAsync(Guid reservationId, CancellationToken ct = default);
}
