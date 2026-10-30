namespace Inventory.Core.Domain.Entities;

public class StockItem
{
    public string ProductId { get; set; }=null!;
    public int QuantityOnHand { get; set; }
    public int ReservedQuantity { get; set; }
    public int Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Computed, not mapped
    public int AvailableQuantity => QuantityOnHand - ReservedQuantity;

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public virtual ICollection<StockReservation> Reservations { get; set; } = new List<StockReservation>();
}

