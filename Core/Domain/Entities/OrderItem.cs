namespace Inventory.Core.Domain.Entities;

public class OrderItem
{
    // Composite PK: (OrderId, ProductId)
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }

    public virtual Order Order { get; set; } = null!;
    public virtual StockItem StockItem { get; set; } = null!;
    public virtual StockReservation? Reservation { get; set; }
}
