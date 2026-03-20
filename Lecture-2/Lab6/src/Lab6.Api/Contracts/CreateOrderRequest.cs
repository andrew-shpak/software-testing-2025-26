namespace Lab6.Api.Contracts;

public record CreateOrderRequest(
    string CustomerName,
    string CustomerEmail,
    string ProductName,
    int Quantity,
    decimal UnitPrice);
