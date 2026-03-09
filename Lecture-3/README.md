# Lecture 3: Integration Testing with WebApplicationFactory

## Learning Objectives

By the end of this lecture, students will be able to:

- Explain the difference between unit testing and integration testing
- Describe how ASP.NET Core test infrastructure works internally
- Use `WebApplicationFactory<T>` to spin up an in-memory test server
- Create custom `WebApplicationFactory` subclasses with service overrides
- Test HTTP endpoints (GET, POST, PUT, DELETE) with proper assertions
- Replace and reconfigure services in the DI container for testing
- Test middleware, authentication, and authorization pipelines
- Apply `IClassFixture<T>` and `IAsyncLifetime` for test lifecycle management
- Identify common pitfalls and apply best practices for integration tests

---

## 1. Unit Testing vs. Integration Testing: A Brief Recap

### 1.1 Where Unit Tests End

In Lecture 2, we learned that unit tests verify a single class or method **in isolation**, replacing all dependencies with test doubles. This is powerful — but it has a blind spot:

```
Unit Tests Verify:                    Unit Tests Do NOT Verify:
──────────────────                    ────────────────────────
✓ Business logic in isolation         ✗ HTTP routing and model binding
✓ Input validation rules              ✗ Middleware pipeline (auth, CORS, etc.)
✓ Edge cases and error handling       ✗ Dependency injection wiring
✓ Algorithm correctness               ✗ JSON serialization/deserialization
                                      ✗ Database queries (real SQL)
                                      ✗ Multiple components working together
```

### 1.2 What Integration Tests Cover

**Integration tests** verify that multiple components work **together** correctly. They test the seams between components — the places where misunderstandings and misconfiguration hide.

```
┌───────────────────────────────────────────────────────────┐
│                   Integration Test                        │
│                                                           │
│  HTTP Request ──► Routing ──► Middleware ──► Controller   │
│                                                  │        │
│                                             Service Layer │
│                                                  │        │
│  HTTP Response ◄── Serialization ◄── Result ◄────┘        │
│                                                           │
│  Verifies: status codes, headers, response body,          │
│            DI wiring, middleware behavior, routing         │
└───────────────────────────────────────────────────────────┘
```

### 1.3 The Testing Pyramid

```
            ╱╲
           ╱  ╲         E2E / UI Tests
          ╱    ╲        (few, slow, brittle)
         ╱──────╲
        ╱        ╲      Integration Tests        ◄── THIS LECTURE
       ╱          ╲     (moderate number, medium speed)
      ╱────────────╲
     ╱              ╲   Unit Tests
    ╱                ╲  (many, fast, isolated)
   ╱──────────────────╲
```

| Aspect | Unit Tests | Integration Tests | E2E Tests |
|---|---|---|---|
| **Scope** | Single class/method | Multiple components | Full system |
| **Speed** | Milliseconds | Milliseconds to seconds | Seconds to minutes |
| **Dependencies** | All mocked | Some real, some mocked | All real |
| **Confidence** | Logic is correct | Components work together | System works for users |
| **Maintenance** | Low | Medium | High |

> **Discussion (5 min):** You have 100% unit test coverage, but your API returns 500 errors in production. How is this possible? What could integration tests have caught?

---

## 2. ASP.NET Core Test Infrastructure Overview

### 2.1 The Problem: How Do You Test a Web API?

Testing an ASP.NET Core API without deploying it to a real server was historically painful. You had to:

1. Build and publish the application
2. Start it on a port
3. Send real HTTP requests
4. Tear it down after tests

This is slow, flaky, and hard to automate.

### 2.2 The Solution: `Microsoft.AspNetCore.Mvc.Testing`

ASP.NET Core provides a **built-in test infrastructure** via the `Microsoft.AspNetCore.Mvc.Testing` NuGet package. The centerpiece is `WebApplicationFactory<TEntryPoint>`.

```bash
dotnet add package Microsoft.AspNetCore.Mvc.Testing
```

### 2.3 Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│  Test Process (single process, no network)                  │
│                                                             │
│  ┌──────────────┐         ┌──────────────────────────────┐  │
│  │  Test Class   │         │  TestServer                  │  │
│  │              │  HTTP    │  ┌────────────────────────┐  │  │
│  │  HttpClient ─┼────────►│  │  ASP.NET Core Pipeline  │  │  │
│  │              │ in-mem   │  │  ┌──────────────────┐  │  │  │
│  │  Assertions  │◄────────┼  │  │  Middleware       │  │  │  │
│  │              │         │  │  │  ┌──────────────┐ │  │  │  │
│  └──────────────┘         │  │  │  │  Controllers │ │  │  │  │
│                           │  │  │  │  ┌────────┐ │ │  │  │  │
│                           │  │  │  │  │Services│ │ │  │  │  │
│                           │  │  │  │  └────────┘ │ │  │  │  │
│                           │  │  │  └──────────────┘ │  │  │  │
│                           │  │  └──────────────────┘  │  │  │
│                           │  └────────────────────────┘  │  │
│                           └──────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

Key points:

- **No real HTTP traffic** — requests go through an in-memory channel
- **No open ports** — no port conflicts, no firewall issues
- **Same pipeline** — middleware, routing, model binding, filters all execute
- **Fast** — no network latency, no process startup overhead
- **Single process** — test and server run in the same process

---

## 3. WebApplicationFactory Deep Dive

### 3.1 What Happens When You Create a WebApplicationFactory?

`WebApplicationFactory<TEntryPoint>` does the following internally:

```
1. Locates the application's entry point assembly
   └── Uses TEntryPoint to find the project's content root

2. Builds the host
   └── Calls Program.cs / Startup.cs to configure services and middleware
   └── BUT uses TestServer instead of Kestrel

3. Creates a TestServer
   └── In-memory server that processes requests without TCP/IP
   └── Implements IServer interface

4. Provides HttpClient
   └── CreateClient() returns an HttpClient configured to talk to TestServer
   └── Base address is set to http://localhost by default

5. Manages lifecycle
   └── Implements IDisposable / IAsyncDisposable
   └── Shuts down the host and server on disposal
```

