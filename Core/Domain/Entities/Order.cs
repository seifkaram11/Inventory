using Inventory.Core.Domain.Enums;

namespace Inventory.Core.Domain.Entities;

public class Order
{
    public Guid OrderId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public virtual ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
