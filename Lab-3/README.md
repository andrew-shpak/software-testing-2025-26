# Lab 3 — Integration Testing: Components

## Objective

Learn to write integration tests that verify how multiple components work together. Test real interactions between services, repositories, and middleware without mocking everything.

## Prerequisites

Before starting this lab, make sure you have:

- .NET 10+ SDK installed (`dotnet --version`)
- A working understanding of ASP.NET Core middleware pipeline, dependency injection, and the controller/service/repository pattern
- Familiarity with xUnit v3 test lifecycle (`IAsyncLifetime`, `IClassFixture<T>`)
- Completed Lab 1 and Lab 2 (unit testing fundamentals and mocking)

## Key Concepts

### WebApplicationFactory

`WebApplicationFactory<TEntryPoint>` bootstraps your ASP.NET Core application in-memory for testing. It creates a `TestServer` and an `HttpClient` that sends requests directly to the pipeline without network overhead. You subclass it to customize services, configuration, or middleware.

### Middleware Pipeline

ASP.NET Core processes every HTTP request through an ordered pipeline of middleware components. Integration tests verify that the entire pipeline (authentication, logging, exception handling, routing, controller execution) works together as expected.

### Dependency Injection in Tests

The DI container can be reconfigured inside `WebApplicationFactory.WithWebHostBuilder` to replace real services with test doubles or in-memory implementations. This lets you test real component interactions while controlling external dependencies.

### Test Isolation

Each test must be independent. Shared state between tests leads to flaky results. Use unique database names, fresh `HttpClient` instances, or `IAsyncLifetime` to set up and tear down per-test state.

## Tools

