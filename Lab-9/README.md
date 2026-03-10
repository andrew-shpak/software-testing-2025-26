# Лабораторна 9 — Тестування сервісів: REST API

## Мета

Тестування інтеграцій з зовнішніми REST API з використанням абстракції HTTP-клієнта, обробки відповідей, політик повторних спроб та симуляції поведінки API за допомогою WireMock.

**Тривалість:** 60 хвилин

## Передумови

- Встановлений .NET 10 SDK або новіший
- Основи C#, включаючи інтерфейси, впровадження залежностей та async/await
- Розуміння HTTP-методів (GET, POST, PUT, DELETE), кодів стану та заголовків
- Знайомство з `HttpClient` та `IHttpClientFactory` у .NET
- Розуміння серіалізації/десеріалізації JSON з `System.Text.Json`
- Базові знання шаблонів стійкості: повторні спроби, переривач ланцюга та тайм-аути

## Ключові концепції

| Концепція | Опис |
|-----------|------|
| **Типізований HttpClient** | Строго типізована обгортка навколо `HttpClient`, зареєстрована через `IHttpClientFactory`. Забезпечує чітке розділення та тестованість. |
| **WireMock.Net** | Внутрішньопроцесний HTTP-сервер, який можна запрограмувати для повернення конкретних відповідей на конкретні запити. Замінює реальний зовнішній API під час тестів. |
| **Заглушка vs Мок (контекст HTTP)** | Заглушка WireMock повертає заготовлену відповідь. Мок WireMock також перевіряє, що певні запити були зроблені (верифікація запитів). |
| **Конвеєр стійкості** | Ланцюг стратегій (повторна спроба, переривач ланцюга, тайм-аут), що застосовуються до вихідних HTTP-запитів через `Microsoft.Extensions.Http.Resilience`. |
| **Політика повторних спроб** | Автоматично повторно надсилає невдалий запит налаштовану кількість разів із необов'язковою затримкою між спробами. Спрямована на тимчасові збої (5xx, мережеві помилки). |
| **Переривач ланцюга** | Моніторить частоту збоїв і "відкривається" (блокує всі запити), коли збої перевищують поріг, запобігаючи каскадним збоям. Після періоду охолодження "напіввідкривається" для перевірки відновлення. |
| **Тайм-аут спроби** | Скасовує окремий HTTP-запит, якщо він не завершується протягом налаштованої тривалості. Відрізняється від загального тайм-ауту для всіх повторних спроб. |
| **IAsyncLifetime** | Інтерфейс xUnit для асинхронного налаштування (`InitializeAsync`) та очищення (`DisposeAsync`). Використовується для запуску та зупинки сервера WireMock для кожного класу тестів. |

## Інструменти

