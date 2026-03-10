# Лекція 3: Інтеграційне тестування з WebApplicationFactory

## Навчальні цілі

Після завершення цієї лекції студенти зможуть:

- Пояснити різницю між модульним та інтеграційним тестуванням
- Описати, як працює тестова інфраструктура ASP.NET Core внутрішньо
- Використовувати `WebApplicationFactory<T>` для запуску in-memory тестового сервера
- Створювати власні підкласи `WebApplicationFactory` із заміною сервісів
- Тестувати HTTP-ендпоінти (GET, POST, PUT, DELETE) з належними перевірками
- Замінювати та переналаштовувати сервіси в DI-контейнері для тестування
- Тестувати middleware, автентифікацію та авторизацію
- Застосовувати `IClassFixture<T>` та `IAsyncLifetime` для управління життєвим циклом тестів
- Визначати поширені помилки та застосовувати найкращі практики інтеграційних тестів

---

## 1. Модульне тестування vs. Інтеграційне тестування: Короткий підсумок

### 1.1 Де закінчуються модульні тести

У Лекції 2 ми дізнались, що модульні тести перевіряють окремий клас або метод **ізольовано**, замінюючи всі залежності тестовими дублерами. Це потужно, але має сліпу зону:

```
Модульні тести перевіряють:          Модульні тести НЕ перевіряють:
──────────────────────               ──────────────────────────────
✓ Бізнес-логіку ізольовано           ✗ HTTP-маршрутизацію та прив'язку моделей
✓ Правила валідації вводу            ✗ Конвеєр middleware (auth, CORS тощо)
✓ Граничні випадки та обробку помилок ✗ Підключення dependency injection
✓ Коректність алгоритмів            ✗ JSON серіалізацію/десеріалізацію
                                     ✗ Запити до бази даних (реальний SQL)
                                     ✗ Спільну роботу кількох компонентів
```

### 1.2 Що покривають інтеграційні тести

**Інтеграційні тести** перевіряють, що кілька компонентів працюють **разом** коректно. Вони тестують з'єднання між компонентами — місця, де ховаються непорозуміння та помилки конфігурації.

```
┌───────────────────────────────────────────────────────────┐
│                   Інтеграційний тест                      │
│                                                           │
│  HTTP-запит ──► Маршрутизація ──► Middleware ──► Контролер │
│                                                  │        │
│                                             Шар сервісів  │
│                                                  │        │
│  HTTP-відповідь ◄── Серіалізація ◄── Результат ◄─┘        │
│                                                           │
│  Перевіряє: коди стану, заголовки, тіло відповіді,        │
│             підключення DI, поведінку middleware, маршрути │
└───────────────────────────────────────────────────────────┘
```

### 1.3 Піраміда тестування

```
            ╱╲
           ╱  ╲         E2E / UI тести
          ╱    ╲        (мало, повільні, крихкі)
         ╱──────╲
        ╱        ╲      Інтеграційні тести        ◄── ЦЯ ЛЕКЦІЯ
       ╱          ╲     (помірна кількість, середня швидкість)
      ╱────────────╲
     ╱              ╲   Модульні тести
    ╱                ╲  (багато, швидкі, ізольовані)
   ╱──────────────────╲
```

| Аспект | Модульні тести | Інтеграційні тести | E2E тести |
|---|---|---|---|
| **Обсяг** | Один клас/метод | Кілька компонентів | Вся система |
| **Швидкість** | Мілісекунди | Мілісекунди-секунди | Секунди-хвилини |
| **Залежності** | Всі замоковані | Деякі реальні, деякі замоковані | Всі реальні |
| **Впевненість** | Логіка коректна | Компоненти працюють разом | Система працює для користувачів |
| **Обслуговування** | Низьке | Середнє | Високе |

> **Дискусія (5 хв):** У вас 100% покриття модульними тестами, але API повертає помилки 500 у продакшні. Як це можливо? Що могли б виявити інтеграційні тести?

---

## 2. Огляд тестової інфраструктури ASP.NET Core

### 2.1 Проблема: Як тестувати Web API?

Тестування ASP.NET Core API без розгортання на реальному сервері було історично болісним. Потрібно було:

1. Зібрати та опублікувати додаток
2. Запустити його на порту
3. Відправити реальні HTTP-запити
4. Зупинити після тестів

Це повільно, нестабільно та важко автоматизувати.

### 2.2 Рішення: `Microsoft.AspNetCore.Mvc.Testing`

ASP.NET Core надає **вбудовану тестову інфраструктуру** через NuGet-пакет `Microsoft.AspNetCore.Mvc.Testing`. Центральним елементом є `WebApplicationFactory<TEntryPoint>`.

```bash
dotnet add package Microsoft.AspNetCore.Mvc.Testing
```

### 2.3 Огляд архітектури

