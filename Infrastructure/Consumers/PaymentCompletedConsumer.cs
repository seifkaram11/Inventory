using Infrastructure.Contracts;
using Inventory.Core.Domain.Enums;
using Inventory.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Consumers;

public class PaymentCompletedConsumer : IConsumer<PaymentCompleted>
{
    InventoryDBContext _dBContext;
    public PaymentCompletedConsumer(InventoryDBContext dBContext)
    {
        _dBContext = dBContext;
    }

    public async Task Consume(ConsumeContext<PaymentCompleted> context)
    {
        try
        {
            var message=context.Message;

            using var trx=await _dBContext.Database.BeginTransactionAsync();
            _dBContext.Database.ExecuteSqlInterpolated($"SELECT 1 FROM order WHERE order_id={message.OrderId} FOR UPDATE");

            var stockReservations=await _dBContext.stockReservations.ToListAsync();
            foreach (var item in stockReservations)
            {
                await _dBContext.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM stock_reservations WHERE reservation_id={item.ReservationId} FRO UPDATE");
                await _dBContext.stockReservations.Where(_=>_.ReservationId==item.ReservationId && _.UpdatedAt==item.UpdatedAt).ExecuteUpdateAsync(_=>_
                .SetProperty(_=>_.Status, ReservationStatus.Fulfilled)
                .SetProperty(_=>_.FulfilledAt, DateTimeOffset.UtcNow)
                .SetProperty(_=>_.UpdatedAt,DateTimeOffset.UtcNow));
            }
            await _dBContext.orders.Where(_=>_.OrderId==message.OrderId).ExecuteUpdateAsync(_=>_.SetProperty(_=>_.Status, OrderStatus.Failed));
            await trx.CommitAsync();
        }
        catch
        {
            return;
        }
    }
}
