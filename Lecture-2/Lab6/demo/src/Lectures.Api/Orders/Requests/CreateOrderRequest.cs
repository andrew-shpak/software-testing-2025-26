namespace Lectures.Api.Orders.Requests;

public record CreateOrderRequest(
    string CustomerName,
    string CustomerEmail,
    string ProductName,
    int Quantity,
    decimal UnitPrice);