```
┌─────────────────────────────────────────────────────────────┐
│  Тестовий процес (один процес, без мережі)                  │
│                                                             │
│  ┌──────────────┐         ┌──────────────────────────────┐  │
│  │  Тестовий     │         │  TestServer                  │  │
│  │  клас         │  HTTP   │  ┌────────────────────────┐  │  │
│  │  HttpClient ─┼────────►│  │  Конвеєр ASP.NET Core  │  │  │
│  │              │ in-mem   │  │  ┌──────────────────┐  │  │  │
│  │  Перевірки   │◄────────┼  │  │  Middleware       │  │  │  │
│  │              │         │  │  │  ┌──────────────┐ │  │  │  │
│  └──────────────┘         │  │  │  │ Контролери  │ │  │  │  │
│                           │  │  │  │  ┌────────┐ │ │  │  │  │
│                           │  │  │  │  │Сервіси │ │ │  │  │  │
│                           │  │  │  │  └────────┘ │ │  │  │  │
│                           │  │  │  └──────────────┘ │  │  │  │
│                           │  │  └──────────────────┘  │  │  │
│                           │  └────────────────────────┘  │  │
│                           └──────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

Ключові моменти:

- **Без реального HTTP-трафіку** — запити проходять через in-memory канал
- **Без відкритих портів** — без конфліктів портів, без проблем з брандмауером
- **Той самий конвеєр** — middleware, маршрутизація, прив'язка моделей, фільтри — все виконується
- **Швидко** — без мережевої затримки, без витрат на запуск процесу
- **Один процес** — тест і сервер працюють в одному процесі

---

## 3. Детальний розгляд WebApplicationFactory

### 3.1 Що відбувається при створенні WebApplicationFactory?

`WebApplicationFactory<TEntryPoint>` внутрішньо виконує наступне:

```
1. Знаходить збірку точки входу додатку
   └── Використовує TEntryPoint для пошуку кореневої директорії проєкту

2. Будує хост
   └── Викликає Program.cs / Startup.cs для конфігурації сервісів та middleware
   └── АЛЕ використовує TestServer замість Kestrel

3. Створює TestServer
   └── In-memory сервер, що обробляє запити без TCP/IP
   └── Реалізує інтерфейс IServer

4. Надає HttpClient
   └── CreateClient() повертає HttpClient, налаштований для роботи з TestServer
   └── Базова адреса за замовчуванням http://localhost

5. Керує життєвим циклом
   └── Реалізує IDisposable / IAsyncDisposable
   └── Зупиняє хост та сервер при утилізації
```

### 3.2 Найпростіший інтеграційний тест

Почнемо з мінімального прикладу. Припустимо, у нас є простий ASP.NET Core API:

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<ITaskService, TaskService>();

var app = builder.Build();

app.MapControllers();
app.Run();

// Зробити клас Program доступним для тестового проєкту
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
// Моделі
namespace TaskApi;

public record TaskItem(int Id, string Title, string? Description, bool IsCompleted);
public record CreateTaskRequest(string Title, string? Description);
public record UpdateTaskRequest(string Title, string? Description, bool IsCompleted);
```

Тепер інтеграційний тест:

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

> **Ключовий момент:** Рядок `public partial class Program { }` внизу `Program.cs` необхідний для того, щоб зробити клас точки входу доступним для тестового проєкту. Без нього `WebApplicationFactory<Program>` не зможе знайти додаток.

### 3.3 Внутрішня будова TestServer

`TestServer` замінює Kestrel (продакшн HTTP-сервер) на in-memory транспорт:

```
Продакшн:
  HttpClient ──[TCP/IP]──► Kestrel ──► Конвеєр ASP.NET Core

Тестування:
  HttpClient ──[in-memory]──► TestServer ──► Конвеєр ASP.NET Core
```

`TestServer` створює спеціальний `HttpMessageHandler`, який обходить мережевий стек. Коли ви викликаєте `factory.CreateClient()`, повернутий `HttpClient` використовує цей обробник внутрішньо.

```csharp
// Що CreateClient() робить внутрішньо (спрощено):
var handler = _testServer.CreateHandler();     // in-memory обробник
var client = new HttpClient(handler)
{
    BaseAddress = new Uri("http://localhost")   // базова адреса за замовчуванням
};
```

Це означає:
- Запити ніколи не залишають процес
- DNS-розв'язання не потрібне
- Немає SSL/TLS-узгодження
- Немає прив'язки до портів чи конфліктів
- Надзвичайно швидкі запити-відповіді

> **Дискусія (5 хв):** Які компроміси in-memory тестування? Які проблеми можна пропустити, не використовуючи реальний HTTP? (Підказка: подумайте про конфігурацію TLS, HTTP/2, поведінку балансувальника навантаження, CORS з реальним браузером.)

---

## 4. Власна WebApplicationFactory

### 4.1 Навіщо налаштовувати?

Стандартна `WebApplicationFactory<Program>` використовує вашу реальну конфігурацію `Program.cs`. У продакшні ваш API може:
- Підключатися до реальної бази даних SQL Server
- Викликати зовнішні платіжні API
- Відправляти листи через SMTP
- Використовувати Azure Key Vault для секретів

Нічого з цього не потрібно в тестах. Власна фабрика дозволяє **замінити** конкретні сервіси, зберігаючи решту реального конвеєра.

