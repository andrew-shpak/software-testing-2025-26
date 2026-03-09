# Lab 9 — Services Testing: REST API

## Objective

Test external REST API integrations using HTTP client abstraction, response handling, retry policies, and simulated API behavior with WireMock.

**Duration:** 60 minutes

## Prerequisites

- .NET 10 SDK or later installed
- C# fundamentals including interfaces, dependency injection, and async/await
- Understanding of HTTP methods (GET, POST, PUT, DELETE), status codes, and headers
- Familiarity with `HttpClient` and `IHttpClientFactory` in .NET
- Understanding of JSON serialization/deserialization with `System.Text.Json`
- Basic knowledge of resilience patterns: retries, circuit breakers, and timeouts

## Key Concepts

| Concept | Description |
|---------|-------------|
| **Typed HttpClient** | A strongly-typed wrapper around `HttpClient` registered via `IHttpClientFactory`. Provides clean separation and testability. |
| **WireMock.Net** | An in-process HTTP server that can be programmed to return specific responses for specific requests. Replaces a real external API during tests. |
| **Stub vs Mock (HTTP context)** | A WireMock stub returns a canned response. A WireMock mock also verifies that certain requests were made (request verification). |
| **Resilience Pipeline** | A chain of strategies (retry, circuit breaker, timeout) applied to outgoing HTTP requests via `Microsoft.Extensions.Http.Resilience`. |
| **Retry Policy** | Automatically re-sends a failed request a configured number of times, with optional backoff between attempts. Targets transient faults (5xx, network errors). |
| **Circuit Breaker** | Monitors failure rates and "opens" (blocks all requests) when failures exceed a threshold, preventing cascading failures. After a cooldown, it "half-opens" to probe recovery. |
| **Attempt Timeout** | Cancels a single HTTP request if it does not complete within a configured duration. Different from an overall timeout across retries. |
| **IAsyncLifetime** | An xUnit interface for async setup (`InitializeAsync`) and teardown (`DisposeAsync`). Used to start and stop the WireMock server per test class. |

## Tools

