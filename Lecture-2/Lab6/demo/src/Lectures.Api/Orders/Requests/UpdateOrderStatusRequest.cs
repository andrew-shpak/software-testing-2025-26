namespace Lectures.Api.Orders.Requests;

public record UpdateOrderStatusRequest(OrderStatusDto Status);

public enum OrderStatusDto
{
    Pending,
    Confirmed,
    Shipped,
    Cancelled
}
