using Infrastructure.Contracts;
using Inventory.Core.Application.RepositoryContrast;
using MassTransit;

namespace Infrastructure.Consumers;

class AddNewItemToOrderConsumer : IConsumer<AddNewItemToOrder>
{
    IStockRepository _stockRepository;

    public AddNewItemToOrderConsumer(IStockRepository stockRepository)
    {
        _stockRepository = stockRepository;
    }

    public async Task Consume(ConsumeContext<AddNewItemToOrder> context)
    {
        var message=context.Message;
        string productId = $"{message.ProductId}#{message.ProductVarID}";
        var result=await _stockRepository.ReceivesNewItemForExistOrder(message.OrderId,productId,message.Quantity);

        if (!result.IsSuccess)
            await context.Publish(new StockReservationFailed(message.OrderId, result.FailureReason.ToString()));
    }
}