- Мова: C#
- HTTP: `HttpClient` / `IHttpClientFactory`
- Мок-сервер: [WireMock.Net](https://github.com/WireMock-Net/WireMock.Net)
- Стійкість: [Microsoft.Extensions.Http.Resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience)
- Фреймворк: [xUnit v3](https://xunit.net/) (`xunit.v3`)

## Налаштування

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

## Завдання

### Завдання 1 — Клієнт зовнішнього API

Створіть типізований HTTP-клієнт для гіпотетичного User API:

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
    // Реалізуйте всі методи з належною обробкою помилок
    // Зіставте HTTP-коди стану з відповідними винятками
}
```

Реалізуйте:

- Належну десеріалізацію відповідей
- Генерацію `NotFoundException` для 404
- Генерацію `ApiException` для 5xx з інформацією про повторну спробу
- Зіставлення помилок валідації (400) з `ValidationException`

**Приклад — доменні моделі та користувацькі винятки**

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

**Приклад — реалізація UserApiClient (частково)**

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

    // Реалізуйте решту методів за тим самим шаблоном...
}
```

**Очікуване зіставлення кодів стану**

| HTTP-код стану | Очікувана поведінка |
|----------------|---------------------|
| 200 OK | Десеріалізувати та повернути тіло відповіді |
| 201 Created | Десеріалізувати створену сутність з тіла відповіді |
| 204 No Content | Повернути успішно (тіло для розбору відсутнє) |
| 400 Bad Request | Згенерувати `ValidationException` з деталями помилок на рівні полів |
| 404 Not Found | Згенерувати `NotFoundException` з описовим повідомленням |
| 500 Internal Server Error | Згенерувати `ApiException` з кодом стану |
| 502 Bad Gateway | Згенерувати `ApiException` (тимчасова помилка, підлягає повторній спробі) |
| 503 Service Unavailable | Згенерувати `ApiException` (тимчасова помилка, підлягає повторній спробі) |

**Мінімальна кількість тестів для Завдання 1**: 4 тести (покриття основних методів інтерфейсу, перевірка успішного шляху).

> **Підказка**: Зареєструйте `UserApiClient` як типізований клієнт, щоб `IHttpClientFactory` керував часом життя його `HttpClient`. Це запобігає вичерпанню сокетів:
> ```csharp
> services.AddHttpClient<IUserApiClient, UserApiClient>(client =>
> {
>     client.BaseAddress = new Uri("https://api.example.com");
> });
> ```

### Завдання 2 — Інтеграційні тести з WireMock

Використовуйте WireMock.Net для симуляції зовнішнього API:

```csharp
var server = WireMockServer.Start();
```

Напишіть тести, які:

1. Імітують `GET /users/1`, що повертає JSON-користувача — перевірте десеріалізацію
2. Імітують `GET /users/999`, що повертає 404 — перевірте, що генерується `NotFoundException`
3. Імітують `POST /users`, що повертає 201 із заголовком `Location` — перевірте розбір відповіді
4. Імітують `GET /users` з параметрами запиту — перевірте, що пагінація надсилається коректно

*Необов'язково (якщо є час):*
- Імітувати повільну відповідь (затримка 5 секунд) — перевірити обробку тайм-ауту
- Імітувати послідовність: перший виклик повертає 500, другий — 200 — перевірити роботу повторних спроб
- Перевірити, що заголовки запиту (`Content-Type`, `Authorization`) надсилаються коректно

**Приклад — тестова фікстура WireMock з IAsyncLifetime**

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

**Приклад — тестування повільної відповіді (тайм-аут)**

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
        Timeout = TimeSpan.FromSeconds(2) // тайм-аут до затримки 5 с
    };
    var client = new UserApiClient(httpClient);

    // Act & Assert
    await Should.ThrowAsync<TaskCanceledException>(
        () => client.GetUserAsync(1));
}
```

**Приклад — тестування повторної спроби з послідовністю відповідей**

```csharp
[Fact]
public async Task GetUserAsync_RetriesAndSucceeds_WhenFirstCallFailsAsync()
{
    // Arrange: перший виклик повертає 500, другий — 200
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

    // Act — клієнт (або його обробник стійкості) повинен повторити спробу
    var user = await _client.GetUserAsync(1);

    // Assert
    user.ShouldNotBeNull();
    user.Name.ShouldBe("Alice");
}
```

**Приклад — перевірка надісланих заголовків запиту**

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

    // Assert — перевірка, що WireMock отримав коректні заголовки
    _server.LogEntries.ShouldContain(entry =>
        entry.RequestMessage.Headers!.ContainsKey("Content-Type") &&
        entry.RequestMessage.Headers["Content-Type"]
            .Any(v => v.Contains("application/json")));
}
```

**Мінімальна кількість тестів для Завдання 2**: 5 тестів (4 обов'язкових сценарії вище + щонайменше 1 необов'язковий сценарій).

**Бонус (якщо є час):** Налаштуйте стійкість за допомогою `AddStandardResilienceHandler` та протестуйте поведінку повторних спроб.

> **Підказка**: Завжди скидайте сервер WireMock між тестами, якщо вони використовують спільний екземпляр. Ви можете викликати `_server.Reset()` у методі налаштування або використовувати `IAsyncLifetime` для створення нового сервера на клас. Якщо ізоляція окремих тестів критична, створюйте сервер для кожного тесту.

> **Підказка**: Використовуйте API `InScenario` / `WillSetStateTo` / `WhenStateIs` WireMock для створення послідовностей відповідей зі станом (наприклад, перший виклик невдалий, другий успішний).

## Оцінювання

| Критерії |
|----------|
| Завдання 1 — Реалізація клієнта API |
| Завдання 2 — Тести WireMock |
| Належна ієрархія винятків |
| Очищення сервера WireMock (IDisposable) |

## Здача роботи

- Рішення з проєктами `Lab9.Core` та `Lab9.Tests`
- Сервер WireMock запускається/зупиняється для кожного класу тестів за допомогою `IAsyncLifetime`

## Посилання

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