### 4.2 Створення власної фабрики

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
            // Видалити реальну реєстрацію репозиторію
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ITaskRepository));

            if (descriptor is not null)
                services.Remove(descriptor);

            // Додати in-memory фейковий репозиторій
            services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();
        });

        builder.UseEnvironment("Testing");
    }
}
```

### 4.3 Повторно використовуваний In-Memory репозиторій

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

### 4.4 Використання власної фабрики

```csharp
public class TasksControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TasksControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // Тести тепер використовують InMemoryTaskRepository замість реальної бази даних
}
```

### 4.5 Патерн заміни сервісів

Загальний патерн заміни сервісів складається з таких кроків:

```
1. Знайти існуючий дескриптор сервісу
2. Видалити його з колекції сервісів
3. Зареєструвати вашу тестову заміну

services.RemoveAll<IMyService>();         // Кроки 1+2 об'єднані
services.AddSingleton<IMyService, FakeMyService>();  // Крок 3
```

Використання `RemoveAll<T>()` з `Microsoft.Extensions.DependencyInjection.Extensions` є чистішим:

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

> **Дискусія (5 хв):** Коли б ви використали фейк (як `InMemoryTaskRepository`) vs. NSubstitute мок всередині фабрики? Які компроміси?

---

## 5. Тестування HTTP-ендпоінтів

### 5.1 Тестування GET-ендпоінтів

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
        // Arrange — спочатку створюємо завдання
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

### 5.2 Тестування POST-ендпоінтів

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

        // Перевірка, що заголовок Location вказує на новий ресурс
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.ToString().ShouldContain("/api/tasks/");

        // Перевірка тіла відповіді
        var task = await response.Content.ReadFromJsonAsync<TaskItem>();
        task.ShouldNotBeNull();
        task.Id.ShouldBeGreaterThan(0);
        task.Title.ShouldBe("New Task");
        task.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task Create_MissingTitle_Returns400Async()
    {
        // Arrange — відправляємо порожній об'єкт (Title обов'язковий)
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

### 5.3 Тестування PUT-ендпоінтів

```csharp
[Fact]
public async Task Update_ExistingTask_Returns204Async()
{
    // Arrange — спочатку створюємо завдання
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

    // Перевірка збереження оновлення
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

### 5.4 Тестування DELETE-ендпоінтів

```csharp
[Fact]
public async Task Delete_ExistingTask_Returns204Async()
{
    // Arrange — спочатку створюємо завдання
    var createResponse = await _client.PostAsJsonAsync(
        "/api/tasks", new CreateTaskRequest("To Delete", null));
    var created = await createResponse.Content.ReadFromJsonAsync<TaskItem>();

    // Act
    var response = await _client.DeleteAsync($"/api/tasks/{created!.Id}");

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    // Перевірка видалення
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

### 5.5 Довідник кодів стану HTTP

| Код стану | Значення | Коли використовувати |
|---|---|---|
| `200 OK` | Успіх з тілом | GET, що повертає дані |
| `201 Created` | Ресурс створено | POST із заголовком Location |
| `204 No Content` | Успіх без тіла | PUT, DELETE успіх |
| `400 Bad Request` | Невалідний ввід | Помилки валідації |
| `401 Unauthorized` | Не автентифіковано | Відсутні/невалідні облікові дані |
| `403 Forbidden` | Не авторизовано | Автентифіковано, але недостатньо прав |
| `404 Not Found` | Ресурс не знайдено | GET/PUT/DELETE з невалідним ID |
| `409 Conflict` | Конфлікт стану | Дублікат створення, невідповідність версій |
| `500 Internal Server Error` | Помилка сервера | Необроблені винятки |

---

## 6. Робота з HttpClient у тестах

### 6.1 Серіалізація та десеріалізація JSON

Простір імен `System.Net.Http.Json` надає методи-розширення, що спрощують роботу з JSON:

```csharp
using System.Net.Http.Json;

// POST з JSON-тілом
var response = await _client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest("Title", null));

// Читання тіла відповіді як JSON
var task = await response.Content.ReadFromJsonAsync<TaskItem>();

// GET та десеріалізація в одному виклику
var tasks = await _client.GetFromJsonAsync<List<TaskItem>>("/api/tasks");

// Власні налаштування серіалізації
var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};
var result = await response.Content.ReadFromJsonAsync<TaskItem>(options);
```

### 6.2 Встановлення власних заголовків

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

### 6.3 Відправка даних форми та власного контенту

```csharp
// Рядковий контент з явним медіатипом
var jsonContent = new StringContent(
    """{"title": "My Task", "description": null}""",
    System.Text.Encoding.UTF8,
    "application/json");
var response = await _client.PostAsync("/api/tasks", jsonContent);

