# Лабораторна 4 — Інтеграційне тестування: API

## Мета

Тестувати повні цикли запитів/відповідей API, включаючи маршрутизацію, прив'язку моделей, валідацію, серіалізацію, узгодження вмісту та коди HTTP-статусів.

**Тривалість:** 60 хвилин

## Передумови

Перед початком цієї лабораторної переконайтеся, що:

- Встановлений .NET 10+ SDK (`dotnet --version`)
- Ви маєте робоче розуміння контролерів ASP.NET Core, прив'язки моделей та Data Annotations (або FluentValidation)
- Ви знайомі з HTTP-методами, кодами статусів та серіалізацією JSON
- Виконана Лабораторна 3 (інтеграційне тестування з `WebApplicationFactory`)

## Ключові поняття

### Повноциклове тестування API

На відміну від модульних тестів, що тестують контролер ізольовано, інтеграційні тести API надсилають реальні HTTP-запити через весь конвеєр ASP.NET Core. Це означає, що маршрутизація, прив'язка моделей, валідація, фільтри, серіалізація та узгодження вмісту задіюються в кожному тесті.

### Тестування валідації

ASP.NET Core автоматично валідує моделі, декоровані Data Annotations (наприклад, `[Required]`, `[MaxLength]`), перед виконанням дії контролера. Коли валідація не проходить, фреймворк повертає `400 Bad Request` з тілом `ValidationProblemDetails`. Ваші тести повинні перевіряти як код статусу, так і структуру відповіді з помилкою.

### Узгодження вмісту

API повинен відповідати з правильним заголовком `Content-Type` та форматом серіалізації. За замовчуванням ASP.NET Core повертає JSON з іменами властивостей у форматі camelCase. Тести повинні перевіряти, що ці угоди дотримуються послідовно.

### Допоміжні методи та базові класи

Уникайте дублювання логіки HTTP-викликів у тестах. Виносіть спільні шаблони (наприклад, створення завдання, перевірка відповіді 400) у допоміжні методи або спільний базовий клас. Це дозволяє тестам зосередитися на сценарії, що перевіряється.

## Інструменти

- Мова: C#
- Фреймворк: [xUnit v3](https://xunit.net/) (`xunit.v3`)
- HTTP: `HttpClient` через `WebApplicationFactory`
- Валідація: Data Annotations / FluentValidation

## Налаштування

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

## Завдання

### Завдання 1 — CRUD API з валідацією

Побудуйте `TasksController` (керування завданнями) з повним CRUD:

```
GET    /api/tasks              — отримати список усіх завдань (підтримка ?status=completed запиту)
GET    /api/tasks/{id}         — отримати завдання за id
POST   /api/tasks              — створити завдання
PUT    /api/tasks/{id}         — оновити завдання
DELETE /api/tasks/{id}         — видалити завдання
PATCH  /api/tasks/{id}/status  — оновити лише статус
```

Модель:

```csharp
public class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; }        // обов'язкове, макс. 200 символів
    public string Description { get; set; }  // необов'язкове, макс. 1000 символів
    public string Status { get; set; }       // "pending", "in_progress", "completed"
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }   // має бути у майбутньому при створенні
}
```

Напишіть інтеграційні тести, що покривають:

1. Успішні CRUD-операції з правильними кодами статусів (200, 201, 204, 404)
2. Помилки валідації повертають 400 з відповідними повідомленнями про помилки
3. Невалідні значення статусу відхиляються
4. Фільтрація запитів за статусом працює правильно
5. Створення завдання з минулою `DueDate` повертає помилку валідації
6. Граничні випадки: неіснуючий ресурс повертає 404 зі структурованою помилкою, порожнє тіло повертає 400

**Мінімальна кількість тестів: 8 тестів**

#### Приклад: CRUD-тест з Shouldly

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

#### Таблиця очікуваної поведінки — CRUD-операції

| Операція | Сценарій | Очікуваний статус | Примітки |
|---|---|---|---|
| `POST /api/tasks` | Валідне тіло | 201 Created | Повертає створене завдання з `Id` |
| `POST /api/tasks` | Відсутній `Title` | 400 Bad Request | Помилка згадує `Title` |
| `POST /api/tasks` | `Title` > 200 символів | 400 Bad Request | Помилка валідації |
| `POST /api/tasks` | Невалідне значення `Status` | 400 Bad Request | Лише `pending`, `in_progress`, `completed` |
| `POST /api/tasks` | Минула `DueDate` | 400 Bad Request | Має бути у майбутньому |
| `GET /api/tasks` | Без фільтра | 200 OK | Повертає всі завдання |
| `GET /api/tasks?status=completed` | Фільтр за статусом | 200 OK | Лише завершені завдання |
| `GET /api/tasks/{id}` | Існуюче завдання | 200 OK | Повертає JSON завдання |
| `GET /api/tasks/{id}` | Неіснуюче завдання | 404 Not Found | Структурована помилка |
| `PUT /api/tasks/{id}` | Валідне оновлення | 200 OK | Повертає оновлене завдання |
| `PATCH /api/tasks/{id}/status` | Валідна зміна статусу | 200 OK | Змінюється лише статус |
| `DELETE /api/tasks/{id}` | Існуюче завдання | 204 No Content | Порожнє тіло |
| `DELETE /api/tasks/{id}` | Неіснуюче завдання | 404 Not Found | Структурована помилка |

> **Підказка:** Використовуйте власний `ICustomValidation` або атрибут `[CustomValidation]` для перевірки майбутньої дати `DueDate`, оскільки Data Annotations самі по собі не можуть порівнювати з `DateTime.UtcNow`.

### Завдання 2 — Узгодження вмісту та серіалізація

Напишіть тести, що перевіряють:

1. API повертає JSON за замовчуванням
2. Формат серіалізації дати є послідовним (ISO 8601)
3. Null-значення необов'язкових полів виключаються з відповіді (або включаються — протестуйте обрану поведінку)
4. Відповідь містить правильний заголовок `Content-Type`
5. Невалідний JSON у тілі запиту повертає 400

**Мінімальна кількість тестів: 4 тести**

#### Приклад: Тест серіалізації з Shouldly

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

> **Підказка:** Для тестування виключення null-полів налаштуйте `JsonSerializerOptions` у `Program.cs` з `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` та перевірте, що JSON-тіло не містить ключ null-властивості.

## Оцінювання

| Критерії |
|----------|
| Завдання 1 — Тести CRUD + валідації |
| Завдання 2 — Тести серіалізації |
| Використання допоміжних методів / базового тестового класу |
| Усі тести незалежні та відтворювані |

## Здача роботи

- Рішення з проєктами `Lab4.Api` та `Lab4.Tests`
- Мінімум 12 тестів загалом
- Продемонструйте покриття як успішних, так і помилкових сценаріїв

## Посилання

- [Інтеграційні тести в ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) — шаблони `WebApplicationFactory` та `HttpClient`
- [Валідація моделей в ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation) — Data Annotations, `ValidationProblemDetails`
- [Обробка помилок в API ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/web-api/handle-errors) — `ProblemDetails`, обробка винятків
- [Документація xUnit v3](https://xunit.net/docs/getting-started/v3/cmdline) — довідник тестового фреймворку
- [Документація Shouldly](https://docs.shouldly.org/) — API бібліотеки перевірок
- [System.Text.Json в ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/web-api/advanced/formatting) — параметри серіалізації, узгодження вмісту
- [Документація FluentValidation](https://docs.fluentvalidation.net/) — альтернативна бібліотека валідації (необов'язково)
