using Lectures.Api.Data;
using Lectures.Api.Orders.Requests;
using Lectures.Api.Orders.Responses;
using Lectures.Api.Domain;
using Lectures.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Lectures.Api.Tests.Orders;

public class OrderServiceTests : IDisposable
{
    private readonly FakeTimeProvider _timeProvider;
    private readonly AppDbContext _db;
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero));

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _sut = new OrderService(_db, _timeProvider);
    }

    public void Dispose() => _db.Dispose();

    // --- GetByIdAsync ---

    [Fact]
    public async Task GetById_ExistingOrder_ReturnsOrderResponseAsync()
    {
        // Arrange
        var created = await _sut.CreateAsync(ValidRequest());

        // Act
        var result = await _sut.GetByIdAsync(created.Id);

        // Assert
        result.Id.ShouldBe(created.Id);
        result.CustomerName.ShouldBe("John Doe");
        result.TotalPrice.ShouldBe(50m);
        result.Status.ShouldBe("Pending");
    }

    [Fact]
    public async Task GetById_NonExistentOrder_ThrowsKeyNotFoundExceptionAsync()
    {
        // Act & Assert
        await Should.ThrowAsync<KeyNotFoundException>(() => _sut.GetByIdAsync(999));
    }

    // --- GetAllAsync ---

    [Fact]
    public async Task GetAll_WithOrders_ReturnsAllOrdersAsync()
    {
        // Arrange
        await _sut.CreateAsync(ValidRequest());
        await _sut.CreateAsync(ValidRequest() with { CustomerName = "Jane Doe" });

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetAll_NoOrders_ReturnsEmptyListAsync()
    {
        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.ShouldBeEmpty();
    }

    // --- CreateAsync ---

    [Fact]
    public async Task Create_ValidRequest_ReturnsOrderWithCorrectDataAsync()
    {
        // Act
        var result = await _sut.CreateAsync(ValidRequest());

        // Assert
        result.Id.ShouldBeGreaterThan(0);
        result.CustomerName.ShouldBe("John Doe");
        result.CustomerEmail.ShouldBe("john@example.com");
        result.TotalPrice.ShouldBe(50m);
        result.Status.ShouldBe("Pending");
    }

    [Fact]
    public async Task Create_UsesTimeProvider_SetsCorrectCreatedAtAsync()
    {
        // Act
        var result = await _sut.CreateAsync(ValidRequest());

        // Assert
        result.CreatedAt.ShouldBe(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Create_AfterTimeAdvance_ReflectsNewTimeAsync()
    {
        // Arrange
        await _sut.CreateAsync(ValidRequest());
        _timeProvider.Advance(TimeSpan.FromHours(2));

        // Act
        var result = await _sut.CreateAsync(ValidRequest() with { CustomerName = "Jane" });

        // Assert
        result.CreatedAt.ShouldBe(new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc));
    }

    // --- UpdateStatusAsync ---

    [Fact]
    public async Task UpdateStatus_PendingToConfirmed_UpdatesSuccessfullyAsync()
    {
        // Arrange
        var order = await _sut.CreateAsync(ValidRequest());

        // Act
        var result = await _sut.UpdateStatusAsync(order.Id,
            new UpdateOrderStatusRequest(OrderStatusDto.Confirmed));

        // Assert
        result.Status.ShouldBe("Confirmed");
    }

    [Fact]
    public async Task UpdateStatus_CancelledOrder_ThrowsInvalidOperationAsync()
    {
        // Arrange
        var order = await _sut.CreateAsync(ValidRequest());
        await _sut.UpdateStatusAsync(order.Id, new UpdateOrderStatusRequest(OrderStatusDto.Cancelled));

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.UpdateStatusAsync(order.Id, new UpdateOrderStatusRequest(OrderStatusDto.Confirmed)));
        ex.Message.ShouldContain("cancelled");
    }

    [Fact]
    public async Task UpdateStatus_ShippedToConfirmed_ThrowsInvalidOperationAsync()
    {
        // Arrange
        var order = await _sut.CreateAsync(ValidRequest());
        await _sut.UpdateStatusAsync(order.Id, new UpdateOrderStatusRequest(OrderStatusDto.Confirmed));
        await _sut.UpdateStatusAsync(order.Id, new UpdateOrderStatusRequest(OrderStatusDto.Shipped));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.UpdateStatusAsync(order.Id, new UpdateOrderStatusRequest(OrderStatusDto.Confirmed)));
    }

    [Fact]
    public async Task UpdateStatus_NonExistentOrder_ThrowsKeyNotFoundAsync()
    {
        // Act & Assert
        await Should.ThrowAsync<KeyNotFoundException>(
            () => _sut.UpdateStatusAsync(999, new UpdateOrderStatusRequest(OrderStatusDto.Confirmed)));
    }

    // --- DeleteAsync ---

    [Fact]
    public async Task Delete_ExistingPendingOrder_ReturnsTrueAsync()
    {
        // Arrange
        var order = await _sut.CreateAsync(ValidRequest());

        // Act
        var result = await _sut.DeleteAsync(order.Id);

        // Assert
        result.ShouldBeTrue();
        await Should.ThrowAsync<KeyNotFoundException>(() => _sut.GetByIdAsync(order.Id));
    }

    [Fact]
    public async Task Delete_NonExistentOrder_ReturnsFalseAsync()
    {
        // Act
        var result = await _sut.DeleteAsync(999);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_ShippedOrder_ThrowsInvalidOperationAsync()
    {
        // Arrange
        var order = await _sut.CreateAsync(ValidRequest());
        await _sut.UpdateStatusAsync(order.Id, new UpdateOrderStatusRequest(OrderStatusDto.Confirmed));
        await _sut.UpdateStatusAsync(order.Id, new UpdateOrderStatusRequest(OrderStatusDto.Shipped));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => _sut.DeleteAsync(order.Id));
    }

    // --- Helper ---

    private static CreateOrderRequest ValidRequest() =>
        new("John Doe", "john@example.com", "Widget", 5, 10m);
}
