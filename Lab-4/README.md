# Lab 4 — Integration Testing: API

## Objective

Test full API request/response cycles including routing, model binding, validation, serialization, content negotiation, and HTTP status codes.

**Duration:** 60 minutes

## Prerequisites

Before starting this lab, make sure you have:

- .NET 10+ SDK installed (`dotnet --version`)
- A working understanding of ASP.NET Core controllers, model binding, and Data Annotations (or FluentValidation)
- Familiarity with HTTP methods, status codes, and JSON serialization
- Completed Lab 3 (integration testing with `WebApplicationFactory`)

## Key Concepts

### Full-Cycle API Testing

Unlike unit tests that test a controller in isolation, API integration tests send real HTTP requests through the entire ASP.NET Core pipeline. This means routing, model binding, validation, filters, serialization, and content negotiation are all exercised in each test.

### Validation Testing

ASP.NET Core automatically validates models decorated with Data Annotations (e.g., `[Required]`, `[MaxLength]`) before the controller action executes. When validation fails, the framework returns a `400 Bad Request` with a `ValidationProblemDetails` body. Your tests must verify both the status code and the structure of the error response.

### Content Negotiation

The API should respond with the correct `Content-Type` header and serialization format. By default, ASP.NET Core returns JSON with camelCase property names. Tests should verify these conventions are consistent.

### Helper Methods and Base Classes

Avoid duplicating HTTP call logic across tests. Extract common patterns (e.g., creating a task, asserting a 400 response) into helper methods or a shared base class. This keeps tests focused on the scenario being verified.

## Tools