### 3.2 The Simplest Integration Test

Let us start with a minimal example. Assume we have a simple ASP.NET Core API:

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<ITaskService, TaskService>();

var app = builder.Build();

app.MapControllers();
app.Run();

// Make Program class accessible to test project
public partial class Program { }
```

```csharp
// Controllers/TasksController.cs
using Microsoft.AspNetCore.Mvc;

namespace TaskApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController(ITaskService taskService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskItem>>> GetAllAsync()
    {
        var tasks = await taskService.GetAllAsync();
        return Ok(tasks);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TaskItem>> GetByIdAsync(int id)
    {
        var task = await taskService.GetByIdAsync(id);
        if (task is null) return NotFound();
        return Ok(task);
    }

    [HttpPost]
    public async Task<ActionResult<TaskItem>> CreateAsync(CreateTaskRequest request)
    {
        var task = await taskService.CreateAsync(request);
        return CreatedAtAction(nameof(GetByIdAsync), new { id = task.Id }, task);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> UpdateAsync(int id, UpdateTaskRequest request)
    {
        var updated = await taskService.UpdateAsync(id, request);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteAsync(int id)
    {
        var deleted = await taskService.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
```

```csharp
// Models
namespace TaskApi;

public record TaskItem(int Id, string Title, string? Description, bool IsCompleted);
public record CreateTaskRequest(string Title, string? Description);
public record UpdateTaskRequest(string Title, string? Description, bool IsCompleted);
```

Now the integration test:

```csharp
// TaskApi.Tests/TasksControllerTests.cs
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace TaskApi.Tests;

public class TasksControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public TasksControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_ReturnsOkStatusCodeAsync()
    {
        // Act
        var response = await _client.GetAsync("/api/tasks");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_NonExistentTask_ReturnsNotFoundAsync()
    {
        // Act
        var response = await _client.GetAsync("/api/tasks/99999");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
```

> **Key:** The `public partial class Program { }` line at the bottom of `Program.cs` is required to make the entry point class accessible to the test project. Without it, `WebApplicationFactory<Program>` cannot find the application.

### 3.3 TestServer Internals

The `TestServer` replaces Kestrel (the production HTTP server) with an in-memory transport:

```
Production:
  HttpClient ──[TCP/IP]──► Kestrel ──► ASP.NET Core Pipeline

Testing:
  HttpClient ──[in-memory]──► TestServer ──► ASP.NET Core Pipeline
```

The `TestServer` creates a special `HttpMessageHandler` that short-circuits the network stack. When you call `factory.CreateClient()`, the returned `HttpClient` uses this handler internally.

```csharp
// What CreateClient() does internally (simplified):
var handler = _testServer.CreateHandler();     // in-memory handler
var client = new HttpClient(handler)
{
    BaseAddress = new Uri("http://localhost")   // default base address
};
```

This means:
- Requests never leave the process
- DNS resolution is not needed
- No SSL/TLS negotiation
- No port binding or conflicts
- Extremely fast round-trips

> **Discussion (5 min):** What are the trade-offs of in-memory testing? What issues might you miss by not using real HTTP? (Hint: think about TLS configuration, HTTP/2, load balancer behavior, CORS with a real browser.)

---

## 4. Custom WebApplicationFactory

### 4.1 Why Customize?

The default `WebApplicationFactory<Program>` uses your real `Program.cs` configuration. In production, your API might:
- Connect to a real SQL Server database
- Call external payment APIs
- Send emails via SMTP
- Use Azure Key Vault for secrets

You do not want any of that in tests. A custom factory lets you **replace** specific services while keeping the rest of the real pipeline.

### 4.2 Creating a Custom Factory

```csharp
// TaskApi.Tests/CustomWebApplicationFactory.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace TaskApi.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real repository registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ITaskRepository));

            if (descriptor is not null)
                services.Remove(descriptor);

            // Add an in-memory fake repository
            services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();
        });

        builder.UseEnvironment("Testing");
    }
}
```

### 4.3 A Reusable In-Memory Repository

```csharp
// TaskApi.Tests/Fakes/InMemoryTaskRepository.cs
using System.Collections.Concurrent;

namespace TaskApi.Tests.Fakes;

public class InMemoryTaskRepository : ITaskRepository
{
    private readonly ConcurrentDictionary<int, TaskItem> _tasks = new();
    private int _nextId = 1;

    public Task<IEnumerable<TaskItem>> GetAllAsync()
        => Task.FromResult<IEnumerable<TaskItem>>(_tasks.Values.ToList());

    public Task<TaskItem?> GetByIdAsync(int id)
        => Task.FromResult(_tasks.GetValueOrDefault(id));

    public Task<TaskItem> CreateAsync(TaskItem task)
    {
        var id = Interlocked.Increment(ref _nextId);
        var newTask = task with { Id = id };
        _tasks[id] = newTask;
        return Task.FromResult(newTask);
    }

    public Task<bool> UpdateAsync(TaskItem task)
    {
        if (!_tasks.ContainsKey(task.Id)) return Task.FromResult(false);
        _tasks[task.Id] = task;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int id)
        => Task.FromResult(_tasks.TryRemove(id, out _));
}
```

### 4.4 Using the Custom Factory

```csharp
public class TasksControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TasksControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // Tests now use InMemoryTaskRepository instead of the real database
}
```

### 4.5 The Service Replacement Pattern

The general pattern for replacing services follows these steps:

```
1. Find the existing service descriptor
2. Remove it from the service collection
3. Register your test replacement

services.RemoveAll<IMyService>();         // Step 1+2 combined
services.AddSingleton<IMyService, FakeMyService>();  // Step 3
```

Using `RemoveAll<T>()` from `Microsoft.Extensions.DependencyInjection.Extensions` is cleaner:

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;

protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.ConfigureServices(services =>
    {
        services.RemoveAll<ITaskRepository>();
        services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();

        services.RemoveAll<IEmailService>();
        services.AddSingleton<IEmailService, FakeEmailService>();
    });
}
```

> **Discussion (5 min):** When would you use a fake (like `InMemoryTaskRepository`) vs. an NSubstitute mock inside the factory? What are the trade-offs?

---

## 5. Testing HTTP Endpoints

### 5.1 Testing GET Endpoints

```csharp
public class TasksControllerGetTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public TasksControllerGetTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_ReturnsOkWithJsonContentTypeAsync()
    {
        // Act
        var response = await _client.GetAsync("/api/tasks");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .ShouldBe("application/json");
    }

    [Fact]
    public async Task GetAll_ReturnsEmptyArray_WhenNoTasksExistAsync()
    {
        // Act
        var tasks = await _client.GetFromJsonAsync<TaskItem[]>("/api/tasks");

        // Assert
        tasks.ShouldNotBeNull();
        tasks.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetById_ExistingTask_ReturnsTaskAsync()
    {
        // Arrange — create a task first
        var createRequest = new CreateTaskRequest("Test Task", "Description");
        var createResponse = await _client.PostAsJsonAsync("/api/tasks", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TaskItem>();

        // Act
        var response = await _client.GetAsync($"/api/tasks/{created!.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var task = await response.Content.ReadFromJsonAsync<TaskItem>();
        task.ShouldNotBeNull();
        task.Title.ShouldBe("Test Task");
        task.Description.ShouldBe("Description");
        task.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task GetById_NonExistentId_Returns404Async()
    {
        // Act
        var response = await _client.GetAsync("/api/tasks/99999");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
```

### 5.2 Testing POST Endpoints

```csharp
public class TasksControllerPostTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TasksControllerPostTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_ValidRequest_Returns201WithLocationHeaderAsync()
    {
        // Arrange
        var request = new CreateTaskRequest("New Task", "Some description");

        // Act
        var response = await _client.PostAsJsonAsync("/api/tasks", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Verify Location header points to the new resource
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.ToString().ShouldContain("/api/tasks/");

        // Verify response body
        var task = await response.Content.ReadFromJsonAsync<TaskItem>();
        task.ShouldNotBeNull();
        task.Id.ShouldBeGreaterThan(0);
        task.Title.ShouldBe("New Task");
        task.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task Create_MissingTitle_Returns400Async()
    {
        // Arrange — send an empty object (Title is required)
        var request = new { };

        // Act
        var response = await _client.PostAsJsonAsync("/api/tasks", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_EmptyBody_Returns400Async()
    {
        // Arrange
        var content = new StringContent("", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/tasks", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
```

### 5.3 Testing PUT Endpoints

```csharp
[Fact]
public async Task Update_ExistingTask_Returns204Async()
{
    // Arrange — create a task first
    var createRequest = new CreateTaskRequest("Original Title", null);
    var createResponse = await _client.PostAsJsonAsync("/api/tasks", createRequest);
    var created = await createResponse.Content.ReadFromJsonAsync<TaskItem>();

    var updateRequest = new UpdateTaskRequest(
        "Updated Title", "Added description", IsCompleted: true);

    // Act
    var response = await _client.PutAsJsonAsync(
        $"/api/tasks/{created!.Id}", updateRequest);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    // Verify the update persisted
    var updated = await _client.GetFromJsonAsync<TaskItem>(
        $"/api/tasks/{created.Id}");
    updated!.Title.ShouldBe("Updated Title");
    updated.Description.ShouldBe("Added description");
    updated.IsCompleted.ShouldBeTrue();
}

[Fact]
public async Task Update_NonExistentTask_Returns404Async()
{
    // Arrange
    var updateRequest = new UpdateTaskRequest("Title", null, false);

    // Act
    var response = await _client.PutAsJsonAsync("/api/tasks/99999", updateRequest);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
}
```

### 5.4 Testing DELETE Endpoints

```csharp
[Fact]
public async Task Delete_ExistingTask_Returns204Async()
{
    // Arrange — create a task first
    var createResponse = await _client.PostAsJsonAsync(
        "/api/tasks", new CreateTaskRequest("To Delete", null));
    var created = await createResponse.Content.ReadFromJsonAsync<TaskItem>();

    // Act
    var response = await _client.DeleteAsync($"/api/tasks/{created!.Id}");

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    // Verify it is gone
    var getResponse = await _client.GetAsync($"/api/tasks/{created.Id}");
    getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
}

[Fact]
public async Task Delete_NonExistentTask_Returns404Async()
{
    // Act
    var response = await _client.DeleteAsync("/api/tasks/99999");

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
}
```

### 5.5 HTTP Status Code Reference

| Status Code | Meaning | When to Use |
|---|---|---|
| `200 OK` | Success with body | GET returning data |
| `201 Created` | Resource created | POST with Location header |
| `204 No Content` | Success without body | PUT, DELETE success |
| `400 Bad Request` | Invalid input | Validation failures |
| `401 Unauthorized` | Not authenticated | Missing/invalid credentials |
| `403 Forbidden` | Not authorized | Authenticated but insufficient permissions |
| `404 Not Found` | Resource not found | GET/PUT/DELETE with invalid ID |
| `409 Conflict` | State conflict | Duplicate creation, version mismatch |
| `500 Internal Server Error` | Server error | Unhandled exceptions |

---

## 6. Working with HttpClient in Tests

### 6.1 JSON Serialization and Deserialization

The `System.Net.Http.Json` namespace provides extension methods that simplify JSON handling:

```csharp
using System.Net.Http.Json;

// POST with JSON body
var response = await _client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest("Title", null));

// Read response body as JSON
var task = await response.Content.ReadFromJsonAsync<TaskItem>();

// GET and deserialize in one call
var tasks = await _client.GetFromJsonAsync<List<TaskItem>>("/api/tasks");

// Custom serialization options
var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};
var result = await response.Content.ReadFromJsonAsync<TaskItem>(options);
```

### 6.2 Setting Custom Headers

```csharp
[Fact]
public async Task GetAll_WithAcceptHeader_ReturnsJsonAsync()
{
    // Arrange
    var request = new HttpRequestMessage(HttpMethod.Get, "/api/tasks");
    request.Headers.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));

    // Act
    var response = await _client.SendAsync(request);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    response.Content.Headers.ContentType?.MediaType
        .ShouldBe("application/json");
}
```

### 6.3 Sending Form Data and Custom Content

```csharp
// String content with explicit media type
var jsonContent = new StringContent(
    """{"title": "My Task", "description": null}""",
    System.Text.Encoding.UTF8,
    "application/json");
var response = await _client.PostAsync("/api/tasks", jsonContent);

// Form URL-encoded content
var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["username"] = "admin",
    ["password"] = "secret"
});
var loginResponse = await _client.PostAsync("/api/auth/login", formContent);
```

### 6.4 Reading Response Details

```csharp
[Fact]
public async Task Create_ValidRequest_ReturnsExpectedHeadersAsync()
{
    // Arrange
    var request = new CreateTaskRequest("Header Test", null);

    // Act
    var response = await _client.PostAsJsonAsync("/api/tasks", request);

    // Assert — status code
    response.StatusCode.ShouldBe(HttpStatusCode.Created);

    // Assert — headers
    response.Headers.Location.ShouldNotBeNull();

    // Assert — response body as string (useful for debugging)
    var body = await response.Content.ReadAsStringAsync();
    body.ShouldContain("Header Test");

    // Assert — response body as typed object
    var task = await response.Content.ReadFromJsonAsync<TaskItem>();
    task!.Title.ShouldBe("Header Test");
}
```

---

## 7. Test Lifecycle: IClassFixture and IAsyncLifetime

### 7.1 Understanding xUnit v3 Test Lifecycle

xUnit v3 creates a **new instance** of the test class for **every test method**. This provides natural test isolation but can be expensive if setup is costly:

```
Test method 1:  new TestClass() → Run Test → Dispose
Test method 2:  new TestClass() → Run Test → Dispose
Test method 3:  new TestClass() → Run Test → Dispose
                    ▲
                    │
           New factory + TestServer for EVERY test?
           That would be very slow!
```

### 7.2 IClassFixture — Share Expensive Resources

`IClassFixture<T>` tells xUnit to create a **single instance** of the fixture type and share it across all tests in the class:

```csharp
public class TasksControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    // Constructor receives the SHARED factory instance
    public TasksControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // All test methods share the same factory (and TestServer)
    // But each test gets its own HttpClient
}
```

```
IClassFixture lifecycle:

new CustomWebApplicationFactory()      ← Created ONCE before any test
    │
    ├── new TestClass(factory) → Test 1 → Dispose TestClass
    ├── new TestClass(factory) → Test 2 → Dispose TestClass
    ├── new TestClass(factory) → Test 3 → Dispose TestClass
    │
Dispose factory                        ← Disposed ONCE after all tests
```

### 7.3 IAsyncLifetime — Async Setup and Teardown

When test setup or teardown requires async operations (e.g., seeding a database, cleaning up data), implement `IAsyncLifetime`:

```csharp
public class TasksControllerTests
    : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public TasksControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // Called BEFORE each test method
    public async Task InitializeAsync()
    {
        // Seed test data
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        await repo.CreateAsync(new TaskItem(0, "Seeded Task", "Description", false));
    }

    // Called AFTER each test method
    public async Task DisposeAsync()
    {
        // Clean up test data
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        await repo.ClearAllAsync(); // hypothetical cleanup method
    }

    [Fact]
    public async Task GetAll_WithSeededData_ReturnsSeededTaskAsync()
    {
        // The seeded task is available here
        var tasks = await _client.GetFromJsonAsync<TaskItem[]>("/api/tasks");
        tasks.ShouldNotBeNull();
        tasks.Length.ShouldBeGreaterThanOrEqualTo(1);
    }
}
```

### 7.4 IClassFixture vs. IAsyncLifetime Comparison

```
┌──────────────────────────┬──────────────────────────┐
│    IClassFixture<T>      │     IAsyncLifetime        │
├──────────────────────────┼──────────────────────────┤
│ Shares one instance of T │ Per-test setup/teardown   │
│ across ALL tests in      │ InitializeAsync() before  │
│ the class                │ each test                 │
│                          │ DisposeAsync() after      │
│ Good for: expensive      │ each test                 │
│ resources (factory,      │                           │
│ TestServer)              │ Good for: seeding data,   │
│                          │ resetting state           │
│ Scope: class-level       │ Scope: test-level         │
└──────────────────────────┴──────────────────────────┘
```

They are often used **together**: `IClassFixture` shares the factory, while `IAsyncLifetime` handles per-test data setup.

> **Discussion (5 min):** What happens if two tests modify the same shared data (e.g., both create a task with ID 1)? How can you prevent test interference?

---

## 8. Test Isolation Strategies

### 8.1 The Shared State Problem

When tests share a `WebApplicationFactory` (and thus a server and its services), they can interfere with each other if services maintain state:

```
Test A: Creates task "Task A" ─────────► Repository now has [Task A]
Test B: Calls GetAll ─────────────────► Gets [Task A] ← UNEXPECTED!
Test C: Creates task "Task C" ─────────► Repository now has [Task A, Task C]
Test D: Asserts count == 1 ──────────► FAILS! Count is 2
```

### 8.2 Strategy 1: Fresh Client per Test

Create a new `HttpClient` for each test with a unique factory configuration:

```csharp
[Fact]
public async Task GetAll_ReturnsOnlyCurrentTestDataAsync()
{
    // Create a factory with a fresh, empty repository
    await using var factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITaskRepository>();
                services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();
            });
        });

    var client = factory.CreateClient();

    // This test has its own isolated repository
    var tasks = await client.GetFromJsonAsync<TaskItem[]>("/api/tasks");
    tasks.ShouldNotBeNull();
    tasks.ShouldBeEmpty();
}
```

### 8.3 Strategy 2: Reset State Between Tests

Use `IAsyncLifetime` to reset shared state:

```csharp
public class TasksControllerTests
    : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TasksControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync()
    {
        // Reset the in-memory repository before each test
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        if (repo is InMemoryTaskRepository inMemRepo)
        {
            inMemRepo.Clear();
        }
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
```

### 8.4 Strategy 3: Unique Data per Test

Design tests so they do not conflict — each test creates and verifies its own unique data:

```csharp
[Fact]
public async Task Create_UniqueTask_CanBeRetrievedByIdAsync()
{
    // Arrange — use a unique identifier to avoid conflicts
    var uniqueTitle = $"Task-{Guid.NewGuid()}";
    var request = new CreateTaskRequest(uniqueTitle, "Isolation test");

    // Act
    var createResponse = await _client.PostAsJsonAsync("/api/tasks", request);
    var created = await createResponse.Content.ReadFromJsonAsync<TaskItem>();

    // Assert — retrieve by ID (not affected by other tests)
    var task = await _client.GetFromJsonAsync<TaskItem>(
        $"/api/tasks/{created!.Id}");
    task!.Title.ShouldBe(uniqueTitle);
}
```

### 8.5 Comparison of Isolation Strategies

| Strategy | Pros | Cons |
|---|---|---|
| **Fresh factory per test** | Complete isolation | Slow — new server per test |
| **Reset state between tests** | Fast, shared server | Must remember to reset everything |
| **Unique data per test** | Fast, no cleanup needed | Can accumulate stale data |
| **Transaction rollback** | Clean, database-aware | Only works with real databases |

---

## 9. Testing Middleware

### 9.1 What is Middleware?

Middleware components form a pipeline that processes every HTTP request and response:

```
Request ──► Middleware 1 ──► Middleware 2 ──► Middleware 3 ──► Endpoint
            (Logging)        (Auth)          (Error Handler)
Response ◄── Middleware 1 ◄── Middleware 2 ◄── Middleware 3 ◄── Endpoint
```

Common middleware: exception handling, authentication, authorization, CORS, rate limiting, request logging.

### 9.2 Testing Exception Handling Middleware

Consider a global exception handler:

```csharp
// Middleware/GlobalExceptionMiddleware.cs
public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning(ex, "Resource not found");
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 404,
                Title = "Not Found",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 500,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred."
            });
        }
    }
}
```

Test that the middleware transforms exceptions into proper HTTP responses:

```csharp
[Fact]
public async Task Middleware_UnhandledException_Returns500WithProblemDetailsAsync()
{
    // Arrange — configure a factory where the service throws
    await using var factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITaskService>();
                services.AddScoped<ITaskService>(_ =>
                {
                    var mock = Substitute.For<ITaskService>();
                    mock.GetAllAsync()
                        .ThrowsAsync(new InvalidOperationException("Something broke"));
                    return mock;
                });
            });
        });

    var client = factory.CreateClient();

    // Act
    var response = await client.GetAsync("/api/tasks");

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);

    var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
    problem.ShouldNotBeNull();
    problem.Status.ShouldBe(500);
    problem.Title.ShouldBe("Internal Server Error");
}
```

### 9.3 Testing CORS Middleware

```csharp
[Fact]
public async Task Options_CorsPreflightRequest_ReturnsCorrectHeadersAsync()
{
    // Arrange
    var request = new HttpRequestMessage(HttpMethod.Options, "/api/tasks");
    request.Headers.Add("Origin", "https://example.com");
    request.Headers.Add("Access-Control-Request-Method", "POST");
    request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

    // Act
    var response = await _client.SendAsync(request);

    // Assert
    response.Headers.TryGetValues(
        "Access-Control-Allow-Origin", out var origins);
    origins.ShouldNotBeNull();
    origins.ShouldContain("https://example.com");
}
```

### 9.4 Testing Request Logging Middleware

You can capture logs in tests by registering a custom log provider:

```csharp
public class TestLoggerProvider : ILoggerProvider
{
    public ConcurrentBag<string> LogMessages { get; } = new();

    public ILogger CreateLogger(string categoryName)
        => new TestLogger(LogMessages);

    public void Dispose() { }

    private class TestLogger(ConcurrentBag<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            messages.Add($"[{logLevel}] {formatter(state, exception)}");
        }
    }
}
```

```csharp
[Fact]
public async Task Request_IsLoggedByMiddlewareAsync()
{
    // Arrange
    var logProvider = new TestLoggerProvider();

    await using var factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(logProvider);
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITaskRepository>();
                services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();
            });
        });

    var client = factory.CreateClient();

    // Act
    await client.GetAsync("/api/tasks");

    // Assert
    logProvider.LogMessages.ShouldContain(
        msg => msg.Contains("tasks", StringComparison.OrdinalIgnoreCase));
}
```

---

## 10. Testing Authentication and Authorization

### 10.1 The Challenge

Production APIs often require authentication (JWT, cookies, API keys). In integration tests, you need to either:

1. **Bypass authentication** entirely (for testing non-auth logic)
2. **Simulate authentication** with fake tokens or claims
3. **Test the auth pipeline** itself

### 10.2 Strategy 1: Fake Authentication Handler

ASP.NET Core allows you to register a custom authentication handler that always succeeds:

```csharp
// TaskApi.Tests/Auth/TestAuthHandler.cs
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TaskApi.Tests.Auth;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";
    public const string DefaultUserId = "test-user-id";
    public const string DefaultUserName = "testuser@example.com";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check if the test wants to simulate an unauthenticated request
        if (Context.Request.Headers.ContainsKey("X-Test-Unauthenticated"))
        {
            return Task.FromResult(AuthenticateResult.Fail("Unauthenticated"));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, DefaultUserId),
            new(ClaimTypes.Name, DefaultUserName),
            new(ClaimTypes.Role, "User")
        };

        // Allow tests to add custom roles via header
        if (Context.Request.Headers.TryGetValue("X-Test-Role", out var role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

### 10.3 Registering the Test Auth Handler

```csharp
public class AuthenticatedWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove real authentication
            services.RemoveAll<IAuthenticationHandler>();

            // Add test authentication
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, options => { });

            // Replace other services as needed
            services.RemoveAll<ITaskRepository>();
            services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();
        });

        builder.UseEnvironment("Testing");
    }
}
```

### 10.4 Testing Authenticated Endpoints

```csharp
public class AuthenticatedTasksControllerTests
    : IClassFixture<AuthenticatedWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthenticatedTasksControllerTests(
        AuthenticatedWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_AuthenticatedUser_Returns200Async()
    {
        // Act — TestAuthHandler automatically authenticates
        var response = await _client.GetAsync("/api/tasks");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_UnauthenticatedUser_Returns401Async()
    {
        // Arrange — signal the test handler to reject
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/tasks");
        request.Headers.Add("X-Test-Unauthenticated", "true");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteTask_AdminRole_Returns204Async()
    {
        // Arrange — create a task, then delete as admin
        var createResponse = await _client.PostAsJsonAsync(
            "/api/tasks", new CreateTaskRequest("Admin Task", null));
        var task = await createResponse.Content.ReadFromJsonAsync<TaskItem>();

        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/api/tasks/{task!.Id}");
        request.Headers.Add("X-Test-Role", "Admin");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteTask_RegularUser_Returns403Async()
    {
        // Arrange — the endpoint requires Admin role
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/tasks/1");
        // No X-Test-Role header — default is "User" role only

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
```

### 10.5 Authorization Scenarios to Test

| Scenario | Expected Status | Test Description |
|---|---|---|
| No credentials | 401 Unauthorized | Anonymous access to protected endpoint |
| Valid credentials, no permission | 403 Forbidden | User without required role |
| Valid credentials, has permission | 200/201/204 | Authorized access |
| Expired token | 401 Unauthorized | Stale authentication |
| Admin accessing user resource | 200 | Elevated privileges |
| User accessing another user's data | 403 Forbidden | Resource-level authorization |

> **Discussion (10 min):** In production, your API uses JWT tokens from an identity provider (e.g., Auth0, Azure AD). Why is it acceptable to bypass real JWT validation in integration tests? When would you want to test with real JWTs?

---

## 11. Advanced Configuration with WithWebHostBuilder

### 11.1 Inline Configuration Without a Custom Factory

For one-off customizations, use `WithWebHostBuilder` directly on a `WebApplicationFactory`:

```csharp
[Fact]
public async Task GetAll_WithCustomConfiguration_UsesTestSettingsAsync()
{
    // Arrange
    await using var factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            // Override configuration
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Features:EnableCaching"] = "false",
                    ["ExternalApi:BaseUrl"] = "http://fake-api.local",
                    ["Logging:LogLevel:Default"] = "Warning"
                });
            });

            // Override services
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITaskRepository>();
                services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();
            });
        });

    var client = factory.CreateClient();

    // Act & Assert
    var response = await client.GetAsync("/api/tasks");
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
}
```

### 11.2 Accessing Services from Tests

You can resolve services from the factory's DI container to inspect state or seed data:

```csharp
[Fact]
public async Task Create_ValidTask_IsSavedToRepositoryAsync()
{
    // Arrange
    var request = new CreateTaskRequest("DI Test", "Checking DI");

    // Act
    await _client.PostAsJsonAsync("/api/tasks", request);

    // Assert — resolve the repository and verify directly
    using var scope = _factory.Services.CreateScope();
    var repo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
    var allTasks = await repo.GetAllAsync();

    allTasks.ShouldContain(t => t.Title == "DI Test");
}
```

### 11.3 Using NSubstitute Inside WebApplicationFactory

You can inject NSubstitute mocks into the DI container for fine-grained control:

```csharp
[Fact]
public async Task Create_ValidTask_CallsNotificationServiceAsync()
{
    // Arrange
    var notificationService = Substitute.For<INotificationService>();

    await using var factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITaskRepository>();
                services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();

                services.RemoveAll<INotificationService>();
                services.AddSingleton(notificationService);
            });
        });

    var client = factory.CreateClient();

    // Act
    await client.PostAsJsonAsync("/api/tasks",
        new CreateTaskRequest("Notify Test", null));

    // Assert — verify the mock was called
    await notificationService.Received(1)
        .NotifyTaskCreatedAsync(Arg.Is<string>(t => t == "Notify Test"));
}
```

---

## 12. Common Pitfalls and Best Practices

### 12.1 Common Pitfalls

#### Pitfall 1: Forgetting `public partial class Program`

```csharp
// Without this, WebApplicationFactory<Program> cannot find your application
// Add to the bottom of Program.cs:
public partial class Program { }
```

**Symptom:** Compile error: `'Program' is inaccessible due to its protection level`

#### Pitfall 2: Not Disposing the Factory

```csharp
// BAD — factory is never disposed, TestServer leaks
var factory = new WebApplicationFactory<Program>();
var client = factory.CreateClient();
// ... tests run, but server is never shut down

