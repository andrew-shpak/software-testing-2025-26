using System.Net;
using System.Net.Http.Json;
using Lectures.Api.Tests.BlackBox.Fixtures;
using Lectures.Api.Tests.BlackBox.Models;
using Shouldly;

namespace Lectures.Api.Tests.BlackBox.Orders;

public class OrdersBlackBoxTests : IClassFixture<BlackBoxApiFixture>
{
    private readonly HttpClient _client;

    public OrdersBlackBoxTests(BlackBoxApiFixture fixture)
    {
        _client = fixture.HttpClient;
    }

    // --- POST /api/orders ---

    [Fact]
    public async Task Create_ValidOrder_Returns201WithOrderAsync()
    {
        // Arrange
        var request = new CreateOrderRequest("Alice", "alice@test.com", "Laptop", 2, 999.99m);

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        order.ShouldNotBeNull();
        order.CustomerName.ShouldBe("Alice");
        order.CustomerEmail.ShouldBe("alice@test.com");
        order.ProductName.ShouldBe("Laptop");
        order.Quantity.ShouldBe(2);
        order.UnitPrice.ShouldBe(999.99m);
        order.TotalPrice.ShouldBe(1999.98m);
        order.Status.ShouldBe("Pending");
        order.Id.ShouldBeGreaterThan(0);
        response.Headers.Location.ShouldNotBeNull();
    }

    [Fact]
    public async Task Create_InvalidOrder_EmptyFields_Returns400Async()
    {
        // Arrange
        var request = new CreateOrderRequest("", "not-an-email", "", 0, -5m);

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_InvalidOrder_ExceedsLimits_Returns400Async()
    {
        // Arrange
        var longName = new string('A', 101);
        var request = new CreateOrderRequest(longName, "user@test.com", "Item", 1001, 100_000m);

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // --- GET /api/orders ---

    [Fact]
    public async Task GetAll_ReturnsOkWithListAsync()
    {
        // Act
        var response = await _client.GetAsync("/api/orders");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var orders = await response.Content.ReadFromJsonAsync<List<OrderResponse>>();
        orders.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAll_AfterCreating_ContainsCreatedOrderAsync()
    {
        // Arrange
        var request = new CreateOrderRequest("Bob", "bob@test.com", "Phone", 1, 499m);
        var createResponse = await _client.PostAsJsonAsync("/api/orders", request);
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        // Act
        var response = await _client.GetAsync("/api/orders");
        var orders = await response.Content.ReadFromJsonAsync<List<OrderResponse>>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        orders.ShouldNotBeNull();
        orders.ShouldContain(o => o.Id == created!.Id);
    }

    // --- GET /api/orders/{id} ---

    [Fact]
    public async Task GetById_ExistingOrder_ReturnsOrderAsync()
    {
        // Arrange
        var request = new CreateOrderRequest("Carol", "carol@test.com", "Tablet", 3, 250m);
        var createResponse = await _client.PostAsJsonAsync("/api/orders", request);
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        // Act
        var response = await _client.GetAsync($"/api/orders/{created!.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        order.ShouldNotBeNull();
        order.CustomerName.ShouldBe("Carol");
        order.ProductName.ShouldBe("Tablet");
        order.Quantity.ShouldBe(3);
        order.TotalPrice.ShouldBe(750m);
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404Async()
    {
        // Act
        var response = await _client.GetAsync("/api/orders/99999");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // --- PUT /api/orders/{id}/status ---

    [Fact]
    public async Task UpdateStatus_PendingToConfirmed_Returns200Async()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest("Dave", "dave@test.com", "Monitor", 1, 300m));
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/orders/{created!.Id}/status",
            new UpdateOrderStatusRequest(1)); // Confirmed = 1

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        order.ShouldNotBeNull();
        order.Status.ShouldBe("Confirmed");
    }

    [Fact]
    public async Task UpdateStatus_ConfirmedToShipped_Returns200Async()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest("Eve", "eve@test.com", "Keyboard", 1, 80m));
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();
        await _client.PutAsJsonAsync($"/api/orders/{created!.Id}/status",
            new UpdateOrderStatusRequest(1)); // Confirmed

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/orders/{created.Id}/status",
            new UpdateOrderStatusRequest(2)); // Shipped = 2

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        order!.Status.ShouldBe("Shipped");
    }

