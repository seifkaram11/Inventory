using Infrastructure.Contracts;
using Inventory.Core.Application.RepositoryContrast;
using MassTransit;

namespace Infrastructure.Consumers;


public class ItemCanceledConsumer : IConsumer<ItemCanceled>
{
    IStockRepository _repository;

    public ItemCanceledConsumer(IStockRepository repository)
    {
        _repository = repository;
    }

    public async Task Consume(ConsumeContext<ItemCanceled> context)//CancelReservationAsync
    {
        var message=context.Message;
        string productId=message.ProductId+"#"+message.ProductVarID;
        var Reservation=(await _repository.GetReservationAsync(_=>_.OrderId==message.orderId&&_.ProductId==productId)).FirstOrDefault();
        if(Reservation is null)return;
        await _repository.CancelReservationAsync(Reservation);
    }
}
