namespace Inventory.Core.Domain.Enums;

public enum OperationFailureReason
{
    None,
    NotFound,
    InvalidState,
    AlreadyCompleted,
    InvalidOperation,
    ConcurrentModification,
    NotCached
}
