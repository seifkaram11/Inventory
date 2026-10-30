using Inventory.Core.Domain.Enums;

namespace Inventory.Core.Application.DTOs;

public sealed record ReservationResult(
    bool IsSuccess,
    ReservationFailureReason FailureReason,
    string? ErrorMessage = null)
{
    public static ReservationResult Success() =>
        new(true, ReservationFailureReason.None);

    public static ReservationResult Failure(
        ReservationFailureReason reason,
        string? message = null) =>
        new(false, reason, message);
}