// GOOD — use IClassFixture (xUnit manages disposal)
public class MyTests : IClassFixture<WebApplicationFactory<Program>> { }

// GOOD — use await using for inline factories
await using var factory = new WebApplicationFactory<Program>();
```

#### Pitfall 3: Sharing Mutable State Across Tests

```csharp
// BAD — in-memory repository accumulates data across tests
public class Tests : IClassFixture<CustomFactory>
{
    [Fact] public async Task Test1_CreatesData() { /* adds task */ }
    [Fact] public async Task Test2_AssumesEmptyState() { /* FAILS! */ }
}

// GOOD — reset state or use unique data
public class Tests : IClassFixture<CustomFactory>, IAsyncLifetime
{
    public Task InitializeAsync() { /* clear repository */ }
}
```

#### Pitfall 4: Testing Against Real External Services

```csharp
// BAD — test calls a real payment API
// Slow, flaky, may charge real money!

// GOOD — replace with a fake or mock
services.RemoveAll<IPaymentGateway>();
services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
```

#### Pitfall 5: Ignoring the Service Lifetime Mismatch

```csharp
// BAD — replacing a Scoped service with Singleton can cause issues
services.RemoveAll<ITaskRepository>();           // was Scoped
services.AddSingleton<ITaskRepository>(mock);    // now Singleton

