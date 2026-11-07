using Infrastructure.Contracts;
using Inventory.Core.Domain.Enums;
using Inventory.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Consumers;

public class PaymentFailedConsumer : IConsumer<PaymentFailed>
{
    InventoryDBContext _dBContext;

    public PaymentFailedConsumer(InventoryDBContext dBContext)
    {
        _dBContext = dBContext;
    }

    //PaymentFailed(Guid OrderId, decimal totalPrice)
    public async Task Consume(ConsumeContext<PaymentFailed> context)
    {
        try
        {
            var message=context.Message;
            using var trx=await _dBContext.Database.BeginTransactionAsync();
            await _dBContext.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM orders WHERE order_id={message.OrderId} FOR UPDATE");
            var reservationItems=_dBContext.stockReservations.ToList();

            foreach (var item in reservationItems)
            {
                await _dBContext.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM stock_reservations WHERE reservation_id={item.ReservationId} FOR UPDATE");
                await _dBContext.stockReservations.Where(_=>_.ReservationId==item.ReservationId).ExecuteUpdateAsync(_=>_
                .SetProperty(_=>_.Status, ReservationStatus.Released)
                .SetProperty(_=>_.ReleasedAt, DateTimeOffset.UtcNow)
                .SetProperty(_=>_.UpdatedAt, DateTimeOffset.UtcNow));

                await _dBContext.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM stock_items WHERE product_id={item.ProductId}");
                await _dBContext.stockItems.Where(_=>_.ProductId==item.ProductId).ExecuteUpdateAsync( _=>_
                .SetProperty(_=>_.QuantityOnHand, _=>_.QuantityOnHand+item.Quantity)
                .SetProperty(_=>_.ReservedQuantity, _=>_.ReservedQuantity-item.Quantity)
                .SetProperty(_=>_.UpdatedAt, DateTimeOffset.UtcNow)
                .SetProperty(_=>_.Version, _=>_.Version+1));
            }

            await _dBContext.orders.Where(_=>_.OrderId==message.OrderId).ExecuteUpdateAsync(_=>_
            .SetProperty(_=>_.Status, OrderStatus.Failed)
            .SetProperty(_=>_.UpdatedAt, DateTimeOffset.UtcNow));
            await trx.CommitAsync();
        }
        catch
        {
            return;
        }
    }
}