- Language: C#
- Framework: [xUnit v3](https://xunit.net/) (`xunit.v3`)
- HTTP: `HttpClient` via `WebApplicationFactory`
- Validation: Data Annotations / FluentValidation

## Setup

```bash
dotnet new sln -n Lab4
dotnet new webapi -n Lab4.Api
dotnet new classlib -n Lab4.Tests
dotnet sln add Lab4.Api Lab4.Tests
dotnet add Lab4.Tests reference Lab4.Api
dotnet add Lab4.Tests package xunit.v3
dotnet add Lab4.Tests package Microsoft.NET.Test.Sdk
dotnet add Lab4.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet add Lab4.Tests package Shouldly
```

## Tasks

### Task 1 — CRUD API with Validation

Build a `TasksController` (to-do task management) with full CRUD:

```
GET    /api/tasks              — list all tasks (support ?status=completed query)
GET    /api/tasks/{id}         — get task by id
POST   /api/tasks              — create task
PUT    /api/tasks/{id}         — update task
DELETE /api/tasks/{id}         — delete task
PATCH  /api/tasks/{id}/status  — update only status
```

Model:

```csharp
public class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; }        // required, max 200 chars
    public string Description { get; set; }  // optional, max 1000 chars
    public string Status { get; set; }       // "pending", "in_progress", "completed"
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }   // must be in the future when creating
}
```

Write integration tests covering:

1. Successful CRUD operations with correct status codes (200, 201, 204, 404)
2. Validation errors return 400 with proper error messages
3. Invalid status values are rejected
4. Query filtering by status works correctly
5. Creating a task with past `DueDate` returns validation error
6. Edge cases: non-existing resource returns 404 with structured error, empty body returns 400

**Minimum test count: 8 tests**

#### Example: CRUD Test with Shouldly

```csharp
public class TasksApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public TasksApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateTask_ValidData_Returns201Async()
    {
        // Arrange
        var newTask = new
        {
            Title = "Write unit tests",
            Description = "Cover all edge cases",
            Status = "pending",
            DueDate = DateTime.UtcNow.AddDays(7)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/tasks", newTask);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<TaskItem>();
        created.ShouldNotBeNull();
        created.Title.ShouldBe("Write unit tests");
        created.Id.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CreateTask_MissingTitle_Returns400Async()
    {
        // Arrange
        var invalidTask = new
        {
            Description = "No title provided",
            Status = "pending"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/tasks", invalidTask);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Title");
    }

    [Fact]
    public async Task CreateTask_TitleExceedsMaxLength_Returns400Async()
    {
        // Arrange
        var invalidTask = new
        {
            Title = new string('A', 201), // 201 chars, max is 200
            Status = "pending"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/tasks", invalidTask);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTask_PastDueDate_Returns400Async()
    {
        // Arrange
        var invalidTask = new
        {
            Title = "Overdue task",
            Status = "pending",
            DueDate = DateTime.UtcNow.AddDays(-1) // in the past
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/tasks", invalidTask);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetTasks_FilterByStatus_ReturnsOnlyMatchingAsync()
    {
        // Arrange — create tasks with different statuses
        await _client.PostAsJsonAsync("/api/tasks", new { Title = "Task A", Status = "pending" });
        await _client.PostAsJsonAsync("/api/tasks", new { Title = "Task B", Status = "completed" });

        // Act
        var response = await _client.GetAsync("/api/tasks?status=completed");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tasks = await response.Content.ReadFromJsonAsync<List<TaskItem>>();
        tasks.ShouldNotBeNull();
        tasks.ShouldAllBe(t => t.Status == "completed");
    }

    [Fact]
    public async Task DeleteTask_Existing_Returns204Async()
    {
        // Arrange — create a task first
        var createResponse = await _client.PostAsJsonAsync("/api/tasks",
            new { Title = "To delete", Status = "pending" });
        var created = await createResponse.Content.ReadFromJsonAsync<TaskItem>();

        // Act
        var response = await _client.DeleteAsync($"/api/tasks/{created!.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify it is gone
        var getResponse = await _client.GetAsync($"/api/tasks/{created.Id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
```

#### Expected Behavior Table — CRUD Operations

| Operation | Scenario | Expected Status | Notes |
|---|---|---|---|
| `POST /api/tasks` | Valid body | 201 Created | Returns created task with `Id` |
| `POST /api/tasks` | Missing `Title` | 400 Bad Request | Error mentions `Title` |
| `POST /api/tasks` | `Title` > 200 chars | 400 Bad Request | Validation error |
| `POST /api/tasks` | Invalid `Status` value | 400 Bad Request | Only `pending`, `in_progress`, `completed` |
| `POST /api/tasks` | Past `DueDate` | 400 Bad Request | Must be in the future |
| `GET /api/tasks` | No filter | 200 OK | Returns all tasks |
| `GET /api/tasks?status=completed` | Filter by status | 200 OK | Only completed tasks |
| `GET /api/tasks/{id}` | Existing task | 200 OK | Returns task JSON |
| `GET /api/tasks/{id}` | Non-existing task | 404 Not Found | Structured error |
| `PUT /api/tasks/{id}` | Valid update | 200 OK | Returns updated task |
| `PATCH /api/tasks/{id}/status` | Valid status change | 200 OK | Only status changes |
| `DELETE /api/tasks/{id}` | Existing task | 204 No Content | Empty body |
| `DELETE /api/tasks/{id}` | Non-existing task | 404 Not Found | Structured error |

> **Hint:** Use a custom `ICustomValidation` or a `[CustomValidation]` attribute for the `DueDate` future-date check, since Data Annotations alone cannot compare against `DateTime.UtcNow`.

### Task 2 — Content Negotiation and Serialization

Write tests that verify:

1. API returns JSON by default
2. Date serialization format is consistent (ISO 8601)
3. Null optional fields are excluded from response (or included — test your chosen behavior)
4. Response includes correct `Content-Type` header
5. Invalid JSON in request body returns 400

**Minimum test count: 4 tests**

#### Example: Serialization Test with Shouldly

```csharp
[Fact]
public async Task Response_HasJsonContentTypeAsync()
{
    // Act
    var response = await _client.GetAsync("/api/tasks");

    // Assert
    response.Content.Headers.ContentType.ShouldNotBeNull();
    response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
}

[Fact]
public async Task DateSerialization_UsesIso8601Async()
{
    // Arrange
    await _client.PostAsJsonAsync("/api/tasks", new
    {
        Title = "Date check",
        Status = "pending",
        DueDate = new DateTime(2026, 12, 31, 10, 30, 0, DateTimeKind.Utc)
    });

    // Act
    var response = await _client.GetAsync("/api/tasks");
    var body = await response.Content.ReadAsStringAsync();

    // Assert — ISO 8601 format: "2026-12-31T10:30:00..."
    body.ShouldContain("2026-12-31T10:30:00");
}

[Fact]
public async Task InvalidJson_Returns400Async()
{
    // Arrange
    var content = new StringContent(
        "{ invalid json !!!",
        System.Text.Encoding.UTF8,
        "application/json");

    // Act
    var response = await _client.PostAsync("/api/tasks", content);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
}
```

> **Hint:** To test null-field exclusion, configure `JsonSerializerOptions` in `Program.cs` with `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` and verify the JSON body does not contain the null property key.

## Grading

| Criteria |
|----------|
| Task 1 — CRUD + validation tests |
| Task 2 — Serialization tests |
| Use of helper methods / base test class |
| All tests independent and repeatable |

## Submission

- Solution with `Lab4.Api` and `Lab4.Tests` projects
- Minimum 12 total tests
- Demonstrate both happy-path and error-path coverage

## References

- [Integration tests in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) — `WebApplicationFactory` and `HttpClient` patterns
- [Model Validation in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation) — Data Annotations, `ValidationProblemDetails`
- [Handle errors in ASP.NET Core APIs](https://learn.microsoft.com/en-us/aspnet/core/web-api/handle-errors) — `ProblemDetails`, exception handling
- [xUnit v3 Documentation](https://xunit.net/docs/getting-started/v3/cmdline) — test framework reference
- [Shouldly Documentation](https://docs.shouldly.org/) — assertion library API
- [System.Text.Json in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/web-api/advanced/formatting) — serialization options, content negotiation
- [FluentValidation Documentation](https://docs.fluentvalidation.net/) — alternative validation library (optional)