// The mock will be shared across all requests — which may or may not be
// what you want. If the mock has mutable state, be careful.

// GOOD — match the lifetime or be explicit about the choice
services.RemoveAll<ITaskRepository>();
services.AddScoped<ITaskRepository>(_ => CreateFreshMock());
```

#### Pitfall 6: Hardcoding Ports or URLs

```csharp
// BAD — assumes a specific port
var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };

// GOOD — let the factory configure the base address
var client = factory.CreateClient(); // base address is set automatically
```

### 12.2 Best Practices Summary

```
Best Practices for Integration Testing with WebApplicationFactory
─────────────────────────────────────────────────────────────────

1. USE IClassFixture to share the factory across tests in a class
   └── Avoids creating a new TestServer per test (expensive)

2. REPLACE external dependencies (databases, APIs, email)
   └── Use fakes for simple behavior, mocks for verification

3. KEEP tests independent
   └── Each test should pass when run alone or with other tests

4. TEST the HTTP contract, not internal implementation
   └── Assert status codes, headers, and response body shapes

5. USE meaningful test names
   └── GetById_NonExistentTask_Returns404Async

6. CLEAN UP state between tests
   └── IAsyncLifetime or unique data per test

7. TEST both happy paths AND error paths
   └── 400, 401, 403, 404, 409, 500