// Form URL-encoded контент
var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["username"] = "admin",
    ["password"] = "secret"
});
var loginResponse = await _client.PostAsync("/api/auth/login", formContent);
```

### 6.4 Читання деталей відповіді

```csharp
[Fact]
public async Task Create_ValidRequest_ReturnsExpectedHeadersAsync()
{
    // Arrange
    var request = new CreateTaskRequest("Header Test", null);

    // Act
    var response = await _client.PostAsJsonAsync("/api/tasks", request);

    // Assert — код стану
    response.StatusCode.ShouldBe(HttpStatusCode.Created);

    // Assert — заголовки
    response.Headers.Location.ShouldNotBeNull();

    // Assert — тіло відповіді як рядок (корисно для налагодження)
    var body = await response.Content.ReadAsStringAsync();
    body.ShouldContain("Header Test");

    // Assert — тіло відповіді як типізований об'єкт
    var task = await response.Content.ReadFromJsonAsync<TaskItem>();
    task!.Title.ShouldBe("Header Test");
}
```

---

## 7. Життєвий цикл тестів: IClassFixture та IAsyncLifetime

### 7.1 Розуміння життєвого циклу тестів xUnit v3

xUnit v3 створює **новий екземпляр** тестового класу для **кожного тестового методу**. Це забезпечує природну ізоляцію тестів, але може бути дорогим, якщо налаштування коштує багато:

```
Тестовий метод 1:  new TestClass() → Запуск тесту → Dispose
Тестовий метод 2:  new TestClass() → Запуск тесту → Dispose
Тестовий метод 3:  new TestClass() → Запуск тесту → Dispose
                    ▲
                    │
           Нова фабрика + TestServer для КОЖНОГО тесту?
           Це було б дуже повільно!
```

### 7.2 IClassFixture — Спільне використання дорогих ресурсів

`IClassFixture<T>` вказує xUnit створити **один екземпляр** типу фікстури та поділити його між усіма тестами в класі:

```csharp
public class TasksControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    // Конструктор отримує СПІЛЬНИЙ екземпляр фабрики
    public TasksControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // Всі тестові методи поділяють ту саму фабрику (і TestServer)
    // Але кожен тест отримує свій HttpClient
}
```

```
Життєвий цикл IClassFixture:

new CustomWebApplicationFactory()      ← Створюється ОДИН раз перед будь-яким тестом
    │
    ├── new TestClass(factory) → Тест 1 → Dispose TestClass
    ├── new TestClass(factory) → Тест 2 → Dispose TestClass
    ├── new TestClass(factory) → Тест 3 → Dispose TestClass
    │
Dispose factory                        ← Утилізується ОДИН раз після всіх тестів
```

### 7.3 IAsyncLifetime — Асинхронне налаштування та очищення

Коли налаштування або очищення тесту вимагає асинхронних операцій (напр., заповнення бази даних, очищення даних), реалізуйте `IAsyncLifetime`:

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

    // Викликається ПЕРЕД кожним тестовим методом
    public async Task InitializeAsync()
    {
        // Заповнення тестовими даними
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        await repo.CreateAsync(new TaskItem(0, "Seeded Task", "Description", false));
    }

    // Викликається ПІСЛЯ кожного тестового методу
    public async Task DisposeAsync()
    {
        // Очищення тестових даних
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        await repo.ClearAllAsync(); // гіпотетичний метод очищення
    }

    [Fact]
    public async Task GetAll_WithSeededData_ReturnsSeededTaskAsync()
    {
        // Заповнене завдання доступне тут
        var tasks = await _client.GetFromJsonAsync<TaskItem[]>("/api/tasks");
        tasks.ShouldNotBeNull();
        tasks.Length.ShouldBeGreaterThanOrEqualTo(1);
    }
}
```

### 7.4 Порівняння IClassFixture та IAsyncLifetime

```
┌──────────────────────────┬──────────────────────────┐
│    IClassFixture<T>      │     IAsyncLifetime        │
├──────────────────────────┼──────────────────────────┤
│ Поділяє один екземпляр T │ Налаштування/очищення    │
│ між УСІМА тестами        │ для кожного тесту        │
│ в класі                  │ InitializeAsync() перед   │
│                          │ кожним тестом             │
│ Добре для: дорогих       │ DisposeAsync() після      │
│ ресурсів (фабрика,       │ кожного тесту             │
│ TestServer)              │                           │
│                          │ Добре для: заповнення     │
│ Область: рівень класу    │ даними, скидання стану    │
│                          │ Область: рівень тесту     │
└──────────────────────────┴──────────────────────────┘
```

Їх часто використовують **разом**: `IClassFixture` поділяє фабрику, тоді як `IAsyncLifetime` керує налаштуванням даних для кожного тесту.

> **Дискусія (5 хв):** Що станеться, якщо два тести модифікують ті самі спільні дані (напр., обидва створюють завдання з ID 1)? Як можна запобігти взаємному впливу тестів?

---

## 8. Стратегії ізоляції тестів

### 8.1 Проблема спільного стану

Коли тести поділяють `WebApplicationFactory` (і відповідно сервер та його сервіси), вони можуть впливати один на одного, якщо сервіси зберігають стан:

```
Тест A: Створює завдання "Task A" ────► Репозиторій тепер має [Task A]
Тест B: Викликає GetAll ──────────────► Отримує [Task A] ← НЕСПОДІВАНО!
Тест C: Створює завдання "Task C" ────► Репозиторій тепер має [Task A, Task C]
Тест D: Перевіряє count == 1 ─────────► ПРОВАЛ! Кількість 2
```

### 8.2 Стратегія 1: Новий клієнт для кожного тесту

Створіть новий `HttpClient` для кожного тесту з унікальною конфігурацією фабрики:

