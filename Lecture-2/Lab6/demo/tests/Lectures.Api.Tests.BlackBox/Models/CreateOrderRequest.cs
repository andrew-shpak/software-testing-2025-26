namespace Lectures.Api.Tests.BlackBox.Models;

public record CreateOrderRequest(
    string CustomerName,
    string CustomerEmail,
    string ProductName,
    int Quantity,
    decimal UnitPrice);
