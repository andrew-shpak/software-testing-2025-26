namespace Lab6.Api.Contracts;

public record UpdateOrderStatusRequest(OrderStatusDto Status);

public enum OrderStatusDto
{
    Pending,
    Confirmed,
    Shipped,
    Cancelled
}
