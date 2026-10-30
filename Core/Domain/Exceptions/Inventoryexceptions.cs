using Inventory.Core.Domain.Enums;

namespace Inventory.Core.Domain.Exceptions;


public abstract class NotFoundException(string message) : Exception(message);

public class StockReservationNotFoundException(Guid reservationId)
    : NotFoundException($"Stock reservation '{reservationId}' was not found.");

public class OrderNotFoundException(Guid orderId)
    : NotFoundException($"Order '{orderId}' was not found.");

public class OrderItemNotFoundException(Guid orderId, string productId)
    : NotFoundException($"Order '{orderId}' has no item for product '{productId}'.");

public class InvalidReservationStateException(Guid reservationId, ReservationStatus status)
    : Exception($"Reservation '{reservationId}' is in state '{status}' and cannot be changed this way.");

public class ConcurrentModificationException(Guid id)
    : Exception($"'{id}' was modified by another operation. Retry.");
