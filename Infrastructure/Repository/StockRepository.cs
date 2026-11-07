using System.Linq.Expressions;
using Infrastructure.Contracts;
using Inventory.Core.Application.RepositoryContrast;
using Inventory.Core.Domain.Entities;
using Inventory.Core.Domain.Enums;
using Inventory.Core.Domain.Exceptions;
using Inventory.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Repository;

public class StockRepository : IStockRepository
{
    readonly InventoryDBContext _inventoryDBContext;
    readonly IPublishEndpoint _publish;

    public StockRepository(InventoryDBContext inventoryDBContext, IPublishEndpoint publish)
    {
        _inventoryDBContext = inventoryDBContext;
        _publish = publish;
    }

    public async Task<Order?> GetOrderAsync(Guid orderId, CancellationToken ct = default)=>
        await _inventoryDBContext.orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == orderId);


    public async Task<IEnumerable<OrderItem>> GetOrderItemsAsync(Guid orderId, CancellationToken ct = default)=>
        await _inventoryDBContext.orderItems
            .AsNoTracking()
            .Where(i => i.OrderId == orderId)
            .ToListAsync();

    public async Task<IEnumerable<StockReservation>> GetReservationAsync(Expression<Func<StockReservation, bool>> ex, CancellationToken ct = default)=>
        await _inventoryDBContext.stockReservations
            .AsNoTracking()
            .Where(ex)
            .OrderBy(r => r.ReservedAt)
            .ToListAsync();

    public async Task<StockReservation?> GetReservationAsync(Guid reservationId, CancellationToken ct = default)=>
        await _inventoryDBContext.stockReservations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReservationId == reservationId);

    public async Task<bool> ReleaseReservationAsync(StockReservation reservation, CancellationToken ct = default)
    {
        try
        {
            var reservationId = reservation.ReservationId;

            if (reservation.Status == ReservationStatus.Released) return true;

            if (reservation.Status != ReservationStatus.Reserved)
                throw new InvalidReservationStateException(reservationId, reservation.Status);

            await using var trx = await _inventoryDBContext.Database.BeginTransactionAsync();
            var now = DateTimeOffset.UtcNow;

            await _inventoryDBContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM orders WHERE order_id = {reservation.OrderId} FOR UPDATE");

            var numOfRow = await _inventoryDBContext.stockReservations
                .Where(r => r.ReservationId == reservationId
                        && r.Status == ReservationStatus.Reserved)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, ReservationStatus.Released)
                    .SetProperty(r => r.ReleasedAt, now)
                    .SetProperty(r => r.UpdatedAt, now));

            if (numOfRow == 0)
                throw new ConcurrentModificationException(reservationId);

            numOfRow = await _inventoryDBContext.stockItems
                .Where(s => s.ProductId == reservation.ProductId
                        && s.ReservedQuantity >= reservation.Quantity)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.ReservedQuantity, x => x.ReservedQuantity - reservation.Quantity)
                    .SetProperty(x => x.Version, x => x.Version + 1)
                    .SetProperty(x => x.UpdatedAt, now));

            if (numOfRow == 0)
                throw new InvalidOperationException(
                    $"Stock for product '{reservation.ProductId}' is missing or inconsistent.");

            var hasLiveReservations = await _inventoryDBContext.stockReservations
                .AsNoTracking()
                .AnyAsync(r => r.OrderId == reservation.OrderId
                            && (r.Status == ReservationStatus.Reserved
                            || r.Status == ReservationStatus.Fulfilled));

            if (!hasLiveReservations)
            {
                await _inventoryDBContext.orders
                    .Where(o => o.OrderId == reservation.OrderId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(o => o.Status, OrderStatus.Failed)
                        .SetProperty(o => o.UpdatedAt, now));
            }

            await trx.CommitAsync();
            await _publish.Publish(new StockReservationFailed(reservation.OrderId, ""));
            await _inventoryDBContext.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ReserveStockAsync(Guid orderId, string productId, long quantity, CancellationToken ct = default)
    {
        try
        {
            if (quantity <= 0 || quantity > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(quantity));

            var qty = (int)quantity;

            await using var trx = await _inventoryDBContext.Database.BeginTransactionAsync();
            var now = DateTimeOffset.UtcNow;

            await _inventoryDBContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM orders WHERE order_id = {orderId} FOR UPDATE", ct);

            var orderStatus = await _inventoryDBContext.orders
                .AsNoTracking()
                .Where(o => o.OrderId == orderId)
                .Select(o => (OrderStatus?)o.Status)
                .FirstOrDefaultAsync(ct);

            if (orderStatus is null)
                throw new OrderNotFoundException(orderId);

            if (orderStatus != OrderStatus.Pending && orderStatus != OrderStatus.Reserved)
                return false;

            var existing = await _inventoryDBContext.stockReservations
                .AsNoTracking()
                .Where(r => r.OrderId == orderId && r.ProductId == productId)
                .Select(r => (ReservationStatus?)r.Status)
                .FirstOrDefaultAsync(ct);

            if (existing is not null)
                return existing == ReservationStatus.Reserved;

            var itemExists = await _inventoryDBContext.orderItems
                .AsNoTracking()
                .AnyAsync(i => i.OrderId == orderId && i.ProductId == productId, ct);

            if (!itemExists)
                throw new OrderItemNotFoundException(orderId, productId);

            var numOfRow = await _inventoryDBContext.stockItems
                .Where(s => s.ProductId == productId
                        && s.QuantityOnHand - s.ReservedQuantity >= qty)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.ReservedQuantity, x => x.ReservedQuantity + qty)
                    .SetProperty(x => x.Version, x => x.Version + 1)
                    .SetProperty(x => x.UpdatedAt, now), ct);

            if (numOfRow == 0) return false;

            _inventoryDBContext.stockReservations.Add(new StockReservation
            {
                ReservationId = Guid.NewGuid(),
                OrderId = orderId,
                ProductId = productId,
                Quantity = qty,
                Status = ReservationStatus.Reserved,
                ReservedAt = now,
                UpdatedAt = now
            });

            await _inventoryDBContext.SaveChangesAsync(ct);

            var itemsCount = await _inventoryDBContext.orderItems
                .AsNoTracking()
                .CountAsync(i => i.OrderId == orderId, ct);

            var reservedCount = await _inventoryDBContext.stockReservations
                .AsNoTracking()
                .CountAsync(r => r.OrderId == orderId
                            && r.Status == ReservationStatus.Reserved, ct);

            if (itemsCount == reservedCount)
            {
                await _inventoryDBContext.orders
                    .Where(o => o.OrderId == orderId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(o => o.Status, OrderStatus.Reserved)
                        .SetProperty(o => o.UpdatedAt, now), ct);
            }

            await trx.CommitAsync();
            return true;
        }
        catch(Exception ex)
        {
            await _publish.Publish(new StockReservationFailed(orderId, ex.Message));
            await _inventoryDBContext.SaveChangesAsync();
            return false;
        }
    }

    public async Task CancelReservationAsync(StockReservation reservation, CancellationToken ct = default)
    {
        var reservationId = reservation!.ReservationId;

        if (reservation.Status == ReservationStatus.Cancelled) return;

        if (reservation.Status != ReservationStatus.Reserved)
            throw new InvalidReservationStateException(reservationId, reservation.Status);

        await using var trx = await _inventoryDBContext.Database.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;

        await _inventoryDBContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM orders WHERE order_id = {reservation.OrderId} FOR UPDATE", ct);

        var numOfRow = await _inventoryDBContext.stockReservations
            .Where(r => r.ReservationId == reservationId
                     && r.Status == ReservationStatus.Reserved)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, ReservationStatus.Cancelled)
                .SetProperty(r => r.UpdatedAt, now), ct);

        if (numOfRow == 0)
            throw new ConcurrentModificationException(reservationId);

        numOfRow = await _inventoryDBContext.stockItems
            .Where(s => s.ProductId == reservation.ProductId
                     && s.ReservedQuantity >= reservation.Quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.ReservedQuantity, x => x.ReservedQuantity - reservation.Quantity)
                .SetProperty(x => x.Version, x => x.Version + 1)
                .SetProperty(x => x.UpdatedAt, now), ct);

        if (numOfRow == 0)
            throw new InvalidOperationException(
                $"Stock for product '{reservation.ProductId}' is missing or inconsistent.");

        var hasLiveReservations = await _inventoryDBContext.stockReservations
            .AsNoTracking()
            .AnyAsync(r => r.OrderId == reservation.OrderId
                        && (r.Status == ReservationStatus.Reserved
                         || r.Status == ReservationStatus.Fulfilled), ct);

        if (!hasLiveReservations)
        {
            await _inventoryDBContext.orders
                .Where(o => o.OrderId == reservation.OrderId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, OrderStatus.Cancelled)
                    .SetProperty(o => o.UpdatedAt, now), ct);
        }
        await _publish.Publish(new StockReservationFailed(reservation.OrderId, ""));
        _inventoryDBContext.SaveChanges();
        await trx.CommitAsync(ct);
    }

    public async Task<bool> FulfillReservationAsync(Guid reservationId, CancellationToken ct = default)
    {
        var reservation = await _inventoryDBContext.stockReservations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReservationId == reservationId, ct)
            ?? throw new StockReservationNotFoundException(reservationId);

        if (reservation.Status == ReservationStatus.Fulfilled) return true;

        if (reservation.Status != ReservationStatus.Reserved)
            throw new InvalidReservationStateException(reservationId, reservation.Status);

        await using var trx = await _inventoryDBContext.Database.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;

        await _inventoryDBContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM orders WHERE order_id = {reservation.OrderId} FOR UPDATE", ct);

        var numOfRow = await _inventoryDBContext.stockReservations
            .Where(r => r.ReservationId == reservationId
                     && r.Status == ReservationStatus.Reserved)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, ReservationStatus.Fulfilled)
                .SetProperty(r => r.UpdatedAt, now)
                .SetProperty(r => r.FulfilledAt, now), ct);

        if (numOfRow == 0)
            throw new ConcurrentModificationException(reservationId);

        numOfRow = await _inventoryDBContext.stockItems
            .Where(s => s.ProductId == reservation.ProductId
                     && s.QuantityOnHand >= reservation.Quantity
                     && s.ReservedQuantity >= reservation.Quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.QuantityOnHand, x => x.QuantityOnHand - reservation.Quantity)
                .SetProperty(x => x.ReservedQuantity, x => x.ReservedQuantity - reservation.Quantity)
                .SetProperty(x => x.Version, x => x.Version + 1)
                .SetProperty(x => x.UpdatedAt, now), ct);

        if (numOfRow == 0)
            throw new InvalidOperationException(
                $"Stock for product '{reservation.ProductId}' is missing or inconsistent.");

        var hasPending = await _inventoryDBContext.stockReservations
            .AsNoTracking()
            .AnyAsync(r => r.OrderId == reservation.OrderId
                        && r.Status == ReservationStatus.Reserved, ct);

        if (!hasPending)
        {
            await _inventoryDBContext.orders
                .Where(o => o.OrderId == reservation.OrderId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, OrderStatus.Fulfilled)
                    .SetProperty(o => o.UpdatedAt, now), ct);
        }

        await trx.CommitAsync(ct);
        return true;
    }

    public async Task<IEnumerable<StockItem>> GetStockItemsAsync(string productId, CancellationToken ct = default)=>
         await _inventoryDBContext.stockItems.AsNoTracking().Where(_ => _.ProductId == productId).ToListAsync(ct);

    public async Task<bool> UpdateOrderStatusAsync(Guid orderId, OrderStatus status, CancellationToken ct=default)
    {
        var numOfRow = await _inventoryDBContext.orders
            .Where(o => o.OrderId == orderId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, status)
                .SetProperty(o => o.UpdatedAt, DateTimeOffset.UtcNow), ct);

        return numOfRow > 0;
    }

    public async Task<bool> SaveChanges(CancellationToken ct = default)=>
         await _inventoryDBContext.SaveChangesAsync(ct) > 0;

    public async Task<IEnumerable<StockItem>> GetAllStockItemsAsync(CancellationToken ct=default)=>
        await _inventoryDBContext.stockItems.ToListAsync(ct);

    public async Task<IEnumerable<T>> Filtiring<T>(Expression<Func<T, bool>> expression, CancellationToken? ct, int? pageNum, int? pageSize)where T:class
    {
        int PageNum=pageNum??1; int PageSize=pageSize??10;
        return _inventoryDBContext.Set<T>().Where(expression).Skip((PageNum-1)*PageSize).Take(PageSize);
    }
    public async Task AddNewOrder(Order order)=>
        _inventoryDBContext.orders.Add(order);

    public async Task AddOrderItem(OrderItem orderItem)=>
        _inventoryDBContext.orderItems.Add(orderItem);

    public async Task AddNewStock(string productId, int Quantity)=>
        _inventoryDBContext.stockItems.Add(new StockItem{ProductId= productId, QuantityOnHand= Quantity, Version=0});
}
