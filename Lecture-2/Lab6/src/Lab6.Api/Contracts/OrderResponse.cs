namespace Lab6.Api.Contracts;

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