```csharp
[Fact]
public async Task GetAll_ReturnsOnlyCurrentTestDataAsync()
{
    // Створюємо фабрику з новим, порожнім репозиторієм
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

    // Цей тест має власний ізольований репозиторій
    var tasks = await client.GetFromJsonAsync<TaskItem[]>("/api/tasks");
    tasks.ShouldNotBeNull();
    tasks.ShouldBeEmpty();
}
```

### 8.3 Стратегія 2: Скидання стану між тестами

Використовуйте `IAsyncLifetime` для скидання спільного стану:

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
        // Скидання in-memory репозиторію перед кожним тестом
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

### 8.4 Стратегія 3: Унікальні дані для кожного тесту

Проєктуйте тести так, щоб вони не конфліктували — кожен тест створює та перевіряє свої власні унікальні дані:

```csharp
[Fact]
public async Task Create_UniqueTask_CanBeRetrievedByIdAsync()
{
    // Arrange — використовуємо унікальний ідентифікатор для уникнення конфліктів
    var uniqueTitle = $"Task-{Guid.NewGuid()}";
    var request = new CreateTaskRequest(uniqueTitle, "Isolation test");

    // Act
    var createResponse = await _client.PostAsJsonAsync("/api/tasks", request);
    var created = await createResponse.Content.ReadFromJsonAsync<TaskItem>();

    // Assert — отримуємо за ID (не залежить від інших тестів)
    var task = await _client.GetFromJsonAsync<TaskItem>(
        $"/api/tasks/{created!.Id}");
    task!.Title.ShouldBe(uniqueTitle);
}
```

### 8.5 Порівняння стратегій ізоляції

| Стратегія | Переваги | Недоліки |
|---|---|---|
| **Нова фабрика для кожного тесту** | Повна ізоляція | Повільно — новий сервер для кожного тесту |
| **Скидання стану між тестами** | Швидко, спільний сервер | Потрібно пам'ятати скидати все |
| **Унікальні дані для кожного тесту** | Швидко, без очищення | Може накопичувати застарілі дані |
| **Відкат транзакцій** | Чисто, враховує базу даних | Працює лише з реальними базами даних |

---

## 9. Тестування Middleware

### 9.1 Що таке Middleware?

Компоненти middleware формують конвеєр, що обробляє кожен HTTP-запит та відповідь:

```
Запит ──► Middleware 1 ──► Middleware 2 ──► Middleware 3 ──► Ендпоінт
          (Логування)       (Auth)          (Обробка помилок)
Відповідь ◄── Middleware 1 ◄── Middleware 2 ◄── Middleware 3 ◄── Ендпоінт
```

Поширені middleware: обробка винятків, автентифікація, авторизація, CORS, обмеження частоти, логування запитів.

### 9.2 Тестування Middleware обробки винятків

Розглянемо глобальний обробник винятків:

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

Перевірка, що middleware перетворює винятки на коректні HTTP-відповіді:

```csharp
[Fact]
public async Task Middleware_UnhandledException_Returns500WithProblemDetailsAsync()
{
    // Arrange — налаштовуємо фабрику, де сервіс генерує виняток
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

### 9.3 Тестування CORS Middleware

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

### 9.4 Тестування Middleware логування запитів

Ви можете захоплювати логи в тестах, зареєструвавши власний провайдер логування:

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

## 10. Тестування автентифікації та авторизації

### 10.1 Виклик

Продакшн API часто вимагають автентифікації (JWT, cookies, API-ключі). В інтеграційних тестах потрібно або:

1. **Обійти автентифікацію** повністю (для тестування логіки, не пов'язаної з auth)
2. **Імітувати автентифікацію** з фейковими токенами або claims
3. **Тестувати сам конвеєр auth**

### 10.2 Стратегія 1: Фейковий обробник автентифікації

ASP.NET Core дозволяє зареєструвати власний обробник автентифікації, який завжди успішний:

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
        // Перевірка, чи тест хоче імітувати неавтентифікований запит
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

        // Дозволити тестам додавати власні ролі через заголовок
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

### 10.3 Реєстрація тестового обробника автентифікації

```csharp
public class AuthenticatedWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Видалити реальну автентифікацію
            services.RemoveAll<IAuthenticationHandler>();

            // Додати тестову автентифікацію
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, options => { });

            // Замінити інші сервіси за потреби
            services.RemoveAll<ITaskRepository>();
            services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();
        });

        builder.UseEnvironment("Testing");
    }
}
```

### 10.4 Тестування автентифікованих ендпоінтів

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
        // Act — TestAuthHandler автоматично автентифікує
        var response = await _client.GetAsync("/api/tasks");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_UnauthenticatedUser_Returns401Async()
    {
        // Arrange — сигнал тестовому обробнику відхилити
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
        // Arrange — створюємо завдання, потім видаляємо як адмін
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
        // Arrange — ендпоінт вимагає роль Admin
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/tasks/1");
        // Без заголовка X-Test-Role — за замовчуванням лише роль "User"

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
```

### 10.5 Сценарії авторизації для тестування

