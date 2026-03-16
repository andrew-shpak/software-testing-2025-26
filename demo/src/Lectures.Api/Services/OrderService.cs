using Lectures.Api.Data;
using Lectures.Api.Orders.Mappers;
using Lectures.Api.Orders.Requests;
using Lectures.Api.Orders.Responses;
using Lectures.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Lectures.Api.Services;

public interface IOrderService
{
    Task<OrderResponse> GetByIdAsync(int id);
    Task<IReadOnlyList<OrderResponse>> GetAllAsync();
    Task<OrderResponse> CreateAsync(CreateOrderRequest request);
    Task<OrderResponse> UpdateStatusAsync(int id, UpdateOrderStatusRequest request);
    Task<bool> DeleteAsync(int id);
}

public class OrderService(AppDbContext db, TimeProvider timeProvider) : IOrderService
{
    private static readonly OrderMapper Mapper = new();

    public async Task<OrderResponse> GetByIdAsync(int id)
    {
        var order = await db.Orders.FindAsync(id)
            ?? throw new KeyNotFoundException($"Order {id} not found.");

        return Mapper.FromEntity(order);
    }

    public async Task<IReadOnlyList<OrderResponse>> GetAllAsync()
    {
        var orders = await db.Orders.ToListAsync();
        return orders.Select(Mapper.FromEntity).ToList();
    }

    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request)
    {
        var order = new Order
        {
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            ProductName = request.ProductName,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            Status = OrderStatus.Pending,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return Mapper.FromEntity(order);
    }

    public async Task<OrderResponse> UpdateStatusAsync(int id, UpdateOrderStatusRequest request)
    {
        var order = await db.Orders.FindAsync(id)
            ?? throw new KeyNotFoundException($"Order {id} not found.");

        var newStatus = (OrderStatus)request.Status;

        if (order.Status == OrderStatus.Cancelled)
            throw new InvalidOperationException("Cannot update a cancelled order.");

        if (order.Status == OrderStatus.Shipped && newStatus != OrderStatus.Shipped)
            throw new InvalidOperationException("Shipped orders can only remain shipped.");

        order.Status = newStatus;
        await db.SaveChangesAsync();

        return Mapper.FromEntity(order);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var order = await db.Orders.FindAsync(id);
        if (order is null)
            return false;

        if (order.Status == OrderStatus.Shipped)
            throw new InvalidOperationException("Cannot delete a shipped order.");

        db.Orders.Remove(order);
        await db.SaveChangesAsync();

        return true;
    }
}
