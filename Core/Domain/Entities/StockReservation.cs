using Inventory.Core.Domain.Enums;

namespace Inventory.Core.Domain.Entities;

public class StockReservation
{
    public Guid ReservationId { get; set; }
    public Guid OrderId { get; set; }
    public string ProductId { get; set; }=null!;
    public int Quantity { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Reserved;
    public DateTimeOffset ReservedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // when the order is not complete the operation in the inventory or the payment
    public DateTimeOffset? ReleasedAt { get; set; }

    public DateTimeOffset? FulfilledAt { get; set; }

    public virtual OrderItem OrderItem { get; set; } = null!;
    public virtual StockItem StockItem { get; set; } = null!;
}