| Сценарій | Очікуваний статус | Опис тесту |
|---|---|---|
| Без облікових даних | 401 Unauthorized | Анонімний доступ до захищеного ендпоінту |
| Валідні облікові дані, без дозволу | 403 Forbidden | Користувач без необхідної ролі |
| Валідні облікові дані, є дозвіл | 200/201/204 | Авторизований доступ |
| Прострочений токен | 401 Unauthorized | Застаріла автентифікація |
| Адмін отримує доступ до ресурсу користувача | 200 | Підвищені привілеї |
| Користувач отримує доступ до даних іншого користувача | 403 Forbidden | Авторизація на рівні ресурсу |

> **Дискусія (10 хв):** У продакшні ваш API використовує JWT-токени від провайдера ідентифікації (напр., Auth0, Azure AD). Чому прийнятно обходити реальну валідацію JWT в інтеграційних тестах? Коли б ви хотіли тестувати з реальними JWT?

---

## 11. Розширена конфігурація з WithWebHostBuilder

### 11.1 Інлайн-конфігурація без власної фабрики

Для одноразових налаштувань використовуйте `WithWebHostBuilder` безпосередньо на `WebApplicationFactory`:

```csharp
[Fact]
public async Task GetAll_WithCustomConfiguration_UsesTestSettingsAsync()
{
    // Arrange
    await using var factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            // Перевизначення конфігурації
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Features:EnableCaching"] = "false",
                    ["ExternalApi:BaseUrl"] = "http://fake-api.local",
                    ["Logging:LogLevel:Default"] = "Warning"
                });
            });

            // Перевизначення сервісів
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

### 11.2 Доступ до сервісів з тестів

Ви можете отримати сервіси з DI-контейнера фабрики для перевірки стану або заповнення даними:

```csharp
[Fact]
public async Task Create_ValidTask_IsSavedToRepositoryAsync()
{
    // Arrange
    var request = new CreateTaskRequest("DI Test", "Checking DI");

    // Act
    await _client.PostAsJsonAsync("/api/tasks", request);

    // Assert — отримуємо репозиторій та перевіряємо безпосередньо
    using var scope = _factory.Services.CreateScope();
    var repo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
    var allTasks = await repo.GetAllAsync();

    allTasks.ShouldContain(t => t.Title == "DI Test");
}
```

### 11.3 Використання NSubstitute всередині WebApplicationFactory

Ви можете впроваджувати NSubstitute моки в DI-контейнер для детального контролю:

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

    // Assert — перевірка, що мок був викликаний
    await notificationService.Received(1)
        .NotifyTaskCreatedAsync(Arg.Is<string>(t => t == "Notify Test"));
}
```

---

## 12. Поширені помилки та найкращі практики

### 12.1 Поширені помилки

#### Помилка 1: Забули `public partial class Program`

```csharp
// Без цього WebApplicationFactory<Program> не зможе знайти ваш додаток
// Додайте внизу Program.cs:
public partial class Program { }
```

**Симптом:** Помилка компіляції: `'Program' is inaccessible due to its protection level`

#### Помилка 2: Не утилізована фабрика

```csharp
// ПОГАНО — фабрика ніколи не утилізується, TestServer витікає
var factory = new WebApplicationFactory<Program>();
var client = factory.CreateClient();
// ... тести виконуються, але сервер ніколи не зупиняється

// ДОБРЕ — використовуйте IClassFixture (xUnit керує утилізацією)
public class MyTests : IClassFixture<WebApplicationFactory<Program>> { }

// ДОБРЕ — використовуйте await using для інлайн фабрик
await using var factory = new WebApplicationFactory<Program>();
```

#### Помилка 3: Спільний змінний стан між тестами

```csharp
// ПОГАНО — in-memory репозиторій накопичує дані між тестами
public class Tests : IClassFixture<CustomFactory>
{
    [Fact] public async Task Test1_CreatesData() { /* додає завдання */ }
    [Fact] public async Task Test2_AssumesEmptyState() { /* ПРОВАЛ! */ }
}

// ДОБРЕ — скидайте стан або використовуйте унікальні дані
public class Tests : IClassFixture<CustomFactory>, IAsyncLifetime
{
    public Task InitializeAsync() { /* очищення репозиторію */ }
}
```

#### Помилка 4: Тестування з реальними зовнішніми сервісами

```csharp
// ПОГАНО — тест викликає реальний платіжний API
// Повільно, нестабільно, може списати реальні гроші!

// ДОБРЕ — замініть фейком або моком
services.RemoveAll<IPaymentGateway>();
services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
```

#### Помилка 5: Ігнорування невідповідності lifetime сервісів

```csharp
// ПОГАНО — заміна Scoped сервісу на Singleton може спричинити проблеми
services.RemoveAll<ITaskRepository>();           // був Scoped
services.AddSingleton<ITaskRepository>(mock);    // тепер Singleton

// Мок буде спільним для всіх запитів — що може бути або не бути
// тим, що ви хочете. Якщо мок має змінний стан, будьте обережні.

// ДОБРЕ — відповідайте lifetime або будьте явними щодо вибору
services.RemoveAll<ITaskRepository>();
services.AddScoped<ITaskRepository>(_ => CreateFreshMock());
```

#### Помилка 6: Жорстко закодовані порти або URL

```csharp
// ПОГАНО — припускає конкретний порт
var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };

// ДОБРЕ — дозвольте фабриці налаштувати базову адресу
var client = factory.CreateClient(); // базова адреса встановлюється автоматично
```

