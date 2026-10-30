namespace Inventory.Core.Application.DTOs;

public class ReserveStocRequest
{
    public Guid orderId { get; set; }
    public Guid productId { get; set; }
    public long quantity { get; set; }
}