8. VERIFY response content, not just status codes
   └── A 200 response with wrong data is still a bug

9. USE System.Net.Http.Json for clean serialization
   └── PostAsJsonAsync, GetFromJsonAsync, ReadFromJsonAsync

10. MATCH the async naming convention
    └── Async suffix for all async test methods
```

---

## 13. Putting It All Together: Complete Example

### 13.1 Project Structure

```
TaskApi/
├── Controllers/
│   └── TasksController.cs
├── Models/
│   ├── TaskItem.cs
│   ├── CreateTaskRequest.cs
│   └── UpdateTaskRequest.cs
├── Services/
│   ├── ITaskService.cs
│   └── TaskService.cs
├── Repositories/
│   ├── ITaskRepository.cs
│   └── TaskRepository.cs
├── Middleware/
│   └── GlobalExceptionMiddleware.cs
└── Program.cs

TaskApi.Tests/
├── Factories/
│   └── CustomWebApplicationFactory.cs
├── Fakes/
│   └── InMemoryTaskRepository.cs
├── Auth/
│   └── TestAuthHandler.cs
├── Controllers/
│   ├── TasksController_GetTests.cs
│   ├── TasksController_PostTests.cs
│   ├── TasksController_PutTests.cs
│   └── TasksController_DeleteTests.cs
└── Middleware/
    └── GlobalExceptionMiddlewareTests.cs
