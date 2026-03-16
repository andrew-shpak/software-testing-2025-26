namespace Lectures.Api.Orders.Responses;

public record OrderResponse(
    int Id,
    string CustomerName,
    string CustomerEmail,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    string Status,
    DateTime CreatedAt);
