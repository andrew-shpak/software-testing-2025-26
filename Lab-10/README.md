# Lab 10 — Services Testing: Microservices and Contract Testing

## Objective

Learn contract testing for microservice communication. Ensure that service providers and consumers agree on API contracts without requiring end-to-end integration.

## Prerequisites

Before starting this lab, make sure you have:

- .NET 10 SDK (or later) installed
- A working understanding of REST APIs and HTTP methods
- Familiarity with xUnit test structure (from previous labs)
- Basic knowledge of JSON serialization/deserialization in C#
- Completed Labs 1-9 (especially Labs 7-8 on integration and API testing)

Install the Pact CLI tools (optional, for debugging):

```bash
# On macOS
brew install pact-foundation/pact-ruby-standalone/pact

# On Windows (via Chocolatey)
choco install pact
```

## Key Concepts

### What Is Contract Testing?

Contract testing verifies that two services (a **consumer** and a **provider**) can communicate correctly by testing against a shared **contract** (also called a "pact"). Unlike end-to-end integration tests, contract tests run independently for each service.

**Why not just use integration tests?**

| Aspect | Integration Tests | Contract Tests |
|--------|-------------------|----------------|
| Speed | Slow (both services must run) | Fast (each side tested independently) |
| Reliability | Flaky (network, environment) | Deterministic (no real network calls) |
| Feedback | Late (need deployed services) | Early (runs in unit test phase) |
| Scope | Tests everything at once | Tests only the API boundary |

### The Pact Workflow

1. **Consumer writes tests** describing what it expects from the provider (requests and expected responses).
2. **PactNet generates a Pact file** (JSON) capturing those expectations.
3. **Provider verifies the Pact file** by replaying the interactions against its real implementation.
4. If verification passes, both sides are compatible. If it fails, the contract is broken.

```
Consumer Tests          Pact File (JSON)          Provider Verification
 ┌───────────┐         ┌───────────────┐         ┌───────────────────┐
 │ Define     │ ──────> │ Interactions  │ ──────> │ Replay against    │
 │ expected   │ generate│ as JSON       │ verify  │ real provider API │
 │ requests & │         │               │         │                   │
 │ responses  │         └───────────────┘         └───────────────────┘
 └───────────┘
```

### Provider States

Provider states allow the consumer to describe what data should exist on the provider side before an interaction runs. For example:

- `"an order with id 1 exists"` -- the provider seeds an order with id=1
- `"no order with id 999 exists"` -- the provider ensures no such order exists

The provider test configures a state handler that sets up the required data for each state.

## Tools