- Language: C#
- Framework: [xUnit v3](https://xunit.net/) (`xunit.v3`)
- Web: [ASP.NET Core TestServer](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
- DI: `Microsoft.Extensions.DependencyInjection`

## Setup

```bash
dotnet new sln -n Lab3
dotnet new webapi -n Lab3.Api
dotnet new classlib -n Lab3.Tests
dotnet sln add Lab3.Api Lab3.Tests
dotnet add Lab3.Tests reference Lab3.Api
dotnet add Lab3.Tests package xunit.v3
dotnet add Lab3.Tests package Microsoft.NET.Test.Sdk
dotnet add Lab3.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet add Lab3.Tests package Shouldly
```

## Tasks

### Task 1 — WebApplicationFactory Setup

Create a minimal ASP.NET Core Web API with:

- `ProductsController` with CRUD endpoints (`GET`, `POST`, `PUT`, `DELETE`)
- `IProductRepository` interface with in-memory implementation
- Service registration in `Program.cs`

Write integration tests using `WebApplicationFactory<Program>`:

1. Create a custom `WebApplicationFactory` that replaces the real repository with a seeded in-memory one
2. Test `GET /api/products` returns all seeded products
3. Test `GET /api/products/{id}` returns 200 for existing and 404 for non-existing
4. Test `POST /api/products` creates a product and returns 201

**Minimum test count: 6 tests**

#### Example: Custom WebApplicationFactory

```csharp
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove real repository registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IProductRepository));
            if (descriptor != null)
                services.Remove(descriptor);

            // Add seeded in-memory repository
            services.AddSingleton<IProductRepository>(sp =>
            {
                var repo = new InMemoryProductRepository();
                repo.Add(new Product { Id = 1, Name = "Laptop", Price = 999.99m });
                repo.Add(new Product { Id = 2, Name = "Mouse", Price = 29.99m });
                return repo;
            });
        });
    }
}
```

#### Example: Integration Test with Shouldly

```csharp
public class ProductsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProductsApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetProducts_ReturnsAllSeededProductsAsync()
    {
        // Act
        var response = await _client.GetAsync("/api/products");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<List<Product>>();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(2);
        products.ShouldContain(p => p.Name == "Laptop");
    }

    [Fact]
    public async Task GetProductById_NonExisting_Returns404Async()
    {
        // Act
        var response = await _client.GetAsync("/api/products/999");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostProduct_ValidProduct_Returns201WithLocationAsync()
    {
        // Arrange
        var newProduct = new { Name = "Keyboard", Price = 49.99m };

        // Act
        var response = await _client.PostAsJsonAsync("/api/products", newProduct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
    }
}
```

#### Expected Behavior Table

| Endpoint | Scenario | Expected Status | Expected Body |
|---|---|---|---|
| `GET /api/products` | Seeded data exists | 200 OK | Array of 2 products |
| `GET /api/products/1` | Product exists | 200 OK | Product JSON |
| `GET /api/products/999` | Product not found | 404 Not Found | Error or empty |
| `POST /api/products` | Valid product | 201 Created | Created product + Location header |
| `PUT /api/products/1` | Valid update | 200 OK | Updated product |
| `DELETE /api/products/1` | Product exists | 204 No Content | Empty |

> **Hint:** Make `Program` accessible to tests by adding `public partial class Program { }` at the bottom of your `Program.cs`, or by adding `[assembly: InternalsVisibleTo("Lab3.Tests")]` in the API project.

### Task 2 — Middleware and Pipeline Testing

Add the following middleware to the API:

- Request logging middleware (logs method, path, status code)
- Exception handling middleware (catches unhandled exceptions, returns 500 with JSON error)
- API key authentication middleware (checks `X-Api-Key` header)

Write integration tests that:

1. Verify requests without `X-Api-Key` return 401
2. Verify requests with invalid key return 403
3. Verify unhandled exceptions return structured JSON error response
4. Verify the pipeline processes requests in correct order

**Minimum test count: 6 tests**

#### Example: Middleware Test with Shouldly

```csharp
[Fact]
public async Task Request_WithoutApiKey_Returns401Async()
{
    // Arrange — client without default headers
    var client = _factory.CreateClient();

    // Act
    var response = await client.GetAsync("/api/products");

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
}

[Fact]
public async Task Request_WithInvalidApiKey_Returns403Async()
{
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

    // Act
    var response = await client.GetAsync("/api/products");

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
}

[Fact]
public async Task UnhandledException_ReturnsStructuredJsonErrorAsync()
{
    // Arrange — configure a factory where a controller throws
    var factory = _factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IProductRepository, ThrowingRepository>();
        });
    });
    var client = factory.CreateClient();
    client.DefaultRequestHeaders.Add("X-Api-Key", "valid-test-key");

    // Act
    var response = await client.GetAsync("/api/products");

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    body.GetProperty("error").GetString().ShouldNotBeNullOrEmpty();
}
```

#### Expected Behavior Table

| Scenario | `X-Api-Key` Header | Expected Status |
|---|---|---|
| No header sent | (absent) | 401 Unauthorized |
| Invalid key | `"wrong-key"` | 403 Forbidden |
| Valid key | `"valid-test-key"` | 200 OK (or endpoint result) |
| Endpoint throws exception | Valid key | 500 with JSON body |

> **Hint:** For pipeline ordering, consider adding a custom response header from each middleware (e.g., `X-Pipeline-Step: 1`, `X-Pipeline-Step: 2`) and verifying the order in the response.

### Task 3 — Dependency Injection Verification

Write tests that verify:

1. All required services are registered in the DI container
2. Scoped services create new instances per request
3. Singleton services return the same instance
4. Replacing a service in tests does not affect other registrations

**Minimum test count: 5 tests**

#### Example: DI Verification with Shouldly

```csharp
[Fact]
public void AllRequiredServices_AreRegistered()
{
    // Arrange
    using var scope = _factory.Services.CreateScope();

    // Act & Assert
    var repo = scope.ServiceProvider.GetService<IProductRepository>();
    repo.ShouldNotBeNull();

    var controller = scope.ServiceProvider.GetService<ProductsController>();
    // Controllers are not registered by default, so resolve via ControllerActivator or check services
}

[Fact]
public void ScopedService_CreatesNewInstancePerScope()
{
    // Arrange & Act
    using var scope1 = _factory.Services.CreateScope();
    using var scope2 = _factory.Services.CreateScope();

    var instance1 = scope1.ServiceProvider.GetRequiredService<IScopedService>();
    var instance2 = scope2.ServiceProvider.GetRequiredService<IScopedService>();

    // Assert
    instance1.ShouldNotBeSameAs(instance2);
}

[Fact]
public void SingletonService_ReturnsSameInstance()
{
    // Arrange & Act
    using var scope1 = _factory.Services.CreateScope();
    using var scope2 = _factory.Services.CreateScope();

    var instance1 = scope1.ServiceProvider.GetRequiredService<ISingletonService>();
    var instance2 = scope2.ServiceProvider.GetRequiredService<ISingletonService>();

    // Assert
    instance1.ShouldBeSameAs(instance2);
}
```

> **Hint:** You can access the DI container directly via `WebApplicationFactory.Services`. Use `CreateScope()` to simulate scoped lifetimes similar to HTTP requests.

## Grading

| Criteria |
|----------|
| Task 1 — WebApplicationFactory tests |
| Task 2 — Middleware tests |
| Task 3 — DI verification tests |
| Proper test isolation (each test is independent) |
| Use of `IClassFixture` or `IAsyncLifetime` |

## Submission

- Solution with `Lab3.Api` and `Lab3.Tests` projects
- Tests should pass with `dotnet test` without any external dependencies

## References

- [Integration tests in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) — official Microsoft guide to `WebApplicationFactory`
- [xUnit v3 Documentation](https://xunit.net/docs/getting-started/v3/cmdline) — test framework reference
- [Shouldly Documentation](https://docs.shouldly.org/) — assertion library API
- [ASP.NET Core Middleware](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/) — middleware pipeline concepts
- [Dependency Injection in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection) — service lifetimes (Transient, Scoped, Singleton)
- [Andrew Lock: Integration Testing with WebApplicationFactory](https://andrewlock.net/introduction-to-integration-testing-with-xunit-and-testserver-in-asp-net-core/) — practical walkthrough
