using Lectures.Api.Orders.Mappers;
using Lectures.Api.Orders.Requests;
using Lectures.Api.Orders.Responses;
using Lectures.Api.Domain;

namespace Lectures.Api.Services;

public interface IOrderService
{
    Task<OrderResponse> GetByIdAsync(int id);
    Task<IReadOnlyList<OrderResponse>> GetAllAsync();
    Task<OrderResponse> CreateAsync(CreateOrderRequest request);
    Task<OrderResponse> UpdateStatusAsync(int id, UpdateOrderStatusRequest request);
    Task<bool> DeleteAsync(int id);
}

public class OrderService(TimeProvider timeProvider) : IOrderService
{
    private static readonly OrderMapper Mapper = new();
    private readonly List<Order> _orders = [];
    private int _nextId = 1;

    public Task<OrderResponse> GetByIdAsync(int id)
    {
        var order = _orders.SingleOrDefault(o => o.Id == id)
            ?? throw new KeyNotFoundException($"Order {id} not found.");

        return Task.FromResult(Mapper.FromEntity(order));
    }

    public Task<IReadOnlyList<OrderResponse>> GetAllAsync()
    {
        var result = _orders.Select(Mapper.FromEntity).ToList();
        return Task.FromResult<IReadOnlyList<OrderResponse>>(result);
    }

    public Task<OrderResponse> CreateAsync(CreateOrderRequest request)
    {
        var order = new Order
        {
            Id = _nextId++,
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            ProductName = request.ProductName,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            Status = OrderStatus.Pending,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        _orders.Add(order);
        return Task.FromResult(Mapper.FromEntity(order));
    }

    public Task<OrderResponse> UpdateStatusAsync(int id, UpdateOrderStatusRequest request)
    {
        var order = _orders.SingleOrDefault(o => o.Id == id)
            ?? throw new KeyNotFoundException($"Order {id} not found.");

        var newStatus = (OrderStatus)request.Status;

        if (order.Status == OrderStatus.Cancelled)
            throw new InvalidOperationException("Cannot update a cancelled order.");

        if (order.Status == OrderStatus.Shipped && newStatus != OrderStatus.Shipped)
            throw new InvalidOperationException("Shipped orders can only remain shipped.");

        order.Status = newStatus;
        return Task.FromResult(Mapper.FromEntity(order));
    }

    public Task<bool> DeleteAsync(int id)
    {
        var order = _orders.SingleOrDefault(o => o.Id == id);
        if (order is null)
            return Task.FromResult(false);

        if (order.Status == OrderStatus.Shipped)
            throw new InvalidOperationException("Cannot delete a shipped order.");

        _orders.Remove(order);
        return Task.FromResult(true);
    }
}