    [Fact]
    public async Task UpdateStatus_CancelledOrder_Returns409Async()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest("Frank", "frank@test.com", "Mouse", 1, 25m));
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();
        await _client.PutAsJsonAsync($"/api/orders/{created!.Id}/status",
            new UpdateOrderStatusRequest(3)); // Cancelled

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/orders/{created.Id}/status",
            new UpdateOrderStatusRequest(1)); // Try to Confirm

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateStatus_ShippedToOther_Returns409Async()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest("Grace", "grace@test.com", "Headphones", 1, 150m));
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();
        await _client.PutAsJsonAsync($"/api/orders/{created!.Id}/status",
            new UpdateOrderStatusRequest(1)); // Confirmed
        await _client.PutAsJsonAsync($"/api/orders/{created.Id}/status",
            new UpdateOrderStatusRequest(2)); // Shipped

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/orders/{created.Id}/status",
            new UpdateOrderStatusRequest(1)); // Try to Confirm

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateStatus_NonExistent_Returns404Async()
    {
        // Act
        var response = await _client.PutAsJsonAsync(
            "/api/orders/99999/status",
            new UpdateOrderStatusRequest(1));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // --- DELETE /api/orders/{id} ---

    [Fact]
    public async Task Delete_ExistingPendingOrder_Returns204Async()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest("Hank", "hank@test.com", "Cable", 5, 10m));
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/orders/{created!.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_RemovedOrder_NotFoundOnGetAsync()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest("Ivy", "ivy@test.com", "Charger", 1, 30m));
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();
        await _client.DeleteAsync($"/api/orders/{created!.Id}");

        // Act
        var response = await _client.GetAsync($"/api/orders/{created.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ShippedOrder_Returns409Async()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest("Jack", "jack@test.com", "Speaker", 1, 200m));
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();
        await _client.PutAsJsonAsync($"/api/orders/{created!.Id}/status",
            new UpdateOrderStatusRequest(1)); // Confirmed
        await _client.PutAsJsonAsync($"/api/orders/{created.Id}/status",
            new UpdateOrderStatusRequest(2)); // Shipped

        // Act
        var response = await _client.DeleteAsync($"/api/orders/{created.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404Async()
    {
        // Act
        var response = await _client.DeleteAsync("/api/orders/99999");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // --- Full lifecycle ---

    [Fact]
    public async Task FullOrderLifecycle_CreateConfirmShip_SucceedsAsync()
    {
        // Create
        var createResponse = await _client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest("Lifecycle", "life@test.com", "Widget", 10, 5.5m));
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();
        order!.Status.ShouldBe("Pending");
        order.TotalPrice.ShouldBe(55m);

        // Confirm
        var confirmResponse = await _client.PutAsJsonAsync(
            $"/api/orders/{order.Id}/status",
            new UpdateOrderStatusRequest(1));
        confirmResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var confirmed = await confirmResponse.Content.ReadFromJsonAsync<OrderResponse>();
        confirmed!.Status.ShouldBe("Confirmed");

        // Ship
        var shipResponse = await _client.PutAsJsonAsync(
            $"/api/orders/{order.Id}/status",
            new UpdateOrderStatusRequest(2));
        shipResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var shipped = await shipResponse.Content.ReadFromJsonAsync<OrderResponse>();
        shipped!.Status.ShouldBe("Shipped");

        // Verify final state via GET
        var getResponse = await _client.GetAsync($"/api/orders/{order.Id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var final = await getResponse.Content.ReadFromJsonAsync<OrderResponse>();
        final!.Status.ShouldBe("Shipped");
        final.CustomerName.ShouldBe("Lifecycle");
    }
}
