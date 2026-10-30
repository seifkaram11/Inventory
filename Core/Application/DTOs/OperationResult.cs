using Inventory.Core.Domain.Enums;

namespace Inventory.Core.Application.DTOs;

public sealed record OperationResult(
    bool IsSuccess,
    OperationFailureReason FailureReason,
    string? ErrorMessage = null)
{
    public static OperationResult Success() =>
        new(true, OperationFailureReason.None);

    public static OperationResult Failure(
        OperationFailureReason reason,
        string? message = null) =>
        new(false, reason, message);
}
