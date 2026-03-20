using AutoFixture;
using Lab6.Api.Contracts;
using Lab6.Api.Data;
using Lab6.Api.Domain;
using Lab6.Api.Services;
using Lab6.Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Lab6.Api.Tests.Unit.Services;

public class OrderServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly FakeTimeProvider _timeProvider;
    private readonly OrderService _sut;
    private readonly Fixture _fixture;

    public OrderServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero));
        _sut = new OrderService(_db, _timeProvider);

        _fixture = new Fixture();
        _fixture.Customize(new OrderCustomization());
    }

    public void Dispose() => _db.Dispose();

    // --- GetAllAsync ---

    [Fact]
    public async Task GetAllAsync_WhenOrdersExist_ReturnsAllOrders()
    {
        // Arrange
        var orders = _fixture.CreateMany<Order>(5).ToList();
        _db.Orders.AddRange(orders);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Count.ShouldBe(5);
    }

    [Fact]
    public async Task GetAllAsync_WhenNoOrders_ReturnsEmptyList()
    {
        var result = await _sut.GetAllAsync();
        result.ShouldBeEmpty();
    }

    // --- GetByIdAsync ---

    [Fact]
    public async Task GetByIdAsync_ExistingOrder_ReturnsOrder()
    {
        var order = _fixture.Create<Order>();
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(order.Id);

        result.CustomerName.ShouldBe(order.CustomerName);
        result.CustomerEmail.ShouldBe(order.CustomerEmail);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ThrowsKeyNotFoundException()
    {
        await Should.ThrowAsync<KeyNotFoundException>(
            () => _sut.GetByIdAsync(999));
    }

    // --- CreateAsync ---

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsCreatedOrder()
    {
        var request = new CreateOrderRequest("John Doe", "john@test.com", "Widget", 5, 19.99m);

        var result = await _sut.CreateAsync(request);

        result.CustomerName.ShouldBe("John Doe");
        result.ProductName.ShouldBe("Widget");
        result.Quantity.ShouldBe(5);
        result.Status.ShouldBe("Pending");
    }

    [Fact]
    public async Task CreateAsync_UsesTimeProvider_ForCreatedAt()
    {
        var request = new CreateOrderRequest("Jane", "jane@test.com", "Gadget", 1, 10.00m);

        var result = await _sut.CreateAsync(request);

        result.CreatedAt.ShouldBe(_timeProvider.GetUtcNow().UtcDateTime);
    }

    // --- UpdateStatusAsync ---

    [Fact]
    public async Task UpdateStatusAsync_ValidTransition_UpdatesStatus()
    {
        var order = _fixture.Create<Order>();
        order.Status = OrderStatus.Pending;
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var result = await _sut.UpdateStatusAsync(
            order.Id,
            new UpdateOrderStatusRequest(OrderStatusDto.Confirmed));

        result.Status.ShouldBe("Confirmed");
    }

    [Fact]
    public async Task UpdateStatusAsync_CancelledOrder_ThrowsInvalidOperationException()
    {
        var order = _fixture.Create<Order>();
        order.Status = OrderStatus.Cancelled;
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.UpdateStatusAsync(order.Id, new UpdateOrderStatusRequest(OrderStatusDto.Confirmed)));
    }

    [Fact]
    public async Task UpdateStatusAsync_ShippedOrder_ThrowsInvalidOperationException()
    {
        var order = _fixture.Create<Order>();
        order.Status = OrderStatus.Shipped;
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.UpdateStatusAsync(order.Id, new UpdateOrderStatusRequest(OrderStatusDto.Pending)));
    }

    [Fact]
    public async Task UpdateStatusAsync_NonExistentId_ThrowsKeyNotFoundException()
    {
        await Should.ThrowAsync<KeyNotFoundException>(
            () => _sut.UpdateStatusAsync(999, new UpdateOrderStatusRequest(OrderStatusDto.Confirmed)));
    }

    // --- DeleteAsync ---

    [Fact]
    public async Task DeleteAsync_PendingOrder_ReturnsTrue()
    {
        var order = _fixture.Create<Order>();
        order.Status = OrderStatus.Pending;
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var result = await _sut.DeleteAsync(order.Id);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_ReturnsFalse()
    {
        var result = await _sut.DeleteAsync(999);
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ShippedOrder_ThrowsInvalidOperationException()
    {
        var order = _fixture.Create<Order>();
        order.Status = OrderStatus.Shipped;
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.DeleteAsync(order.Id));
    }

    // --- Bulk insert with AutoFixture: 100_000 orders ---

    [Fact]
    public async Task BulkInsert_100000Orders_AllPersistedToDatabase()
    {
        // Arrange
        var orders = _fixture.CreateMany<Order>(100_000).ToList();

        // Act
        _db.Orders.AddRange(orders);
        await _db.SaveChangesAsync();

        // Assert
        var count = await _db.Orders.CountAsync();
        count.ShouldBe(100_000);
    }
}