- Language: C#
- Contract Testing: [PactNet](https://github.com/pact-foundation/pact-net)
- Framework: [xUnit v3](https://xunit.net/) (`xunit.v3`)

## Setup

```bash
dotnet new sln -n Lab10
dotnet new webapi -n Lab10.OrderService
dotnet new classlib -n Lab10.OrderClient
dotnet new classlib -n Lab10.Consumer.Tests
dotnet new classlib -n Lab10.Provider.Tests
dotnet sln add Lab10.OrderService Lab10.OrderClient Lab10.Consumer.Tests Lab10.Provider.Tests
dotnet add Lab10.Consumer.Tests reference Lab10.OrderClient
dotnet add Lab10.Consumer.Tests package xunit.v3
dotnet add Lab10.Consumer.Tests package Microsoft.NET.Test.Sdk
dotnet add Lab10.Consumer.Tests package PactNet
dotnet add Lab10.Provider.Tests reference Lab10.OrderService
dotnet add Lab10.Provider.Tests package xunit.v3
dotnet add Lab10.Provider.Tests package Microsoft.NET.Test.Sdk
dotnet add Lab10.Provider.Tests package PactNet
dotnet add Lab10.Provider.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet add Lab10.Consumer.Tests package Shouldly
dotnet add Lab10.Provider.Tests package Shouldly
```

## Scenario

You have two microservices:

- **Order Service** (provider) — manages orders via REST API
- **Notification Service** (consumer) — consumes Order Service API to get order details for sending emails

### Example Order Model

```csharp
public class Order
{
    public int Id { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Pending";
}

public class OrderItem
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
```

### Example API Endpoints (Provider)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/orders/{id}` | Get a single order by ID |
| `GET` | `/api/orders?customerId={id}` | Get all orders for a customer |
| `POST` | `/api/orders` | Create a new order |

### Example Consumer Client

```csharp
public class OrderClient
{
    private readonly HttpClient _httpClient;

    public OrderClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Order?> GetOrderAsync(int id)
    {
        var response = await _httpClient.GetAsync($"/api/orders/{id}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Order>();
    }

    public async Task<List<Order>> GetOrdersByCustomerAsync(int customerId)
    {
        var response = await _httpClient.GetAsync($"/api/orders?customerId={customerId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Order>>() ?? new();
    }
}
```

## Tasks

### Task 1 — Consumer-Side Contract Tests

In `Lab10.Consumer.Tests`, write Pact consumer tests:

1. Define expected interaction for `GET /api/orders/{id}`:
   - Request: `GET /api/orders/1` with `Accept: application/json`
   - Expected response: 200 with JSON body containing `id`, `customerEmail`, `items`, `totalAmount`, `status`

2. Define expected interaction for `GET /api/orders?customerId={id}`:
   - Request: `GET /api/orders?customerId=42`
   - Expected response: 200 with JSON array of orders

3. Define expected interaction for non-existing order:
   - Request: `GET /api/orders/999`
   - Expected response: 404

4. Define expected interaction for `POST /api/orders`:
   - Request: POST with order JSON body
   - Expected response: 201 with created order

5. Generate the Pact file and verify it is created in `pacts/` directory

#### Consumer Test Structure Example

```csharp
public class OrderApiConsumerTests
{
    private readonly IPactBuilderV4 _pactBuilder;

    public OrderApiConsumerTests()
    {
        var pact = Pact.V4("NotificationService", "OrderService", new PactConfig
        {
            PactDir = Path.Combine("..", "..", "..", "..", "pacts")
        });

        _pactBuilder = pact.WithHttpInteractions();
    }

    [Fact]
    public async Task GetOrder_WhenOrderExists_ReturnsOrderAsync()
    {
        // Arrange
        _pactBuilder
            .UponReceiving("a request for order with id 1")
            .Given("an order with id 1 exists")
            .WithRequest(HttpMethod.Get, "/api/orders/1")
            .WithHeader("Accept", "application/json")
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new
            {
                id = 1,
                customerEmail = Match.Type("customer@example.com"),
                items = Match.MinType(new { productName = "Widget", quantity = 1, price = 9.99 }, 1),
                totalAmount = Match.Decimal(9.99),
                status = Match.Type("Pending")
            });

        // Act & Assert
        await _pactBuilder.VerifyAsync(async ctx =>
        {
            var client = new OrderClient(new HttpClient { BaseAddress = ctx.MockServerUri });
            var order = await client.GetOrderAsync(1);

            order.ShouldNotBeNull();
            order.Id.ShouldBe(1);
            order.CustomerEmail.ShouldNotBeNullOrEmpty();
        });
    }
}
```

> **Hint:** PactNet matchers like `Match.Type(...)`, `Match.Decimal(...)`, and `Match.MinType(...)` let you define flexible contracts. `Match.Type` checks the type rather than the exact value, which makes contracts less brittle.

#### Expected Pact File Structure

After running the consumer tests, a file like `pacts/NotificationService-OrderService.json` should be created. It contains all defined interactions in JSON format.

### Task 2 — Provider-Side Contract Verification

In `Lab10.Provider.Tests`, verify the provider against the Pact file:

1. Set up `WebApplicationFactory` for the Order Service
2. Configure provider states:
   - `"an order with id 1 exists"` — seed test data
   - `"no order with id 999 exists"` — ensure clean state
   - `"customer 42 has orders"` — seed customer orders
3. Run Pact verification and ensure all interactions pass
4. Test that adding a new required field to the response breaks the contract

#### Provider Verification Example

```csharp
public class OrderApiProviderTests : IDisposable
{
    private readonly PactVerifier _verifier;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly IHost _host;

    public OrderApiProviderTests()
    {
        _factory = new WebApplicationFactory<Program>();
        _host = _factory.Server.Host;
        _verifier = new PactVerifier("OrderService", new PactVerifierConfig());
    }

    [Fact]
    public void VerifyPacts()
    {
        var pactPath = Path.Combine("..", "..", "..", "..", "pacts",
            "NotificationService-OrderService.json");

        _verifier
            .WithHttpEndpoint(_factory.Server.BaseAddress)
            .WithProviderStateUrl(new Uri(_factory.Server.BaseAddress, "/provider-states"))
            .WithFileSource(new FileInfo(pactPath))
            .Verify();
    }

    public void Dispose()
    {
        _factory.Dispose();
        _verifier.Dispose();
    }
}
```

> **Hint:** You need to implement a `/provider-states` endpoint (or middleware) in your test server that accepts POST requests from the verifier and sets up the required data. Look at `IStartupFilter` or middleware registration inside `WebApplicationFactory<T>.WithWebHostBuilder(...)`.

#### Provider State Handler Pattern

```csharp
// In your test project, add middleware to handle provider states
app.MapPost("/provider-states", async (HttpContext context) =>
{
    var providerState = await context.Request.ReadFromJsonAsync<ProviderState>();

    switch (providerState?.State)
    {
        case "an order with id 1 exists":
            // Seed the database or in-memory store with an order
            break;
        case "no order with id 999 exists":
            // Ensure no order with id 999 exists (clean state)
            break;
        case "customer 42 has orders":
            // Seed multiple orders for customer 42
            break;
    }
});
```

### Task 3 — Contract Evolution

Document and test contract evolution scenarios:

1. **Adding optional field**: Add `deliveryDate` to order response. Verify contract still passes (backward compatible).
2. **Removing a field**: Remove `status` from response. Verify consumer contract breaks.
3. **Changing field type**: Change `totalAmount` from `number` to `string`. Verify contract breaks.

Write tests proving each scenario and document findings in `REPORT.md`.

#### Expected Behavior for Contract Evolution

| Change | Consumer Impact | Provider Verification | Backward Compatible? |
|--------|----------------|----------------------|---------------------|
| Add optional `deliveryDate` | Consumer ignores unknown fields | Passes (extra fields are OK) | Yes |
| Remove `status` | Consumer expects `status` | Fails (missing required field) | No |
| Change `totalAmount` type | Consumer parses as wrong type | Fails (type mismatch) | No |

> **Hint:** The key insight is that contract tests follow **Postel's Law** (the Robustness Principle): "Be conservative in what you send, be liberal in what you accept." Adding new optional fields is safe. Removing or changing existing fields is a breaking change.

## Grading

| Criteria |
|----------|
| Task 1 — Consumer contract tests |
| Task 2 — Provider verification |
| Task 3 — Contract evolution |

## Submission

- Solution with all four projects
- Generated Pact files in `pacts/` directory
- `REPORT.md` with contract evolution analysis

## References

- [PactNet GitHub Repository](https://github.com/pact-foundation/pact-net) -- library source, examples, and README
- [Pact Documentation (Official)](https://docs.pact.io/) -- comprehensive guides for all Pact implementations
- [Pact Introduction: 5-Minute Guide](https://docs.pact.io/5-minute-getting-started-guide) -- quick-start tutorial
- [Consumer-Driven Contract Testing](https://martinfowler.com/articles/consumerDrivenContracts.html) -- Martin Fowler's article on the concept
- [Contract Testing vs Integration Testing](https://pactflow.io/blog/contract-testing-vs-integration-testing/) -- comparison of approaches
- [PactNet v5 Migration Guide](https://github.com/pact-foundation/pact-net/blob/master/docs/upgrading-to-5.md) -- if using PactNet v5
- [xUnit v3 Documentation](https://xunit.net/docs/getting-started/v3/cmdline) -- test framework reference
- [Shouldly Assertion Library](https://docs.shouldly.org/) -- assertion library used in this lab
- [ASP.NET Core Integration Testing with WebApplicationFactory](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) -- relevant for provider-side setup
