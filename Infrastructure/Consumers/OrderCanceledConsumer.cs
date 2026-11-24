using Inventory.Core.Domain.Enums;
using Inventory.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Contracts;

public class OrderCanceledConsumer : IConsumer<OrderCanceled>
{
    InventoryDBContext _dBContext;

    public OrderCanceledConsumer(InventoryDBContext dBContext)
    {
        _dBContext = dBContext;
    }

    public async Task Consume(ConsumeContext<OrderCanceled> context)
    {
        var message = context.Message;
        var trx=await _dBContext.Database.BeginTransactionAsync();
        await _dBContext.Database.ExecuteSqlAsync($"SELECT 1 FROM orders WHERE order_id={message.orderId} FOR UPDATE");
        await _dBContext.orders.ExecuteUpdateAsync(_=>
        _.SetProperty(_=>_.Status, OrderStatus.Cancelled)
        .SetProperty(_=>_.UpdatedAt, DateTimeOffset.UtcNow));

        var items=await _dBContext.orderItems.Where(_=>_.OrderId==message.orderId).Select(_=>new {_.ProductId, _.Quantity}).ToListAsync();
        var productIds=items.Select(_=>_.ProductId).ToList();

        await _dBContext.stockReservations.Where(_=>productIds.Contains(_.ProductId) && _.OrderId==message.orderId).ExecuteUpdateAsync(_=>_.SetProperty(_=>_
        .Status, ReservationStatus.Cancelled)
        .SetProperty(_=>_.UpdatedAt, DateTimeOffset.UtcNow));

        foreach(var item in items)
        {
            await _dBContext.stockItems.Where(_=>item.ProductId==_.ProductId)
            .ExecuteUpdateAsync(_=>_
            .SetProperty(_=>_.QuantityOnHand, _=>_.QuantityOnHand+item.Quantity)
            .SetProperty(_=>_.ReservedQuantity, _=>_.ReservedQuantity-item.Quantity)
            .SetProperty(_=>_.UpdatedAt, DateTimeOffset.UtcNow));
        }
        await trx.CommitAsync();
    }
}
