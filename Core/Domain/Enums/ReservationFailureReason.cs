namespace Inventory.Core.Domain.Enums;

public enum ReservationFailureReason
{
    None,
    OrderNotFound,
    InsufficientStock,
    InvalidOrderStatus,
    AlreadyProcessed,
    InvalidOperation
}
