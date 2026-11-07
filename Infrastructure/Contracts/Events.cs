namespace Infrastructure.Contracts;

public record OrderCretated(Guid OrderId, Guid ProductId, Guid ProductVarID, Guid userId,int Quantity, decimal totalPrice);
public record OrderCanceled(Guid orderId);
public record ItemCanceled(Guid orderId, Guid ProductId, Guid ProductVarID);


public record PaymentFailed(Guid OrderId, decimal totalPrice);//error:the inventory receve this event from payment
public record PaymentCompleted(Guid OrderId);//accept:the inventory receve this event from payment

public record StockReservationFailed(Guid OrderId, string Reason);//error: inventory=>Order
public record StockReservationCompleted(Guid OrderId, Guid userId, decimal totalPrice);//agree: inventory => payment