```

### 13.2 Complete Test Class

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;

namespace TaskApi.Tests.Controllers;

public class TasksControllerIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TasksControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync()
    {
        // Reset the repository before each test
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        if (repo is InMemoryTaskRepository inMemRepo)
            inMemRepo.Clear();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ──────────────────────────────────────────────

    private async Task<TaskItem> CreateTestTaskAsync(
        string title = "Test Task", string? description = null)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/tasks", new CreateTaskRequest(title, description));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskItem>())!;
    }

    // ── GET /api/tasks ──────────────────────────────────────

    [Fact]
    public async Task GetAll_EmptyRepository_ReturnsEmptyArrayAsync()
    {
        var tasks = await _client.GetFromJsonAsync<TaskItem[]>("/api/tasks");

        tasks.ShouldNotBeNull();
        tasks.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithTasks_ReturnsAllTasksAsync()
    {
        await CreateTestTaskAsync("Task 1");
        await CreateTestTaskAsync("Task 2");

        var tasks = await _client.GetFromJsonAsync<TaskItem[]>("/api/tasks");

        tasks.ShouldNotBeNull();
        tasks.Length.ShouldBe(2);
    }

    // ── GET /api/tasks/{id} ─────────────────────────────────

    [Fact]
    public async Task GetById_ExistingTask_ReturnsTaskWithCorrectDataAsync()
    {
        var created = await CreateTestTaskAsync("Specific Task", "Details");

        var task = await _client.GetFromJsonAsync<TaskItem>(
            $"/api/tasks/{created.Id}");

        task.ShouldNotBeNull();
        task.Id.ShouldBe(created.Id);
        task.Title.ShouldBe("Specific Task");
        task.Description.ShouldBe("Details");
        task.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task GetById_NonExistentTask_Returns404Async()
    {
        var response = await _client.GetAsync("/api/tasks/99999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── POST /api/tasks ─────────────────────────────────────

    [Fact]
    public async Task Create_ValidTask_Returns201WithLocationAsync()
    {
        var request = new CreateTaskRequest("New Task", "Description");

        var response = await _client.PostAsJsonAsync("/api/tasks", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();

        var task = await response.Content.ReadFromJsonAsync<TaskItem>();
        task.ShouldNotBeNull();
        task.Title.ShouldBe("New Task");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_InvalidTitle_Returns400Async(string title)
    {
        var request = new CreateTaskRequest(title, null);

        var response = await _client.PostAsJsonAsync("/api/tasks", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── PUT /api/tasks/{id} ─────────────────────────────────

    [Fact]
    public async Task Update_ExistingTask_Returns204AndPersistsChangesAsync()
    {
        var created = await CreateTestTaskAsync("Before Update");
        var updateRequest = new UpdateTaskRequest(
            "After Update", "New Description", true);

        var response = await _client.PutAsJsonAsync(
            $"/api/tasks/{created.Id}", updateRequest);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify changes persisted
        var updated = await _client.GetFromJsonAsync<TaskItem>(
            $"/api/tasks/{created.Id}");
        updated!.Title.ShouldBe("After Update");
        updated.Description.ShouldBe("New Description");
        updated.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_NonExistentTask_Returns404Async()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/tasks/99999",
            new UpdateTaskRequest("Title", null, false));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/tasks/{id} ──────────────────────────────

    [Fact]
    public async Task Delete_ExistingTask_Returns204AndRemovesTaskAsync()
    {
        var created = await CreateTestTaskAsync("To Delete");

        var response = await _client.DeleteAsync($"/api/tasks/{created.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/tasks/{created.Id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_NonExistentTask_Returns404Async()
    {
        var response = await _client.DeleteAsync("/api/tasks/99999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── Content Negotiation ─────────────────────────────────

    [Fact]
    public async Task GetAll_ResponseIsJsonAsync()
    {
        var response = await _client.GetAsync("/api/tasks");

        response.Content.Headers.ContentType?.MediaType
            .ShouldBe("application/json");
    }
}
```

