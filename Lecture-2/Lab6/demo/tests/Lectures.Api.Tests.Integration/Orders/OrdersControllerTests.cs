using System.Net;
using System.Net.Http.Json;
using Lectures.Api.Orders.Requests;
using Lectures.Api.Orders.Responses;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace Lectures.Api.Tests.Integration.Orders;

public class OrdersControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public OrdersControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    // --- GET /api/orders ---

    [Fact]
    public async Task GetAll_Initially_ReturnsEmptyListAsync()
    {
        // Act
        var response = await _client.GetAsync("/api/orders");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var orders = await response.Content.ReadFromJsonAsync<List<OrderResponse>>();
        orders.ShouldNotBeNull();
    }

    // --- POST /api/orders ---

    [Fact]
    public async Task Create_ValidOrder_Returns201WithOrderAsync()
    {
        // Arrange
        var request = new CreateOrderRequest("Alice", "alice@test.com", "Gadget", 2, 25.50m);

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        order.ShouldNotBeNull();
        order.CustomerName.ShouldBe("Alice");
        order.TotalPrice.ShouldBe(51m);
        order.Status.ShouldBe("Pending");
        response.Headers.Location.ShouldNotBeNull();
    }

    [Fact]
    public async Task Create_InvalidOrder_Returns400WithErrorsAsync()
    {
        // Arrange
        var request = new CreateOrderRequest("", "not-an-email", "", 0, -5m);

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // --- GET /api/orders/{id} ---

    [Fact]
    public async Task GetById_AfterCreate_ReturnsOrderAsync()
    {
        // Arrange
        var createRequest = new CreateOrderRequest("Bob", "bob@test.com", "Gizmo", 1, 100m);
        var createResponse = await _client.PostAsJsonAsync("/api/orders", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        // Act
        var response = await _client.GetAsync($"/api/orders/{created!.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        order.ShouldNotBeNull();
        order.CustomerName.ShouldBe("Bob");
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
            new CreateOrderRequest("Carol", "carol@test.com", "Thing", 3, 15m));
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/orders/{created!.Id}/status",
            new UpdateOrderStatusRequest(OrderStatusDto.Confirmed));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        order!.Status.ShouldBe("Confirmed");
    }

    [Fact]
    public async Task UpdateStatus_NonExistent_Returns404Async()
    {
        // Act
        var response = await _client.PutAsJsonAsync(
            "/api/orders/99999/status",
            new UpdateOrderStatusRequest(OrderStatusDto.Confirmed));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // --- DELETE /api/orders/{id} ---

    [Fact]
    public async Task Delete_ExistingOrder_Returns204Async()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest("Dave", "dave@test.com", "Doohickey", 1, 5m));
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/orders/{created!.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404Async()
    {
        // Act
        var response = await _client.DeleteAsync("/api/orders/99999");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
