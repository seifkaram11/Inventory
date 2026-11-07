using Inventory.Infrastructure.Data;
using MassTransit;

namespace Infrastructure.Contracts;

public class OrderCanceledConsumer : IConsumer<OrderCanceled>
{
    InventoryDBContext _dBContext;

    public OrderCanceledConsumer(InventoryDBContext dBContext)
    {
        _dBContext = dBContext;
    }

    public Task Consume(ConsumeContext<OrderCanceled> context)
    {
        throw new NotImplementedException();
    }
}