- Language: C#
- HTTP: `HttpClient` / `IHttpClientFactory`
- Mock Server: [WireMock.Net](https://github.com/WireMock-Net/WireMock.Net)
- Resilience: [Microsoft.Extensions.Http.Resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience)
- Framework: [xUnit v3](https://xunit.net/) (`xunit.v3`)

## Setup

```bash
dotnet new sln -n Lab9
dotnet new classlib -n Lab9.Core
dotnet new classlib -n Lab9.Tests
dotnet sln add Lab9.Core Lab9.Tests
dotnet add Lab9.Core package Microsoft.Extensions.Http
dotnet add Lab9.Core package Microsoft.Extensions.Http.Resilience
dotnet add Lab9.Tests reference Lab9.Core
dotnet add Lab9.Tests package xunit.v3
dotnet add Lab9.Tests package Microsoft.NET.Test.Sdk
dotnet add Lab9.Tests package WireMock.Net
dotnet add Lab9.Tests package Shouldly
```

## Tasks

### Task 1 — External API Client

Create a typed HTTP client for a hypothetical User API:

```csharp
public interface IUserApiClient
{
    Task<User> GetUserAsync(int id);
    Task<IEnumerable<User>> GetUsersAsync(int page, int pageSize);
    Task<User> CreateUserAsync(CreateUserRequest request);
    Task UpdateUserAsync(int id, UpdateUserRequest request);
    Task DeleteUserAsync(int id);
}

public class UserApiClient : IUserApiClient
{
    private readonly HttpClient _httpClient;
    // Implement all methods with proper error handling
    // Map HTTP status codes to appropriate exceptions
}
```

Implement:

- Proper deserialization of responses
- Throw `NotFoundException` for 404
- Throw `ApiException` for 5xx with retry info
- Map validation errors (400) to `ValidationException`

**Example — Domain Models and Custom Exceptions**

```csharp
public record User(int Id, string Name, string Email);
public record CreateUserRequest(string Name, string Email);
public record UpdateUserRequest(string Name, string Email);

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public class ApiException : Exception
{
    public int StatusCode { get; }
    public ApiException(int statusCode, string message)
        : base(message) => StatusCode = statusCode;
}

public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }
    public ValidationException(IDictionary<string, string[]> errors)
        : base("Validation failed") => Errors = errors;
}
```

**Example — UserApiClient Implementation (partial)**

```csharp
public class UserApiClient : IUserApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public UserApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<User> GetUserAsync(int id)
    {
        var response = await _httpClient.GetAsync($"/users/{id}");

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new NotFoundException($"User {id} not found");

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<User>(JsonOptions)
            ?? throw new InvalidOperationException("Null response body");
    }

    // Implement remaining methods following the same pattern...
}
```

**Expected Status Code Mapping**

| HTTP Status Code | Expected Behavior |
|-----------------|-------------------|
| 200 OK | Deserialize and return the response body |
| 201 Created | Deserialize the created entity from the response body |
| 204 No Content | Return successfully (no body to parse) |
| 400 Bad Request | Throw `ValidationException` with field-level error details |
| 404 Not Found | Throw `NotFoundException` with a descriptive message |
| 500 Internal Server Error | Throw `ApiException` with the status code |
| 502 Bad Gateway | Throw `ApiException` (transient, eligible for retry) |
| 503 Service Unavailable | Throw `ApiException` (transient, eligible for retry) |

**Minimum test count for Task 1**: 4 tests (covering the main interface methods verifying the happy path).

> **Hint**: Register `UserApiClient` as a typed client so `IHttpClientFactory` manages its `HttpClient` lifetime. This avoids socket exhaustion:
> ```csharp
> services.AddHttpClient<IUserApiClient, UserApiClient>(client =>
> {
>     client.BaseAddress = new Uri("https://api.example.com");
> });
> ```

### Task 2 — WireMock Integration Tests

Use WireMock.Net to simulate the external API:

```csharp
var server = WireMockServer.Start();
```

Write tests that:

1. Mock `GET /users/1` returning a JSON user — verify deserialization
2. Mock `GET /users/999` returning 404 — verify `NotFoundException` is thrown
3. Mock `POST /users` returning 201 with `Location` header — verify response parsing
4. Mock `GET /users` with query parameters — verify pagination is sent correctly

*Optional (if time allows):*
- Mock slow response (5 second delay) — verify timeout handling
- Mock sequence: first call returns 500, second returns 200 — verify retry works
- Verify request headers (`Content-Type`, `Authorization`) are sent correctly

**Example — WireMock Test Fixture with IAsyncLifetime**

```csharp
using WireMock.Server;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using System.Text.Json;

public class UserApiClientTests : IAsyncLifetime
{
    private WireMockServer _server = null!;
    private UserApiClient _client = null!;

    public Task InitializeAsync()
    {
        _server = WireMockServer.Start();
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(_server.Url!)
        };
        _client = new UserApiClient(httpClient);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _server.Stop();
        _server.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetUserAsync_ReturnsUser_WhenApiReturns200Async()
    {
        // Arrange
        _server
            .Given(Request.Create().WithPath("/users/1").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(
                    new { Id = 1, Name = "Alice", Email = "alice@example.com" }))
            );

        // Act
        var user = await _client.GetUserAsync(1);

        // Assert
        user.Id.ShouldBe(1);
        user.Name.ShouldBe("Alice");
        user.Email.ShouldBe("alice@example.com");
    }

    [Fact]
    public async Task GetUserAsync_ThrowsNotFoundException_WhenApiReturns404Async()
    {
        // Arrange
        _server
            .Given(Request.Create().WithPath("/users/999").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => _client.GetUserAsync(999));
    }
}
```

**Example — Testing a Slow Response (Timeout)**

```csharp
[Fact]
public async Task GetUserAsync_ThrowsTaskCanceledException_WhenResponseIsSlowAsync()
{
    // Arrange
    _server
        .Given(Request.Create().WithPath("/users/1").UsingGet())
        .RespondWith(Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", "application/json")
            .WithBody(JsonSerializer.Serialize(
                new { Id = 1, Name = "Slow", Email = "slow@example.com" }))
            .WithDelay(TimeSpan.FromSeconds(5))
        );

    var httpClient = new HttpClient
    {
        BaseAddress = new Uri(_server.Url!),
        Timeout = TimeSpan.FromSeconds(2) // timeout before the 5s delay
    };
    var client = new UserApiClient(httpClient);

    // Act & Assert
    await Should.ThrowAsync<TaskCanceledException>(
        () => client.GetUserAsync(1));
}
```

**Example — Testing Retry with Response Sequence**

```csharp
[Fact]
public async Task GetUserAsync_RetriesAndSucceeds_WhenFirstCallFailsAsync()
{
    // Arrange: first call returns 500, second returns 200
    _server
        .Given(Request.Create().WithPath("/users/1").UsingGet())
        .InScenario("retry")
        .WillSetStateTo("first-call-done")
        .RespondWith(Response.Create().WithStatusCode(500));

    _server
        .Given(Request.Create().WithPath("/users/1").UsingGet())
        .InScenario("retry")
        .WhenStateIs("first-call-done")
        .RespondWith(Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", "application/json")
            .WithBody(JsonSerializer.Serialize(
                new { Id = 1, Name = "Alice", Email = "alice@example.com" }))
        );

    // Act — the client (or its resilience handler) should retry
    var user = await _client.GetUserAsync(1);

    // Assert
    user.ShouldNotBeNull();
    user.Name.ShouldBe("Alice");
}
```

**Example — Verifying Sent Request Headers**

```csharp
[Fact]
public async Task CreateUserAsync_SendsCorrectContentTypeAsync()
{
    // Arrange
    _server
        .Given(Request.Create().WithPath("/users").UsingPost())
        .RespondWith(Response.Create()
            .WithStatusCode(201)
            .WithHeader("Content-Type", "application/json")
            .WithBody(JsonSerializer.Serialize(
                new { Id = 42, Name = "Bob", Email = "bob@example.com" }))
        );

    // Act
    var user = await _client.CreateUserAsync(
        new CreateUserRequest("Bob", "bob@example.com"));

    // Assert — verify WireMock received the correct headers
    _server.LogEntries.ShouldContain(entry =>
        entry.RequestMessage.Headers!.ContainsKey("Content-Type") &&
        entry.RequestMessage.Headers["Content-Type"]
            .Any(v => v.Contains("application/json")));
}
```

**Minimum test count for Task 2**: 5 tests (4 required scenarios above + at least 1 optional scenario).

**Bonus (if time allows):** Configure resilience with `AddStandardResilienceHandler` and test retry behavior.

> **Hint**: Always reset the WireMock server between tests if they share an instance. You can call `_server.Reset()` in a setup method, or use `IAsyncLifetime` to create a fresh server per class. If individual test isolation is critical, create the server per-test instead.

> **Hint**: Use WireMock's `InScenario` / `WillSetStateTo` / `WhenStateIs` API to create stateful response sequences (e.g., first call fails, second succeeds).

## Grading

| Criteria |
|----------|
| Task 1 — API client implementation |
| Task 2 — WireMock tests |
| Proper exception hierarchy |
| WireMock server cleanup (IDisposable) |

## Submission

- Solution with `Lab9.Core` and `Lab9.Tests` projects
- WireMock server started/stopped per test class using `IAsyncLifetime`

## References

- [WireMock.Net Wiki](https://github.com/WireMock-Net/WireMock.Net/wiki)
- [WireMock.Net — Request Matching](https://github.com/WireMock-Net/WireMock.Net/wiki/Request-Matching)
- [WireMock.Net — Response Templating](https://github.com/WireMock-Net/WireMock.Net/wiki/Response-Templating)
- [WireMock.Net — Scenarios and Stateful Behavior](https://github.com/WireMock-Net/WireMock.Net/wiki/Scenarios-and-Stateful-Behavior)
- [Microsoft.Extensions.Http.Resilience Documentation](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience)
- [Building Resilient Cloud Services with .NET](https://devblogs.microsoft.com/dotnet/building-resilient-cloud-services-with-dotnet-8/)
- [IHttpClientFactory Guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory)
- [Typed HttpClient in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests#typed-clients)
- [Polly — Resilience and Transient Fault Handling](https://github.com/App-vNext/Polly)
- [xUnit v3 — IAsyncLifetime](https://xunit.net/docs/shared-context#async-lifetime)
- [Shouldly Assertion Library](https://docs.shouldly.org/)
- [System.Text.Json Overview](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview)
