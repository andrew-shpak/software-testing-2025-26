using Lectures.Api.Domain;
using Lectures.Api.Orders.Responses;

namespace Lectures.Api.Orders.Mappers;

public record OrderMapper
{
    public OrderResponse FromEntity(Order order) => new(
        order.Id,
        order.CustomerName,
        order.CustomerEmail,
        order.ProductName,
        order.Quantity,
        order.UnitPrice,
        order.TotalPrice,
        order.Status.ToString(),
        order.CreatedAt);
}