---

## 14. When to Use Integration Tests vs. Unit Tests

### 14.1 Decision Guide

```
Ask yourself:
                                          ┌──────────────────────┐
"Am I testing business logic              │                      │
 with no I/O or infrastructure?"──YES────►│   Write a UNIT TEST  │
        │                                 │                      │
        NO                                └──────────────────────┘
        │
        ▼
"Am I testing how components              ┌──────────────────────┐
 work together (routing, DI,              │                      │
 middleware, serialization)?"────YES──────►│ Write an INTEGRATION │
        │                                 │ TEST                 │
        NO                                └──────────────────────┘
        │
        ▼
"Am I testing the full system             ┌──────────────────────┐
 from a user's perspective                │                      │
 (browser, real DB, real APIs)?"──YES────►│  Write an E2E TEST   │
        │                                 │                      │
        NO                                └──────────────────────┘
        │
        ▼
  Reconsider what you are testing.
```

### 14.2 What to Test Where

| Concern | Unit Test | Integration Test |
|---|---|---|
| Business rule: discount is 10% for orders over $100 | Yes | No |
| Controller returns 404 for missing resource | No | Yes |
| Service correctly maps DTO to entity | Yes | No |
| POST /api/tasks returns 201 with Location header | No | Yes |
| Validation rejects empty strings | Yes | Yes (both) |
| Authentication rejects unauthenticated requests | No | Yes |
| JSON property names are camelCase | No | Yes |
| Database query returns correct results | No | Yes (Lecture 4) |
| Middleware catches exceptions and returns 500 | No | Yes |

