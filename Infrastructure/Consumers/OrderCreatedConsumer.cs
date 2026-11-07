using Infrastructure.Contracts;
using Inventory.Core.Domain.Entities;
using Inventory.Core.Domain.Enums;
using Inventory.Infrastructure.Data;
using MassTransit;

namespace Infrastructure.Consumers;

public class OrderCreatedConsumer : IConsumer<OrderCretated>
{
    IPublishEndpoint publish;
    InventoryDBContext _dBContext;

    public OrderCreatedConsumer(IPublishEndpoint publish, InventoryDBContext dBContext)
    {
        this.publish = publish;
        _dBContext = dBContext;
    }

    public async Task Consume(ConsumeContext<OrderCretated> context)
    {
        try
        {
            var message=context.Message;
            string productId=message.ProductId.ToString()+"#"+message.ProductVarID.ToString();
            OrderItem orderItem = new()
            {
                OrderId=message.OrderId,
                ProductId=productId,
                Quantity=message.Quantity
            };
            Order order = new()
            {
                OrderId=message.OrderId,
                ReceivedAt=DateTimeOffset.UtcNow,
                Status=OrderStatus.Pending
            };
            StockReservation stockReservation = new()
            {
                ReservationId=Guid.NewGuid(),
                OrderId=message.OrderId,
                ProductId=productId,
                Quantity=message.Quantity,
                Status=ReservationStatus.Pending,
                ReservedAt=DateTimeOffset.UtcNow
            };

            using var trx=await _dBContext.Database.BeginTransactionAsync();
            await _dBContext.orders.AddAsync(order);
            await _dBContext.orderItems.AddAsync(orderItem);
            await _dBContext.stockReservations.AddAsync(stockReservation);
            await publish.Publish(new StockReservationCompleted(message.OrderId, message.userId,message.totalPrice));
            await _dBContext.SaveChangesAsync();
            await trx.CommitAsync();
        }
        catch (Exception)
        {
            return;
        }
    }
}
