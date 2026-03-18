namespace Lectures.Api.Tests.BlackBox.Models;

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