### 12.2 Підсумок найкращих практик

```
Найкращі практики інтеграційного тестування з WebApplicationFactory
──────────────────────────────────────────────────────────────────

1. ВИКОРИСТОВУЙТЕ IClassFixture для спільного використання фабрики між тестами класу
   └── Уникає створення нового TestServer на кожен тест (дорого)

2. ЗАМІНЮЙТЕ зовнішні залежності (бази даних, API, email)
   └── Використовуйте фейки для простої поведінки, моки для перевірки

3. ТРИМАЙТЕ тести незалежними
   └── Кожен тест повинен проходити при запуску окремо або з іншими тестами

4. ТЕСТУЙТЕ HTTP-контракт, а не внутрішню реалізацію
   └── Перевіряйте коди стану, заголовки та форму тіла відповіді

5. ВИКОРИСТОВУЙТЕ зрозумілі назви тестів
   └── GetById_NonExistentTask_Returns404Async

6. ОЧИЩУЙТЕ стан між тестами
   └── IAsyncLifetime або унікальні дані для кожного тесту

7. ТЕСТУЙТЕ як щасливі шляхи, ТАК І шляхи помилок
   └── 400, 401, 403, 404, 409, 500

8. ПЕРЕВІРЯЙТЕ вміст відповіді, а не тільки коди стану
   └── Відповідь 200 з неправильними даними все одно є помилкою

9. ВИКОРИСТОВУЙТЕ System.Net.Http.Json для чистої серіалізації
   └── PostAsJsonAsync, GetFromJsonAsync, ReadFromJsonAsync

10. ДОТРИМУЙТЕСЬ конвенції іменування Async
    └── Суфікс Async для всіх асинхронних тестових методів
```

---

## 13. Збираємо все разом: Повний приклад

### 13.1 Структура проєкту

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

