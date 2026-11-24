using Infrastructure.Contracts;
using Inventory.Core.Application.DTOs;
using Inventory.Core.Application.RepositoryContrast;
using Inventory.Infrastructure.Data;
using MassTransit;

namespace Infrastructure.Consumers;

public class OrderCreatedConsumer : IConsumer<OrderCretated>
{
    IStockRepository _stockRepository;

    public OrderCreatedConsumer(IStockRepository stockRepository)
    {
        _stockRepository = stockRepository;
    }

    public async Task Consume(ConsumeContext<OrderCretated> context)
    {
        var message = context.Message;

        string productId = $"{message.ProductId}#{message.ProductVarID}";

        var result = await _stockRepository.ProcessOrderProductAsync(
            message.OrderId,
            message.userId,
            productId,
            message.Quantity,
            message.ExpectedItemsCount,
            context.CancellationToken);

        if (!result.IsSuccess)
        {
            await context.Publish(new StockReservationFailed(message.OrderId, result.FailureReason.ToString()));
            return;
        }
        if (result.orderCompleted is not null && (bool)result.orderCompleted!)
        {
            await context.Publish(new StockReservationCompleted(message.OrderId, message.userId, message.totalPrice));
        }
    }
}