> **Discussion (5 min):** Your team has limited time. Should you write more unit tests or more integration tests? What is the cost-benefit of each?

---

## 15. Practical Exercise

### Task: Build Integration Tests for a BookStore API

You are given a `BookStoreApi` with the following endpoints:

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/books` | List all books |
| GET | `/api/books/{id}` | Get book by ID |
| POST | `/api/books` | Create a new book |
| PUT | `/api/books/{id}` | Update a book |
| DELETE | `/api/books/{id}` | Delete a book (Admin only) |
| GET | `/api/books/search?query=...` | Search books by title |

**Models:**

```csharp
public record Book(int Id, string Title, string Author, decimal Price, int Year);
public record CreateBookRequest(string Title, string Author, decimal Price, int Year);
public record UpdateBookRequest(string Title, string Author, decimal Price, int Year);
```

**Your tasks:**

1. Create a `CustomWebApplicationFactory` that replaces the real database with an in-memory fake
2. Write integration tests covering:
   - GET all books returns 200 with correct JSON
   - GET by ID returns 404 for non-existent book
   - POST creates a book and returns 201 with Location header
   - POST with missing required fields returns 400
   - PUT updates an existing book and returns 204
   - DELETE as Admin returns 204
   - DELETE as regular User returns 403
   - Search returns matching books
3. Implement test isolation (state reset between tests)
4. Add authentication testing using a `TestAuthHandler`

**Bonus challenges:**
- Test that the API returns proper `ProblemDetails` for validation errors
- Test content negotiation (Accept header)
- Verify the response includes pagination headers for GET all

> **Discussion (15 min):** Review each other's tests. Are there edge cases that were missed? How readable are the test names? Could you understand the expected API behavior just by reading the test names?

---

## 16. Summary

### Key Takeaways

1. **Integration tests verify component interactions** — they catch bugs that unit tests cannot, such as routing errors, DI misconfiguration, and serialization issues

2. **WebApplicationFactory creates an in-memory test server** — no real ports, no network latency, same ASP.NET Core pipeline as production

3. **Custom factories let you replace services** — swap real databases, APIs, and email services with fakes or mocks using `ConfigureServices`

4. **Test all HTTP verbs and status codes** — GET (200, 404), POST (201, 400), PUT (204, 404), DELETE (204, 404), authentication (401, 403)

5. **IClassFixture shares the factory** across tests in a class, while **IAsyncLifetime** provides per-test setup and teardown

6. **Test isolation is critical** — use state reset, unique data, or fresh factories to prevent test interference

7. **Authentication can be faked** using a custom `AuthenticationHandler` that creates test claims and roles

8. **Middleware is testable** — exception handlers, CORS, logging, and other pipeline components can be verified through HTTP responses

9. **Use `System.Net.Http.Json`** for clean JSON serialization in tests (`PostAsJsonAsync`, `GetFromJsonAsync`)

10. **Follow the Async naming convention** — all async test methods should end with `Async`

### Preview of Next Lecture

In **Lecture 4: Database Testing with Testcontainers**, we will:
- Test real database queries and migrations using EF Core
- Use Testcontainers to spin up SQL Server and PostgreSQL in Docker
- Compare testing strategies: InMemory, SQLite, and Testcontainers
- Test data integrity, transactions, and concurrent access
- Integrate database testing with `WebApplicationFactory`

---

## References and Further Reading

- **Microsoft Documentation: Integration tests in ASP.NET Core**
  - https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
- **Microsoft Documentation: WebApplicationFactory**
  - https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.testing.webapplicationfactory-1
- **Microsoft Documentation: TestServer**
  - https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.testhost.testserver
- **xUnit v3 Documentation** — https://xunit.net/docs/getting-started/v3/cmdline
- **Shouldly Documentation** — https://docs.shouldly.org/
- **NSubstitute Documentation** — https://nsubstitute.github.io/help/getting-started/
- **"Unit Testing Principles, Practices, and Patterns"** — Vladimir Khorikov (Manning, 2020) — Chapters 8-9 on integration testing
- **ASP.NET Core in Action** — Andrew Lock (Manning, 3rd edition, 2023) — Chapter 36 on testing
- **System.Net.Http.Json Namespace** — https://learn.microsoft.com/en-us/dotnet/api/system.net.http.json