### 13.2 Повний тестовий клас

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
        // Скидання репозиторію перед кожним тестом
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        if (repo is InMemoryTaskRepository inMemRepo)
            inMemRepo.Clear();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Допоміжні методи ─────────────────────────────────────

    private async Task<TaskItem> CreateTestTaskAsync(
        string title = "Test Task", string? description = null)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/tasks", new CreateTaskRequest(title, description));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskItem>())!;
    }

    // ── GET /api/tasks ───────────────────────────────────────

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

    // ── GET /api/tasks/{id} ──────────────────────────────────

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

    // ── POST /api/tasks ──────────────────────────────────────

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

    // ── PUT /api/tasks/{id} ──────────────────────────────────

    [Fact]
    public async Task Update_ExistingTask_Returns204AndPersistsChangesAsync()
    {
        var created = await CreateTestTaskAsync("Before Update");
        var updateRequest = new UpdateTaskRequest(
            "After Update", "New Description", true);

        var response = await _client.PutAsJsonAsync(
            $"/api/tasks/{created.Id}", updateRequest);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Перевірка збереження змін
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

    // ── DELETE /api/tasks/{id} ───────────────────────────────

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

    // ── Узгодження контенту ──────────────────────────────────

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

## 14. Коли використовувати інтеграційні тести vs. модульні тести

### 14.1 Посібник з прийняття рішень

```
Запитайте себе:
                                          ┌──────────────────────┐
"Чи тестую я бізнес-логіку              │                      │
 без I/O або інфраструктури?"──ТАК─────►│  Пишіть МОДУЛЬНИЙ    │
        │                                │  ТЕСТ                │
        НІ                               └──────────────────────┘
        │
        ▼
"Чи тестую я взаємодію                   ┌──────────────────────┐
 компонентів (маршрутизація,             │                      │
 DI, middleware, серіалізація)?"──ТАК───►│ Пишіть ІНТЕГРАЦІЙНИЙ │
        │                                │ ТЕСТ                 │
        НІ                               └──────────────────────┘
        │
        ▼
"Чи тестую я повну систему              ┌──────────────────────┐
 з перспективи користувача               │                      │
 (браузер, реальна БД, реальні API)?"─ТАК►│  Пишіть E2E ТЕСТ   │
        │                                │                      │
        НІ                               └──────────────────────┘
        │
        ▼
  Переосмисліть, що ви тестуєте.
```

### 14.2 Що тестувати де

| Питання | Модульний тест | Інтеграційний тест |
|---|---|---|
| Бізнес-правило: знижка 10% для замовлень понад $100 | Так | Ні |
| Контролер повертає 404 для відсутнього ресурсу | Ні | Так |
| Сервіс коректно перетворює DTO на сутність | Так | Ні |
| POST /api/tasks повертає 201 із заголовком Location | Ні | Так |
| Валідація відхиляє порожні рядки | Так | Так (обидва) |
| Автентифікація відхиляє неавтентифіковані запити | Ні | Так |
| Назви JSON-властивостей у camelCase | Ні | Так |
| Запит до бази даних повертає коректні результати | Ні | Так (Лекція 4) |
| Middleware перехоплює винятки та повертає 500 | Ні | Так |

> **Дискусія (5 хв):** Ваша команда має обмежений час. Чи потрібно писати більше модульних тестів чи інтеграційних? Яке співвідношення вартість/вигода кожного?

---

## 15. Практична вправа

### Завдання: Створити інтеграційні тести для BookStore API

Вам дано `BookStoreApi` з наступними ендпоінтами:

| Метод | Ендпоінт | Опис |
|---|---|---|
| GET | `/api/books` | Список усіх книг |
| GET | `/api/books/{id}` | Отримати книгу за ID |
| POST | `/api/books` | Створити нову книгу |
| PUT | `/api/books/{id}` | Оновити книгу |
| DELETE | `/api/books/{id}` | Видалити книгу (лише Admin) |
| GET | `/api/books/search?query=...` | Пошук книг за назвою |

**Моделі:**

```csharp
public record Book(int Id, string Title, string Author, decimal Price, int Year);
public record CreateBookRequest(string Title, string Author, decimal Price, int Year);
public record UpdateBookRequest(string Title, string Author, decimal Price, int Year);
```

**Ваші завдання:**

1. Створити `CustomWebApplicationFactory`, що замінює реальну базу даних на in-memory фейк
2. Написати інтеграційні тести, що покривають:
   - GET всіх книг повертає 200 з коректним JSON
   - GET за ID повертає 404 для неіснуючої книги
   - POST створює книгу та повертає 201 із заголовком Location
   - POST з відсутніми обов'язковими полями повертає 400
   - PUT оновлює існуючу книгу та повертає 204
   - DELETE як Admin повертає 204
   - DELETE як звичайний User повертає 403
   - Search повертає відповідні книги
3. Реалізувати ізоляцію тестів (скидання стану між тестами)
4. Додати тестування автентифікації за допомогою `TestAuthHandler`

**Бонусні завдання:**
- Перевірити, що API повертає коректні `ProblemDetails` для помилок валідації
- Тестувати узгодження контенту (заголовок Accept)
- Перевірити, що відповідь включає заголовки пагінації для GET all

> **Дискусія (15 хв):** Перегляньте тести одне одного. Чи є граничні випадки, які пропущені? Наскільки читабельні назви тестів? Чи могли б ви зрозуміти очікувану поведінку API, просто читаючи назви тестів?

---

## 16. Підсумок

### Ключові висновки

1. **Інтеграційні тести перевіряють взаємодію компонентів** — вони виявляють помилки, які модульні тести не можуть, такі як помилки маршрутизації, неправильна конфігурація DI та проблеми серіалізації

2. **WebApplicationFactory створює in-memory тестовий сервер** — без реальних портів, без мережевої затримки, той самий конвеєр ASP.NET Core, що й у продакшні

3. **Власні фабрики дозволяють замінювати сервіси** — заміна реальних баз даних, API та email-сервісів на фейки або моки за допомогою `ConfigureServices`

4. **Тестуйте всі HTTP-дієслова та коди стану** — GET (200, 404), POST (201, 400), PUT (204, 404), DELETE (204, 404), автентифікація (401, 403)

5. **IClassFixture поділяє фабрику** між тестами класу, тоді як **IAsyncLifetime** забезпечує налаштування та очищення для кожного тесту

6. **Ізоляція тестів є критичною** — використовуйте скидання стану, унікальні дані або нові фабрики для запобігання взаємному впливу тестів

7. **Автентифікацію можна імітувати** за допомогою власного `AuthenticationHandler`, що створює тестові claims та ролі

8. **Middleware можна тестувати** — обробники винятків, CORS, логування та інші компоненти конвеєра можна перевірити через HTTP-відповіді

9. **Використовуйте `System.Net.Http.Json`** для чистої серіалізації JSON у тестах (`PostAsJsonAsync`, `GetFromJsonAsync`)

10. **Дотримуйтесь конвенції іменування Async** — всі асинхронні тестові методи повинні закінчуватися на `Async`

### Анонс наступної лекції

У **Лекції 4: Тестування бази даних з Testcontainers** ми:
- Тестуватимемо реальні запити та міграції бази даних з EF Core
- Використаємо Testcontainers для запуску SQL Server та PostgreSQL в Docker
- Порівняємо стратегії тестування: InMemory, SQLite та Testcontainers
- Тестуватимемо цілісність даних, транзакції та конкурентний доступ
- Інтегруємо тестування бази даних з `WebApplicationFactory`

---

## Посилання та додаткова література

- **Microsoft Documentation: Integration tests in ASP.NET Core**
  - https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
- **Microsoft Documentation: WebApplicationFactory**
  - https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.testing.webapplicationfactory-1
- **Microsoft Documentation: TestServer**
  - https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.testhost.testserver
- **xUnit v3 Documentation** — https://xunit.net/docs/getting-started/v3/cmdline
- **Shouldly Documentation** — https://docs.shouldly.org/
- **NSubstitute Documentation** — https://nsubstitute.github.io/help/getting-started/
- **"Unit Testing Principles, Practices, and Patterns"** — Vladimir Khorikov (Manning, 2020) — Розділи 8-9 про інтеграційне тестування
- **ASP.NET Core in Action** — Andrew Lock (Manning, 3rd edition, 2023) — Розділ 36 про тестування
- **System.Net.Http.Json Namespace** — https://learn.microsoft.com/en-us/dotnet/api/system.net.http.json
