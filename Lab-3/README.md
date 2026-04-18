# Лабораторна 3 — Інтеграційне тестування: Компоненти

> **Lab → 2 points**

## Мета

Навчитися писати інтеграційні тести, що перевіряють спільну роботу кількох компонентів. Тестувати реальну взаємодію між сервісами, репозиторіями та проміжним програмним забезпеченням (middleware) без повної імітації всього.

**Тривалість:** 60 хвилин

## Передумови

Перед початком цієї лабораторної переконайтеся, що:

- Встановлений .NET 10+ SDK (`dotnet --version`)
- Ви маєте робоче розуміння конвеєра middleware ASP.NET Core, впровадження залежностей та шаблону контролер/сервіс/репозиторій
- Ви знайомі з життєвим циклом тестів xUnit v3 (`IAsyncLifetime`, `IClassFixture<T>`)
- Виконані Лабораторна 1 та Лабораторна 2 (основи модульного тестування та імітації)

## Ключові поняття

### WebApplicationFactory

`WebApplicationFactory<TEntryPoint>` розгортає ваш застосунок ASP.NET Core у пам'яті для тестування. Він створює `TestServer` та `HttpClient`, що надсилає запити безпосередньо до конвеєра без мережевих витрат. Ви можете створити підклас для налаштування сервісів, конфігурації або middleware.

### Конвеєр Middleware

ASP.NET Core обробляє кожен HTTP-запит через упорядкований конвеєр компонентів middleware. Інтеграційні тести перевіряють, що весь конвеєр (автентифікація, журналювання, обробка винятків, маршрутизація, виконання контролера) працює разом як очікувалося.

### Впровадження залежностей у тестах

DI-контейнер можна переналаштувати всередині `WebApplicationFactory.WithWebHostBuilder`, щоб замінити реальні сервіси тестовими замінниками або реалізаціями у пам'яті. Це дозволяє тестувати реальну взаємодію компонентів, контролюючи зовнішні залежності.

### Ізоляція тестів

Кожен тест повинен бути незалежним. Спільний стан між тестами призводить до нестабільних результатів. Використовуйте унікальні імена баз даних, нові екземпляри `HttpClient` або `IAsyncLifetime` для налаштування та очищення стану кожного тесту.

## Інструменти

- Мова: C#
- Фреймворк: [xUnit v3](https://xunit.net/) (`xunit.v3`)
- Веб: [ASP.NET Core TestServer](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
- DI: `Microsoft.Extensions.DependencyInjection`

## Налаштування

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

## Завдання

### Завдання 1 — Налаштування WebApplicationFactory

Створіть мінімальний ASP.NET Core Web API з:

- `ProductsController` з CRUD-ендпоінтами (`GET`, `POST`, `PUT`, `DELETE`)
- Інтерфейсом `IProductRepository` з реалізацією у пам'яті
- Реєстрацією сервісів у `Program.cs`

Напишіть інтеграційні тести з використанням `WebApplicationFactory<Program>`:

1. Створіть власну `WebApplicationFactory`, що замінює реальний репозиторій на попередньо заповнений репозиторій у пам'яті
2. Протестуйте, що `GET /api/products` повертає всі попередньо додані продукти
3. Протестуйте, що `GET /api/products/{id}` повертає 200 для існуючого та 404 для неіснуючого
4. Протестуйте, що `POST /api/products` створює продукт та повертає 201

**Мінімальна кількість тестів: 5 тестів**

#### Приклад: Власна WebApplicationFactory

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

#### Приклад: Інтеграційний тест з Shouldly

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

#### Таблиця очікуваної поведінки

| Ендпоінт | Сценарій | Очікуваний статус | Очікуване тіло відповіді |
|---|---|---|---|
| `GET /api/products` | Існують попередньо додані дані | 200 OK | Масив з 2 продуктів |
| `GET /api/products/1` | Продукт існує | 200 OK | JSON продукту |
| `GET /api/products/999` | Продукт не знайдено | 404 Not Found | Помилка або порожньо |
| `POST /api/products` | Валідний продукт | 201 Created | Створений продукт + заголовок Location |
| `PUT /api/products/1` | Валідне оновлення | 200 OK | Оновлений продукт |
| `DELETE /api/products/1` | Продукт існує | 204 No Content | Порожньо |

> **Підказка:** Зробіть `Program` доступним для тестів, додавши `public partial class Program { }` в кінці `Program.cs`, або додавши `[assembly: InternalsVisibleTo("Lab3.Tests")]` у проєкті API.

### Завдання 2 — Тестування Middleware та конвеєра

Додайте наступне middleware до API:

- Middleware журналювання запитів (логує метод, шлях, код статусу)
- Middleware обробки винятків (перехоплює необроблені винятки, повертає 500 з JSON-помилкою)
- Middleware автентифікації за API-ключем (перевіряє заголовок `X-Api-Key`)

Напишіть інтеграційні тести, що:

1. Перевіряють, що запити без `X-Api-Key` повертають 401
2. Перевіряють, що запити з невалідним ключем повертають 403
3. Перевіряють, що необроблені винятки повертають структуровану JSON-відповідь з помилкою
4. Перевіряють, що конвеєр обробляє запити у правильному порядку

**Мінімальна кількість тестів: 5 тестів**

#### Приклад: Тест Middleware з Shouldly

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

#### Таблиця очікуваної поведінки

| Сценарій | Заголовок `X-Api-Key` | Очікуваний статус |
|---|---|---|
| Заголовок не надіслано | (відсутній) | 401 Unauthorized |
| Невалідний ключ | `"wrong-key"` | 403 Forbidden |
| Валідний ключ | `"valid-test-key"` | 200 OK (або результат ендпоінта) |
| Ендпоінт кидає виняток | Валідний ключ | 500 з JSON-тілом |

> **Підказка:** Для перевірки порядку конвеєра розгляньте можливість додавання власного заголовка відповіді з кожного middleware (наприклад, `X-Pipeline-Step: 1`, `X-Pipeline-Step: 2`) та перевірки порядку у відповіді.

## Оцінювання

| Критерії |
|----------|
| Завдання 1 — Тести WebApplicationFactory |
| Завдання 2 — Тести Middleware |
| Належна ізоляція тестів (кожен тест незалежний) |
| Використання `IClassFixture` або `IAsyncLifetime` |

## Здача роботи

- Рішення з проєктами `Lab3.Api` та `Lab3.Tests`
- Тести повинні проходити з `dotnet test` без зовнішніх залежностей

## Посилання

- [Інтеграційні тести в ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) — офіційний посібник Microsoft з `WebApplicationFactory`
- [Документація xUnit v3](https://xunit.net/docs/getting-started/v3/cmdline) — довідник тестового фреймворку
- [Документація Shouldly](https://docs.shouldly.org/) — API бібліотеки перевірок
- [Middleware ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/) — концепції конвеєра middleware
- [Впровадження залежностей в ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection) — час життя сервісів (Transient, Scoped, Singleton)
- [Andrew Lock: Інтеграційне тестування з WebApplicationFactory](https://andrewlock.net/introduction-to-integration-testing-with-xunit-and-testserver-in-asp-net-core/) — практичний покроковий посібник
